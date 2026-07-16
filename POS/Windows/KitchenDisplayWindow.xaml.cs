using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;

namespace KosovaPOS.Windows
{
    public partial class KitchenDisplayWindow : Window
    {
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _clockTimer;
        private string _selectedStation = "All";
        private List<KitchenOrder> _orders = new List<KitchenOrder>();
        private bool _tableErrorShown = false;

        public KitchenDisplayWindow()
        {
            InitializeComponent();
            InitializeTimers();
            LoadOrders();
        }

        private void InitializeTimers()
        {
            // Clock timer
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            // Auto-refresh orders every 10 seconds
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _refreshTimer.Tick += (s, e) => LoadOrders();
            _refreshTimer.Start();
        }

        private void UpdateClock()
        {
            txtCurrentTime.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy - HH:mm:ss");
        }

        private void LoadOrders()
        {
            try
            {
                using var context = new POSDbContext();

                // Check if table exists
                try
                {
                    var query = context.KitchenOrders
                        .Where(o => o.Status != "Served" && o.Status != "Cancelled")
                        .OrderBy(o => o.ReceivedAt)
                        .AsQueryable();

                    if (_selectedStation != "All")
                    {
                        query = query.Where(o => o.Station == _selectedStation);
                    }

                    _orders = query.ToList();
                }
                catch (Exception ex) when (ex.Message.Contains("Invalid object name") || 
                                          ex.InnerException?.Message.Contains("Invalid object name") == true)
                {
                    // KitchenOrders table doesn't exist yet - show friendly message
                    _orders = new List<KitchenOrder>();

                    // Only show error once
                    if (!_tableErrorShown)
                    {
                        _tableErrorShown = true;
                        MessageBox.Show(
                            "Tabela e porosive të kuzhinës nuk ekziston ende në databazë.\n\n" +
                            "Ju lutem ekzekutoni migracionet e databazës për të krijuar tabelat e nevojshme.\n\n" +
                            "Sistemi i Kuzhinës do të funksionojë pas krijimit të tabelave.",
                            "Tabela Mungon",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }

                DisplayOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të porosive:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                _orders = new List<KitchenOrder>();
                DisplayOrders();
            }
        }

        private void DisplayOrders()
        {
            panelNewOrders.Children.Clear();
            panelPreparingOrders.Children.Clear();
            panelReadyOrders.Children.Clear();

            foreach (var order in _orders)
            {
                var orderCard = CreateOrderCard(order);

                switch (order.Status)
                {
                    case "New":
                        panelNewOrders.Children.Add(orderCard);
                        break;
                    case "Preparing":
                        panelPreparingOrders.Children.Add(orderCard);
                        break;
                    case "Ready":
                        panelReadyOrders.Children.Add(orderCard);
                        break;
                }
            }

            // Add empty state messages
            if (panelNewOrders.Children.Count == 0)
            {
                panelNewOrders.Children.Add(CreateEmptyStateMessage("Nuk ka porosi të reja"));
            }
            if (panelPreparingOrders.Children.Count == 0)
            {
                panelPreparingOrders.Children.Add(CreateEmptyStateMessage("Nuk ka porosi në përgatitje"));
            }
            if (panelReadyOrders.Children.Count == 0)
            {
                panelReadyOrders.Children.Add(CreateEmptyStateMessage("Nuk ka porosi gati"));
            }
        }

        private Border CreateOrderCard(KitchenOrder order)
        {
            var card = new Border
            {
                Style = (Style)FindResource("OrderCard")
            };

            // Set border color based on priority and elapsed time
            var elapsedMinutes = (DateTime.Now - order.ReceivedAt).TotalMinutes;
            var borderBrush = order.Priority == "Urgent" ? Brushes.Red :
                             elapsedMinutes > order.TargetTime ? Brushes.Orange :
                             Brushes.Green;

            card.BorderBrush = borderBrush;
            card.BorderThickness = new Thickness(4);

            var stackPanel = new StackPanel();

            // Order Header
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var orderNumberText = new TextBlock
            {
                Text = $"#\u2003Porosi #{order.OrderNumber}",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59))
            };
            Grid.SetColumn(orderNumberText, 0);
            headerGrid.Children.Add(orderNumberText);

            var timeText = new TextBlock
            {
                Text = $"\u23F1 {elapsedMinutes:F0}m",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = elapsedMinutes > order.TargetTime ? Brushes.Red : Brushes.Green
            };
            Grid.SetColumn(timeText, 1);
            headerGrid.Children.Add(timeText);

            stackPanel.Children.Add(headerGrid);

            // Order Type & Table/Customer
            var infoText = new TextBlock
            {
                Text = order.OrderType == "DineIn" && !string.IsNullOrEmpty(order.TableNumber)
                    ? $"\u2302 Tavolina: {order.TableNumber}"
                    : order.OrderType == "Delivery" && !string.IsNullOrEmpty(order.CustomerName)
                    ? $"\u27A4 {order.CustomerName}"
                    : $"\u2610 {order.OrderType}",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 8, 0, 0)
            };
            stackPanel.Children.Add(infoText);

            // Station
            if (!string.IsNullOrEmpty(order.Station))
            {
                var stationBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                stationBadge.Child = new TextBlock
                {
                    Text = order.Station.ToUpper(),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                };
                stackPanel.Children.Add(stationBadge);
            }

            // Order Items
            if (!string.IsNullOrEmpty(order.OrderItems))
            {
                var itemsText = new TextBlock
                {
                    Text = order.OrderItems,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 12, 0, 0)
                };
                stackPanel.Children.Add(itemsText);
            }

            // Special Instructions
            if (!string.IsNullOrEmpty(order.SpecialInstructions))
            {
                var instructionsText = new TextBlock
                {
                    Text = $"\u2709 {order.SpecialInstructions}",
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.DarkRed,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                stackPanel.Children.Add(instructionsText);
            }

            // Priority Badge
            if (order.Priority == "High" || order.Priority == "Urgent")
            {
                var priorityColor = order.Priority == "Urgent"
                    ? Color.FromRgb(220, 38, 38)   // #DC2626 - theme-consistent red
                    : Color.FromRgb(217, 119, 6);  // #D97706 - theme amber/warning
                var priorityBadge = new Border
                {
                    Background = new SolidColorBrush(priorityColor),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                priorityBadge.Child = new TextBlock
                {
                    Text = $"\u26A0 {order.Priority.ToUpper()}",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                stackPanel.Children.Add(priorityBadge);
            }

            // Action Buttons
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };

            if (order.Status == "New")
            {
                var startButton = new Button
                {
                    Content = "\u25B6 Filloje",
                    Style = (Style)FindResource("ActionButton"),
                    Background = new SolidColorBrush(Color.FromRgb(34, 197, 94))
                };
                startButton.Click += (s, e) => UpdateOrderStatus(order, "Preparing");
                buttonsPanel.Children.Add(startButton);
            }
            else if (order.Status == "Preparing")
            {
                var readyButton = new Button
                {
                    Content = "\u2705 Gati",
                    Style = (Style)FindResource("ActionButton"),
                    Background = new SolidColorBrush(Color.FromRgb(59, 130, 246))
                };
                readyButton.Click += (s, e) => UpdateOrderStatus(order, "Ready");
                buttonsPanel.Children.Add(readyButton);
            }
            else if (order.Status == "Ready")
            {
                var serveButton = new Button
                {
                    Content = "\u2714 Sh\u00ebrbyer",
                    Style = (Style)FindResource("ActionButton"),
                    Background = new SolidColorBrush(Color.FromRgb(2, 132, 199))
                };
                serveButton.Click += (s, e) => UpdateOrderStatus(order, "Served");
                buttonsPanel.Children.Add(serveButton);
            }

            // Cancel button (for all statuses)
            var cancelButton = new Button
            {
                Content = "\u2716",
                Style = (Style)FindResource("ActionButton"),
                Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Padding = new Thickness(10, 10, 10, 10)
            };
            cancelButton.Click += (s, e) => CancelOrder(order);
            buttonsPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(buttonsPanel);
            card.Child = stackPanel;

            return card;
        }

        private Border CreateEmptyStateMessage(string message)
        {
            var container = new Border
            {
                Padding = new Thickness(20),
                Margin = new Thickness(10),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                CornerRadius = new CornerRadius(12)
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontStyle = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = "Porositë dërgohen automatikisht nga Kasa (F5) ose mund t'i shtoni manualisht me butonin '+ Shto Porosi'.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                MaxWidth = 380
            });
            container.Child = sp;
            return container;
        }

        private void UpdateOrderStatus(KitchenOrder order, string newStatus)
        {
            try
            {
                using var context = new POSDbContext();
                var dbOrder = context.KitchenOrders.Find(order.Id);
                if (dbOrder != null)
                {
                    dbOrder.Status = newStatus;

                    switch (newStatus)
                    {
                        case "Preparing":
                            dbOrder.StartedAt = DateTime.Now;
                            break;
                        case "Ready":
                            dbOrder.ReadyAt = DateTime.Now;
                            // If this was a cashier-linked order, keep the sentinel
                            break;
                        case "Served":
                            dbOrder.ServedAt = DateTime.Now;
                            // Clear the cashier-pending sentinel once served
                            if (dbOrder.ReceiptId == -1)
                                dbOrder.ReceiptId = 0;
                            break;
                    }

                    dbOrder.UpdatedAt = DateTime.Now;
                    context.SaveChanges();
                    LoadOrders();

                    // Show notification when order is ready
                    if (newStatus == "Ready" && order.ReceiptId == -1)
                    {
                        MessageBox.Show(
                            $"Porosia #{order.OrderNumber} është GATI!\n\nArkëtari mund ta procesojë pagesën tani.",
                            "Porosi Gati", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë përditësimit të statusit:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelOrder(KitchenOrder order)
        {
            var result = MessageBox.Show(
                $"A jeni të sigurt që dëshironi të anuloni porosinë #{order.OrderNumber}?",
                "Anulo Porosinë",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                UpdateOrderStatus(order, "Cancelled");
            }
        }

        private void Station_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Reset all station buttons
                foreach (var child in ((StackPanel)button.Parent).Children)
                {
                    if (child is Button btn)
                    {
                        btn.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    }
                }

                // Highlight selected station
                button.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                _selectedStation = button.Tag?.ToString() ?? "All";
                LoadOrders();
            }
        }

        private void AddKitchenOrder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "➕ Shto Porosi Manuale - Kuzhina",
                Width = 580,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerBorder = new Border { Padding = new Thickness(24, 16, 24, 16) };
            headerBorder.Background = new LinearGradientBrush(Color.FromRgb(234, 88, 12), Color.FromRgb(194, 65, 12), 0);
            headerBorder.Child = new TextBlock
            {
                Text = "➕ Shto Porosi Manuale",
                FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            };
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var formStack = new StackPanel { Margin = new Thickness(20, 16, 20, 10) };

            // Row 1: Order Type + Station
            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row1.ColumnDefinitions.Add(new ColumnDefinition());
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row1.ColumnDefinitions.Add(new ColumnDefinition());
            var sp1L = new StackPanel(); sp1L.Children.Add(KdsLabel("Lloji:"));
            var cmbOrderType = new ComboBox { Height = 38, FontSize = 13 };
            foreach (var t in new[] { "DineIn", "TakeOut", "Delivery" }) cmbOrderType.Items.Add(t);
            cmbOrderType.SelectedIndex = 0;
            sp1L.Children.Add(cmbOrderType);
            Grid.SetColumn(sp1L, 0); row1.Children.Add(sp1L);
            var sp1R = new StackPanel(); sp1R.Children.Add(KdsLabel("Stacioni:"));
            var cmbStation = new ComboBox { Height = 38, FontSize = 13 };
            foreach (var s in new[] { "Kitchen", "Pizza", "Grill", "Salad", "Drinks", "Dessert" }) cmbStation.Items.Add(s);
            cmbStation.SelectedIndex = 0;
            sp1R.Children.Add(cmbStation);
            Grid.SetColumn(sp1R, 2); row1.Children.Add(sp1R);
            formStack.Children.Add(row1);

            // Table selection
            formStack.Children.Add(KdsLabel("Tavolina:"));
            var cmbTable = new ComboBox { Height = 38, FontSize = 13, IsEditable = true, Margin = new Thickness(0, 0, 0, 12) };
            cmbTable.Items.Add("-- Zgjidh tavolinë --");
            try
            {
                using var ctx0 = new POSDbContext();
                var tables = ctx0.RestaurantTables.Where(t => t.IsActive).OrderBy(t => t.TableNumber).ToList();
                foreach (var t in tables)
                    cmbTable.Items.Add(new ComboBoxItem { Content = $"Tavolina {t.TableNumber} ({t.Capacity}p.)", Tag = t.TableNumber });
            }
            catch { }
            cmbTable.SelectedIndex = 0;
            formStack.Children.Add(cmbTable);

            // Client selection
            formStack.Children.Add(KdsLabel("Klienti:"));
            var cmbClient = new ComboBox { Height = 38, FontSize = 13, IsEditable = true, Margin = new Thickness(0, 0, 0, 12) };
            cmbClient.Items.Add("-- Klient i pazidentifikuar --");
            try
            {
                using var ctx1 = new POSDbContext();
                var clients = ctx1.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).Take(100).ToList();
                foreach (var c in clients)
                    cmbClient.Items.Add(new ComboBoxItem { Content = $"{c.Name}{(c.Phone != null ? $" ({c.Phone})" : "")}", Tag = c.Name });
            }
            catch { }
            cmbClient.SelectedIndex = 0;
            formStack.Children.Add(cmbClient);

            // Article picker section
            formStack.Children.Add(KdsLabel("Shto Artikuj *:"));
            var pickerRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cmbArticle = new ComboBox { Height = 38, FontSize = 13, IsEditable = true };
            cmbArticle.Items.Add("-- Zgjidhni artikullin --");
            try
            {
                using var ctx2 = new POSDbContext();
                var articles = ctx2.Artikujt.OrderBy(a => a.Emertimi).Take(300).ToList();
                foreach (var a in articles)
                    cmbArticle.Items.Add(new ComboBoxItem
                    {
                        Content = $"{a.Emertimi} {(a.CShitjes.HasValue ? $"(€{a.CShitjes:N2})" : "")}",
                        Tag = a.Emertimi
                    });
            }
            catch { }
            cmbArticle.SelectedIndex = 0;
            Grid.SetColumn(cmbArticle, 0); pickerRow.Children.Add(cmbArticle);

            var txtQty = new TextBox { Height = 38, FontSize = 13, Text = "1", Padding = new Thickness(6, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)), BorderThickness = new Thickness(1) };
            Grid.SetColumn(txtQty, 1); pickerRow.Children.Add(txtQty);

            var btnAddItem = KdsRoundBtn("+ Shto", Color.FromRgb(34, 197, 94));
            btnAddItem.Height = 38; btnAddItem.Width = 80;
            Grid.SetColumn(btnAddItem, 3); pickerRow.Children.Add(btnAddItem);
            formStack.Children.Add(pickerRow);

            // Items list
            var itemsList = new System.Collections.Generic.List<string>();
            var lstItems = new ListBox
            {
                Height = 110, FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 4)
            };
            formStack.Children.Add(lstItems);

            var btnRemoveItem = KdsRoundBtn("✖ Fshije artikullin", Color.FromRgb(239, 68, 68));
            btnRemoveItem.HorizontalAlignment = HorizontalAlignment.Left;
            btnRemoveItem.Height = 32; btnRemoveItem.FontSize = 12; btnRemoveItem.Margin = new Thickness(0, 0, 0, 12);
            formStack.Children.Add(btnRemoveItem);

            btnAddItem.Click += (s, ev) =>
            {
                var selItem = cmbArticle.SelectedItem as ComboBoxItem;
                var articleName = selItem?.Tag?.ToString() ?? cmbArticle.Text;
                if (string.IsNullOrWhiteSpace(articleName) || articleName.StartsWith("--")) return;
                if (!int.TryParse(txtQty.Text, out var qty) || qty <= 0) qty = 1;
                var entry = $"{qty}x {articleName}";
                itemsList.Add(entry);
                lstItems.Items.Add(entry);
                txtQty.Text = "1";
            };

            btnRemoveItem.Click += (s, ev) =>
            {
                if (lstItems.SelectedItem is string sel)
                {
                    itemsList.Remove(sel);
                    lstItems.Items.Remove(sel);
                }
            };

            // Priority + Target Time row
            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row2.ColumnDefinitions.Add(new ColumnDefinition());
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row2.ColumnDefinitions.Add(new ColumnDefinition());
            var sp2L = new StackPanel(); sp2L.Children.Add(KdsLabel("Prioriteti:"));
            var cmbPriority = new ComboBox { Height = 38, FontSize = 13 };
            foreach (var p in new[] { "Normal", "High", "Urgent" }) cmbPriority.Items.Add(p);
            cmbPriority.SelectedIndex = 0;
            sp2L.Children.Add(cmbPriority);
            Grid.SetColumn(sp2L, 0); row2.Children.Add(sp2L);
            var sp2R = new StackPanel(); sp2R.Children.Add(KdsLabel("Koha (min):"));
            var txtTarget = new TextBox { Height = 38, FontSize = 13, Text = "15", Padding = new Thickness(8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)), BorderThickness = new Thickness(1) };
            sp2R.Children.Add(txtTarget);
            Grid.SetColumn(sp2R, 2); row2.Children.Add(sp2R);
            formStack.Children.Add(row2);

            // Special instructions
            formStack.Children.Add(KdsLabel("Instruksione Speciale:"));
            var txtInstructions = new TextBox
            {
                MinLines = 2, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
                FontSize = 13, Padding = new Thickness(10, 8, 10, 8), Height = 54,
                VerticalContentAlignment = VerticalAlignment.Top,
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 12)
            };
            formStack.Children.Add(txtInstructions);

            // Send to cashier checkbox
            var chkSendToCashier = new CheckBox
            {
                Content = "📤 Dërgo gjithashtu te kasa (importo nga arkëtari)",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            formStack.Children.Add(chkSendToCashier);

            scrollViewer.Content = formStack;
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

            var btnFooter = new Border
            {
                Padding = new Thickness(20, 12, 20, 12),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnSave = KdsRoundBtn("✓ Shto Porosinë", Color.FromRgb(34, 197, 94));
            btnSave.Width = 160; btnSave.Height = 42; btnSave.FontSize = 14; btnSave.FontWeight = FontWeights.Bold;
            var btnCancel = KdsRoundBtn("✗ Anulo", Color.FromRgb(100, 116, 139));
            btnCancel.Width = 100; btnCancel.Height = 42; btnCancel.FontSize = 14; btnCancel.Margin = new Thickness(10, 0, 0, 0);

            btnCancel.Click += (s, ev) => dialog.Close();
            btnSave.Click += (s, ev) =>
            {
                cmbArticle.ClearValue(System.Windows.Controls.ComboBox.BorderBrushProperty);
                cmbArticle.ClearValue(System.Windows.Controls.ComboBox.BorderThicknessProperty);
                lstItems.ClearValue(System.Windows.Controls.ListBox.BorderBrushProperty);
                lstItems.ClearValue(System.Windows.Controls.ListBox.BorderThicknessProperty);

                if (itemsList.Count == 0 && string.IsNullOrWhiteSpace(cmbArticle.Text))
                {
                    cmbArticle.BorderBrush = System.Windows.Media.Brushes.Red;
                    cmbArticle.BorderThickness = new System.Windows.Thickness(2);
                    lstItems.BorderBrush = System.Windows.Media.Brushes.Red;
                    lstItems.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Artikujt' është e detyrueshme. Shtoni të paktën një artikull.",
                        "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Build order items string
                var orderItemsText = itemsList.Count > 0
                    ? string.Join("\n", itemsList)
                    : (!string.IsNullOrWhiteSpace(cmbArticle.Text) && !cmbArticle.Text.StartsWith("--")
                        ? $"1x {cmbArticle.Text}" : "");

                if (string.IsNullOrWhiteSpace(orderItemsText))
                {
                    cmbArticle.BorderBrush = System.Windows.Media.Brushes.Red;
                    cmbArticle.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Artikujt' është e detyrueshme. Shtoni të paktën një artikull.",
                        "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Resolve table number
                string? tableNum = null;
                if (cmbTable.SelectedItem is ComboBoxItem ti && ti.Tag != null)
                    tableNum = ti.Tag.ToString();
                else if (!string.IsNullOrWhiteSpace(cmbTable.Text) && !cmbTable.Text.StartsWith("--"))
                    tableNum = cmbTable.Text.Trim();

                // Resolve customer name
                string? customerName = null;
                if (cmbClient.SelectedItem is ComboBoxItem ci && ci.Tag != null)
                    customerName = ci.Tag.ToString();
                else if (!string.IsNullOrWhiteSpace(cmbClient.Text) && !cmbClient.Text.StartsWith("--"))
                    customerName = cmbClient.Text.Trim();

                int.TryParse(txtTarget.Text, out var targetTime);
                if (targetTime <= 0) targetTime = 15;

                try
                {
                    using var ctx = new POSDbContext();
                    int nextNum = ctx.KitchenOrders.Any() ? ctx.KitchenOrders.Max(o => o.OrderNumber) + 1 : 1;
                    ctx.KitchenOrders.Add(new KitchenOrder
                    {
                        ReceiptId = 0,
                        OrderNumber = nextNum,
                        OrderType = cmbOrderType.SelectedItem?.ToString() ?? "DineIn",
                        TableNumber = tableNum,
                        CustomerName = customerName,
                        OrderItems = orderItemsText,
                        SpecialInstructions = string.IsNullOrWhiteSpace(txtInstructions.Text) ? null : txtInstructions.Text.Trim(),
                        Status = "New",
                        Priority = cmbPriority.SelectedItem?.ToString() ?? "Normal",
                        Station = cmbStation.SelectedItem?.ToString() ?? "Kitchen",
                        ReceivedAt = DateTime.Now,
                        TargetTime = targetTime,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                    ctx.SaveChanges();
                    int savedOrderNum = nextNum;
                    dialog.Close();
                    LoadOrders();

                    if (chkSendToCashier.IsChecked == true)
                    {
                        // Mark the kitchen order as ready-for-cashier using ReceiptId = -1 as sentinel
                        try
                        {
                            using var markCtx = new Database.POSDbContext();
                            var created = markCtx.KitchenOrders
                                .FirstOrDefault(o => o.OrderNumber == savedOrderNum);
                            if (created != null)
                            {
                                created.ReceiptId = -1; // sentinel: needs cashier to pick up
                                markCtx.SaveChanges();
                            }
                        }
                        catch { /* non-fatal */ }

                        MessageBox.Show(
                            $"Porosia #{savedOrderNum} u dërgua te kuzhina dhe u shënua si 'Gati për kasë'!\n\n" +
                            $"Artikujt:\n{orderItemsText}\n\n" +
                            $"Arkëtari mund ta importojë porosinë me butonin '📥 Nga Kuzhina' te kasa.",
                            "Dërguar te Kasa", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    // Build full error chain for diagnosis
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine(ex.Message);
                    var inner = ex.InnerException;
                    while (inner != null)
                    {
                        sb.AppendLine($"→ {inner.Message}");
                        inner = inner.InnerException;
                    }
                    var details = sb.ToString().Trim();
                    MessageBox.Show($"Gabim gjatë shtimit të porosisë:\n{details}\n\nNëse ky gabim vazhdon, sigurohuni që migracionet e databazës janë ekzekutuar (rinis aplikacionin).",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);
            btnFooter.Child = btnPanel;
            Grid.SetRow(btnFooter, 2);
            rootGrid.Children.Add(btnFooter);

            dialog.Content = rootGrid;
            dialog.ShowDialog();
        }

        private static Button KdsRoundBtn(string text, Color bg)
        {
            var btn = new Button
            {
                Content = text, Padding = new Thickness(12, 0, 12, 0), Height = 36,
                Background = new SolidColorBrush(bg),
                Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol")
            };
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cpFactory);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = borderFactory };
            btn.Template = template;
            return btn;
        }

        private static TextBlock KdsLabel(string text) =>
            new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };

        private static TextBox KdsTb(string text = "") =>
            new TextBox
            {
                Height = 38, FontSize = 14, Text = text, Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14)
            };

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            _clockTimer?.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            _clockTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
