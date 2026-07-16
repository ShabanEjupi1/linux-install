using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;

namespace KosovaPOS.Windows
{
    public partial class TableManagementWindow : Window
    {
        private string _currentArea = "Indoor";
        private List<RestaurantTable> _tables = new List<RestaurantTable>();

        public TableManagementWindow()
        {
            InitializeComponent();
            LoadTables();

            // Auto-refresh every 15 seconds
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(15);
            timer.Tick += (s, e) => LoadTables();
            timer.Start();
        }

        private void LoadTables()
        {
            try
            {
                using var context = new POSDbContext();

                // Load all active tables
                _tables = context.RestaurantTables
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.TableNumber)
                    .ToList();

                // If no tables exist, create default tables
                if (!_tables.Any())
                {
                    CreateDefaultTables();
                    _tables = context.RestaurantTables
                        .Where(t => t.IsActive)
                        .OrderBy(t => t.TableNumber)
                        .ToList();
                }

                DisplayTables();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Invalid object name") && ex.Message.Contains("RestaurantTables"))
                {
                    DatabaseHelper.ShowTableMissingError("RestaurantTables", "Menaxhimi i Tavolinave");
                }
                else
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të tavolinave:\n\n{ex.Message}", 
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CreateDefaultTables()
        {
            try
            {
                using var context = new POSDbContext();

                var defaultTables = new List<RestaurantTable>();

                // Indoor tables (1-12)
                for (int i = 1; i <= 12; i++)
                {
                    defaultTables.Add(new RestaurantTable
                    {
                        TableNumber = $"T{i:D2}",
                        Capacity = i <= 6 ? 4 : 6,
                        Location = "Indoor",
                        Status = "Available",
                        IsActive = true
                    });
                }

                // Outdoor tables (13-18)
                for (int i = 13; i <= 18; i++)
                {
                    defaultTables.Add(new RestaurantTable
                    {
                        TableNumber = $"T{i:D2}",
                        Capacity = 4,
                        Location = "Outdoor",
                        Status = "Available",
                        IsActive = true
                    });
                }

                // VIP tables (19-22)
                for (int i = 19; i <= 22; i++)
                {
                    defaultTables.Add(new RestaurantTable
                    {
                        TableNumber = $"V{i - 18:D2}",
                        Capacity = 8,
                        Location = "VIP",
                        Status = "Available",
                        IsActive = true
                    });
                }

                context.RestaurantTables.AddRange(defaultTables);
                context.SaveChanges();

                MessageBox.Show("Tavolinat standarde u krijuan me sukses!", 
                    "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë krijimit të tavolinave:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayTables()
        {
            TablesPanel.Children.Clear();

            var filteredTables = _tables.Where(t => t.Location == _currentArea).ToList();

            foreach (var table in filteredTables)
            {
                var button = CreateTableButton(table);
                TablesPanel.Children.Add(button);
            }
        }

        private Button CreateTableButton(RestaurantTable table)
        {
            var button = new Button
            {
                Style = (Style)FindResource("TableButton"),
                Tag = table.TableNumber,
                DataContext = table
            };

            // Set background color based on status
            var background = table.Status switch
            {
                "Available" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Green
                "Occupied" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
                "Reserved" => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Orange
                "Cleaning" => new SolidColorBrush(Color.FromRgb(148, 163, 184)), // Gray
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            button.Background = background;
            button.Foreground = Brushes.White;

            // Create content
            var stackPanel = new StackPanel();

            // Status text
            var statusText = new TextBlock
            {
                Text = GetStatusText(table.Status),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stackPanel.Children.Add(statusText);

            // Capacity
            var capacityText = new TextBlock
            {
                Text = $"\u2302 {table.Capacity} vende",
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stackPanel.Children.Add(capacityText);

            // Occupied time if applicable
            if (table.Status == "Occupied" && table.OccupiedSince.HasValue)
            {
                var duration = DateTime.Now - table.OccupiedSince.Value;
                var timeText = new TextBlock
                {
                    Text = $"\u23F1 {duration.Hours}h {duration.Minutes}m",
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stackPanel.Children.Add(timeText);
            }

            button.Content = stackPanel;
            button.Click += (s, e) => TableButton_Click(table);

            return button;
        }

        private string GetStatusText(string status)
        {
            return status switch
            {
                "Available" => "E LIRË",
                "Occupied" => "E ZËNË",
                "Reserved" => "E REZERVUAR",
                "Cleaning" => "PASTRIMI",
                _ => status.ToUpper()
            };
        }

        private void TableButton_Click(RestaurantTable table)
        {
            // Show context menu with options - constrained sizing
            var contextMenu = new System.Windows.Controls.ContextMenu
            {
                MaxWidth = 280,
                FontSize = 14,
                Padding = new Thickness(4)
            };

            var menuItemStyle = new Style(typeof(MenuItem));
            menuItemStyle.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(12, 8, 12, 8)));
            menuItemStyle.Setters.Add(new Setter(MenuItem.FontSizeProperty, 14.0));

            MenuItem CreateMenuItem(string header)
            {
                return new MenuItem { Header = header, Style = menuItemStyle };
            }

            // Edit Table
            var editMenuItem = CreateMenuItem("\u270F\uFE0F Ndrysho Tavolin\u00ebn");
            editMenuItem.Click += (s, e) => EditTable(table);
            contextMenu.Items.Add(editMenuItem);

            // Reserve Table (only if available)
            if (table.Status == "Available")
            {
                var reserveMenuItem = CreateMenuItem("\uD83D\uDCC5 Rezervo Tavolin\u00ebn");
                reserveMenuItem.Click += (s, e) => ReserveTable(table);
                contextMenu.Items.Add(reserveMenuItem);
            }

            // Mark as Occupied (only if available)
            if (table.Status == "Available")
            {
                var occupyMenuItem = CreateMenuItem("\u2705 Z\u00ebne Tavolin\u00ebn");
                occupyMenuItem.Click += (s, e) => SetTableStatus(table, "Occupied");
                contextMenu.Items.Add(occupyMenuItem);
            }

            // Clear/Free Table (if occupied or reserved)
            if (table.Status == "Occupied" || table.Status == "Reserved")
            {
                var freeMenuItem = CreateMenuItem("\u2705 Liro Tavolin\u00ebn");
                freeMenuItem.Click += (s, e) => SetTableStatus(table, "Available");
                contextMenu.Items.Add(freeMenuItem);
            }

            // Start Cleaning
            if (table.Status == "Available" || table.Status == "Occupied")
            {
                var cleanMenuItem = CreateMenuItem("\uD83E\uDDFD Pastro Tavolin\u00ebn");
                cleanMenuItem.Click += (s, e) => SetTableStatus(table, "Cleaning");
                contextMenu.Items.Add(cleanMenuItem);
            }

            // Finish Cleaning
            if (table.Status == "Cleaning")
            {
                var finishCleanMenuItem = CreateMenuItem("\u2705 P\u00ebrfundo Pastrimin");
                finishCleanMenuItem.Click += (s, e) => SetTableStatus(table, "Available");
                contextMenu.Items.Add(finishCleanMenuItem);
            }

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            // View Details
            var detailsMenuItem = CreateMenuItem("\uD83D\uDCD1 Detajet e Tavolin\u00ebs");
            detailsMenuItem.Click += (s, e) =>
            {
                var message = $"Tavolina: {table.TableNumber}\n" +
                             $"Kapaciteti: {table.Capacity} vende\n" +
                             $"Lokacioni: {table.Location}\n" +
                             $"Statusi: {GetStatusText(table.Status)}\n";

                if (table.Status == "Occupied" && table.OccupiedSince.HasValue)
                {
                    var duration = DateTime.Now - table.OccupiedSince.Value;
                    message += $"\nE z\u00ebn\u00eb q\u00eb nga: {table.OccupiedSince:HH:mm}\n";
                    message += $"Koh\u00ebzgjatja: {duration.Hours}h {duration.Minutes}m";
                    if (table.CurrentReceiptId.HasValue)
                    {
                        message += $"\nPorosia #: {table.CurrentReceiptId}";
                    }
                }

                if (table.Status == "Reserved" && table.ReservedUntil.HasValue)
                {
                    message += $"\nE rezervuar deri: {table.ReservedUntil:HH:mm dd/MM/yyyy}";
                    if (!string.IsNullOrEmpty(table.ReservedFor))
                    {
                        message += $"\nRezervar p\u00ebr: {table.ReservedFor}";
                    }
                }

                MessageBox.Show(message, $"Tavolina {table.TableNumber}", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
            contextMenu.Items.Add(detailsMenuItem);

            contextMenu.IsOpen = true;
        }
        
        private void EditTable(RestaurantTable table)
        {
            try
            {
                var dialog = new Window
                {
                    Title = $"Ndrysho Tavolin\u00ebn {table.TableNumber}",
                    Width = 420,
                    Height = 380,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var stackPanel = new StackPanel
                {
                    Margin = new Thickness(20)
                };

                // Table Number
                var lblTableNumber = new TextBlock
                {
                    Text = "Numri i Tavolinës:",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var txtTableNumber = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(8),
                    FontSize = 14,
                    Text = table.TableNumber
                };
                stackPanel.Children.Add(lblTableNumber);
                stackPanel.Children.Add(txtTableNumber);

                // Capacity
                var lblCapacity = new TextBlock
                {
                    Text = "Kapaciteti (Numri i vendeve):",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var txtCapacity = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(8),
                    FontSize = 14,
                    Text = table.Capacity.ToString()
                };
                stackPanel.Children.Add(lblCapacity);
                stackPanel.Children.Add(txtCapacity);

                // Location
                var lblLocation = new TextBlock
                {
                    Text = "Lokacioni:",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var cmbLocation = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(8),
                    FontSize = 14
                };
                cmbLocation.Items.Add("Indoor");
                cmbLocation.Items.Add("Outdoor");
                cmbLocation.Items.Add("VIP");
                cmbLocation.SelectedItem = table.Location;
                stackPanel.Children.Add(lblLocation);
                stackPanel.Children.Add(cmbLocation);

                // Buttons
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                var btnSave = new Button
                {
                    Content = "✓ RUAJ",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var btnCancel = new Button
                {
                    Content = "✗ ANULO",
                    Width = 100,
                    Height = 35,
                    Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                btnCancel.Click += (s, ev) => dialog.Close();

                btnSave.Click += (s, ev) =>
                {
                    // Validate input
                    if (string.IsNullOrWhiteSpace(txtTableNumber.Text))
                    {
                        MessageBox.Show("Ju lutem vendosni numrin e tavolinës!", "Validim", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!int.TryParse(txtCapacity.Text, out int capacity) || capacity <= 0)
                    {
                        MessageBox.Show("Ju lutem vendosni një kapacitet të vlefshëm!", "Validim", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Check if table number already exists (except this one)
                    var tableNumber = txtTableNumber.Text.Trim().ToUpper();
                    using var context = new POSDbContext();
                    if (context.RestaurantTables.Any(t => t.TableNumber == tableNumber && t.Id != table.Id))
                    {
                        MessageBox.Show($"Tavolina me numër '{tableNumber}' ekziston tashmë!", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    try
                    {
                        var dbTable = context.RestaurantTables.Find(table.Id);
                        if (dbTable != null)
                        {
                            dbTable.TableNumber = tableNumber;
                            dbTable.Capacity = capacity;
                            dbTable.Location = cmbLocation.SelectedItem?.ToString() ?? "Indoor";
                            dbTable.UpdatedAt = DateTime.Now;
                            context.SaveChanges();

                            MessageBox.Show("Tavolina u përditësua me sukses!", "Sukses", 
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            dialog.Close();
                            LoadTables();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjatë përditësimit:\n\n{ex.Message}", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                buttonPanel.Children.Add(btnSave);
                buttonPanel.Children.Add(btnCancel);
                stackPanel.Children.Add(buttonPanel);

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = stackPanel
                };
                dialog.Content = scrollViewer;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim:\n\n{ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReserveTable(RestaurantTable table)
        {
            try
            {
                var dialog = new Window
                {
                    Title = $"Rezervo Tavolin\u00ebn {table.TableNumber}",
                    Width = 460,
                    Height = 520,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
                };

                var rootGrid = new Grid();
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Header
                var headerBorder = new Border { Padding = new Thickness(20, 14, 20, 14) };
                headerBorder.Background = new LinearGradientBrush(
                    Color.FromRgb(245, 158, 11), Color.FromRgb(217, 119, 6), 0);
                headerBorder.Child = new TextBlock
                {
                    Text = $"\uD83D\uDCC5 Rezervo Tavolin\u00ebn {table.TableNumber}",
                    FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White
                };
                Grid.SetRow(headerBorder, 0);
                rootGrid.Children.Add(headerBorder);

                // Form
                var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                var stackPanel = new StackPanel { Margin = new Thickness(20, 16, 20, 10) };

                // Reserved For (Customer Name) with lookup button
                stackPanel.Children.Add(FormLabel("Emri i klientit:"));
                var customerPanel = new Grid();
                customerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                customerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var txtName = FormTextBox();
                Grid.SetColumn(txtName, 0);
                customerPanel.Children.Add(txtName);

                var btnLookup = new Button
                {
                    Content = "\uD83D\uDD0D",
                    Width = 40, Height = 36,
                    Margin = new Thickness(6, 0, 0, 12),
                    Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    Foreground = Brushes.White,
                    FontSize = 16,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "K\u00ebrko klient ekzistues"
                };
                Grid.SetColumn(btnLookup, 1);
                customerPanel.Children.Add(btnLookup);
                stackPanel.Children.Add(customerPanel);

                // Customer lookup handler
                btnLookup.Click += (s, ev) =>
                {
                    try
                    {
                        List<Customer> customers;
                        using (var ctx = new POSDbContext())
                        {
                            customers = ctx.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                        }
                        if (!customers.Any())
                        {
                            MessageBox.Show("Nuk ka klient\u00eb t\u00eb regjistruar.", "Informacion",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        var pickDlg = new Window
                        {
                            Title = "\uD83D\uDC65 Zgjidh Klientin",
                            Width = 420, Height = 380,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = dialog, ResizeMode = ResizeMode.NoResize,
                            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
                        };
                        var pickStack = new StackPanel { Margin = new Thickness(16) };
                        var searchBox = new TextBox
                        {
                            Height = 36, FontSize = 14, Padding = new Thickness(10, 0, 10, 0),
                            VerticalContentAlignment = VerticalAlignment.Center,
                            BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        pickStack.Children.Add(searchBox);
                        var listBox = new ListBox { Height = 220, FontSize = 14 };
                        void FillList(string filter)
                        {
                            listBox.Items.Clear();
                            var filtered = string.IsNullOrWhiteSpace(filter) ? customers
                                : customers.Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                    || (c.Phone != null && c.Phone.Contains(filter))).ToList();
                            foreach (var c in filtered)
                                listBox.Items.Add(new ListBoxItem
                                {
                                    Content = $"\uD83D\uDC64 {c.Name}  |  \u260E {c.Phone ?? "-"}",
                                    Tag = c
                                });
                        }
                        searchBox.TextChanged += (_, __) => FillList(searchBox.Text);
                        FillList("");
                        pickStack.Children.Add(listBox);
                        var pickBtnPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 10, 0, 0)
                        };
                        var bPick = new Button
                        {
                            Content = "\u2713 Zgjidh", Width = 100, Height = 34,
                            Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                            Foreground = Brushes.White, FontWeight = FontWeights.Bold,
                            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        bPick.Click += (_, __) =>
                        {
                            if (listBox.SelectedItem is ListBoxItem li && li.Tag is Customer sel)
                            {
                                txtName.Text = sel.Name;
                                pickDlg.Close();
                            }
                        };
                        listBox.MouseDoubleClick += (_, __) => bPick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var bPickCancel = new Button
                        {
                            Content = "Anulo", Width = 80, Height = 34,
                            Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                            Foreground = Brushes.White, BorderThickness = new Thickness(0),
                            Cursor = System.Windows.Input.Cursors.Hand
                        };
                        bPickCancel.Click += (_, __) => pickDlg.Close();
                        pickBtnPanel.Children.Add(bPick);
                        pickBtnPanel.Children.Add(bPickCancel);
                        pickStack.Children.Add(pickBtnPanel);
                        pickDlg.Content = pickStack;
                        pickDlg.ShowDialog();
                    }
                    catch { }
                };

                // Reserved Until Date
                stackPanel.Children.Add(FormLabel("Data e rezervimit:"));
                var datePicker = new DatePicker
                {
                    Margin = new Thickness(0, 0, 0, 12),
                    SelectedDate = DateTime.Today,
                    FontSize = 14
                };
                stackPanel.Children.Add(datePicker);

                // Time
                stackPanel.Children.Add(FormLabel("Ora e rezervimit:"));
                var timePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                var txtHour = new TextBox
                {
                    Width = 60, Padding = new Thickness(8), FontSize = 14,
                    Text = DateTime.Now.Hour.ToString("D2"),
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                var lblColon = new TextBlock
                {
                    Text = ":", FontSize = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 5, 0)
                };
                var txtMinute = new TextBox
                {
                    Width = 60, Padding = new Thickness(8), FontSize = 14,
                    Text = "00", Margin = new Thickness(5, 0, 0, 0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                timePanel.Children.Add(txtHour);
                timePanel.Children.Add(lblColon);
                timePanel.Children.Add(txtMinute);
                stackPanel.Children.Add(timePanel);

                // Phone
                stackPanel.Children.Add(FormLabel("Telefoni (opsional):"));
                var txtPhone = FormTextBox();
                stackPanel.Children.Add(txtPhone);

                scrollViewer.Content = stackPanel;
                Grid.SetRow(scrollViewer, 1);
                rootGrid.Children.Add(scrollViewer);

                // Buttons footer
                var footerBorder = new Border
                {
                    Padding = new Thickness(20, 12, 20, 12),
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(0, 1, 0, 0)
                };
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var btnSave = new Button
                {
                    Content = "\u2713 REZERVO",
                    Width = 120, Height = 38,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var btnCancel = new Button
                {
                    Content = "\u2717 ANULO",
                    Width = 100, Height = 38,
                    Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                btnCancel.Click += (s, ev) => dialog.Close();

                btnSave.Click += (s, ev) =>
                {
                    // Validate
                    if (string.IsNullOrWhiteSpace(txtName.Text))
                    {
                        MessageBox.Show("Ju lutem vendosni emrin e klientit!", "Validim", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!int.TryParse(txtHour.Text, out int hour) || hour < 0 || hour > 23 ||
                        !int.TryParse(txtMinute.Text, out int minute) || minute < 0 || minute > 59)
                    {
                        MessageBox.Show("Ju lutem vendosni nj\u00eb or\u00eb t\u00eb vlefshme!", "Validim", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var reservedDate = datePicker.SelectedDate ?? DateTime.Today;
                    var reservedDateTime = reservedDate.Date.AddHours(hour).AddMinutes(minute);

                    try
                    {
                        using var context = new POSDbContext();
                        var dbTable = context.RestaurantTables.Find(table.Id);
                        if (dbTable != null)
                        {
                            dbTable.Status = "Reserved";
                            dbTable.ReservedFor = txtName.Text.Trim();
                            dbTable.ReservedUntil = reservedDateTime;
                            dbTable.ReservedPhone = txtPhone.Text.Trim();
                            dbTable.UpdatedAt = DateTime.Now;
                            context.SaveChanges();

                            MessageBox.Show(
                                $"Tavolina {table.TableNumber} u rezervua me sukses!\n\n" +
                                $"Klienti: {txtName.Text.Trim()}\n" +
                                $"Data: {reservedDateTime:dd/MM/yyyy HH:mm}",
                                "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);

                            dialog.Close();
                            LoadTables();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjat\u00eb rezervimit:\n\n{ex.Message}", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                buttonPanel.Children.Add(btnSave);
                buttonPanel.Children.Add(btnCancel);
                footerBorder.Child = buttonPanel;
                Grid.SetRow(footerBorder, 2);
                rootGrid.Children.Add(footerBorder);

                dialog.Content = rootGrid;
                txtName.Focus();
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim:\n\n{ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static TextBlock FormLabel(string text) =>
            new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 4) };

        private static TextBox FormTextBox() =>
            new TextBox
            {
                Height = 36, FontSize = 14,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 12)
            };

        private void SetTableStatus(RestaurantTable table, string newStatus)
        {
            try
            {
                using var context = new POSDbContext();
                var dbTable = context.RestaurantTables.Find(table.Id);
                if (dbTable != null)
                {
                    dbTable.Status = newStatus;
                    dbTable.OccupiedSince = newStatus == "Occupied" ? DateTime.Now : null;
                    dbTable.CurrentReceiptId = newStatus == "Occupied" ? -1 : null; // -1 placeholder
                    dbTable.UpdatedAt = DateTime.Now;
                    context.SaveChanges();
                    LoadTables();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ndryshimit të statusit:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatistics()
        {
            var total = _tables.Count;
            var available = _tables.Count(t => t.Status == "Available");
            var occupied = _tables.Count(t => t.Status == "Occupied");
            var reserved = _tables.Count(t => t.Status == "Reserved");

            txtTotalTables.Text = total.ToString();
            txtAvailable.Text = available.ToString();
            txtOccupied.Text = occupied.ToString();
            txtReserved.Text = reserved.ToString();

            var occupancyRate = total > 0 ? (double)(occupied + reserved) / total * 100 : 0;
            progressOccupancy.Value = occupancyRate;
            txtOccupancyPercent.Text = $"{occupancyRate:F0}%";
        }

        private void Area_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Reset all area buttons
                btnIndoor.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                btnOutdoor.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                btnVIP.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139));

                // Highlight selected area
                button.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));

                _currentArea = button.Tag.ToString() ?? "Indoor";
                DisplayTables();
            }
        }

        private void AddTable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a simple dialog window for adding a new table
                var dialog = new Window
                {
                    Title = "Shto Tavolinë të Re",
                    Width = 400,
                    Height = 350,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var stackPanel = new StackPanel
                {
                    Margin = new Thickness(20)
                };

                // Table Number
                var lblTableNumber = new TextBlock
                {
                    Text = "Numri i Tavolinës:",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var txtTableNumber = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(8),
                    FontSize = 14
                };
                stackPanel.Children.Add(lblTableNumber);
                stackPanel.Children.Add(txtTableNumber);

                // Capacity
                var lblCapacity = new TextBlock
                {
                    Text = "Kapaciteti (Numri i vendeve):",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var txtCapacity = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(8),
                    FontSize = 14,
                    Text = "4"
                };
                stackPanel.Children.Add(lblCapacity);
                stackPanel.Children.Add(txtCapacity);

                // Location
                var lblLocation = new TextBlock
                {
                    Text = "Lokacioni:",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                var cmbLocation = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(8),
                    FontSize = 14
                };
                cmbLocation.Items.Add("Indoor");
                cmbLocation.Items.Add("Outdoor");
                cmbLocation.Items.Add("VIP");
                cmbLocation.SelectedIndex = 0;
                stackPanel.Children.Add(lblLocation);
                stackPanel.Children.Add(cmbLocation);

                // Buttons
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                var btnSave = new Button
                {
                    Content = "✓ RUAJ",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var btnCancel = new Button
                {
                    Content = "✗ ANULO",
                    Width = 100,
                    Height = 35,
                    Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                btnCancel.Click += (s, ev) => dialog.Close();

                btnSave.Click += (s, ev) =>
                {
                    // Validate input
                    if (string.IsNullOrWhiteSpace(txtTableNumber.Text))
                    {
                        MessageBox.Show("Ju lutem vendosni numrin e tavolinës!", "Validim", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!int.TryParse(txtCapacity.Text, out int capacity) || capacity <= 0)
                    {
                        MessageBox.Show("Ju lutem vendosni një kapacitet të vlefshëm!", "Validim", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Check if table number already exists
                    var tableNumber = txtTableNumber.Text.Trim().ToUpper();
                    using var context = new POSDbContext();
                    if (context.RestaurantTables.Any(t => t.TableNumber == tableNumber))
                    {
                        MessageBox.Show($"Tavolina me numër '{tableNumber}' ekziston tashmë!", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Create new table
                    var newTable = new RestaurantTable
                    {
                        TableNumber = tableNumber,
                        Capacity = capacity,
                        Location = cmbLocation.SelectedItem?.ToString() ?? "Indoor",
                        Status = "Available",
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    try
                    {
                        context.RestaurantTables.Add(newTable);
                        context.SaveChanges();

                        MessageBox.Show("Tavolina u shtua me sukses!", "Sukses", 
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        dialog.Close();
                        LoadTables();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjatë shtimit të tavolinës:\n\n{ex.Message}", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                buttonPanel.Children.Add(btnSave);
                buttonPanel.Children.Add(btnCancel);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim:\n\n{ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Reservations_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                var reservations = context.RestaurantTables
                    .Where(t => t.Status == "Reserved")
                    .OrderBy(t => t.ReservedUntil)
                    .ToList();

                if (!reservations.Any())
                {
                    MessageBox.Show("Nuk ka rezervime aktive në këtë moment!", 
                        "Rezervimet", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var message = "REZERVIMET AKTIVE:\n\n";
                foreach (var table in reservations)
                {
                    message += $"▪ Tavolina {table.TableNumber}\n";
                    message += $"  Klienti: {table.ReservedFor ?? "N/A"}\n";
                    message += $"  Data: {table.ReservedUntil:dd/MM/yyyy HH:mm}\n";
                    if (!string.IsNullOrEmpty(table.ReservedPhone))
                    {
                        message += $"  Tel: {table.ReservedPhone}\n";
                    }
                    message += "\n";
                }

                MessageBox.Show(message, "Rezervimet", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të rezervimeve:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
