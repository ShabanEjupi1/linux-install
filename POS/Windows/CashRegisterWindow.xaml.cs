using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class CashRegisterWindow : Window
    {
        private ObservableCollection<ReceiptItem> _receiptItems = new ObservableCollection<ReceiptItem>();
        private Receipt _currentReceipt;
        private ObservableCollection<Article> _searchResults = new ObservableCollection<Article>();
        
        // Enterprise features
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _metricsTimer;
        private bool _isDarkMode = false;
        private bool _isReturnMode = false;
        private ReceiptItem _numpadTargetItem;
        private Point _dragStartPoint;
        private int _activeReceiptTab = 1;
        private Dictionary<int, ObservableCollection<ReceiptItem>> _receiptTabs = new Dictionary<int, ObservableCollection<ReceiptItem>>();
        private int? _selectedCustomerId = null;
        
        // Dashboard metrics
        private decimal _todayRevenue = 0;
        private int _transactionCount = 0;
        private int _itemsSold = 0;
        
        public CashRegisterWindow()
        {
            InitializeComponent();
            InitializeWindow();
            InitializeEnterpriseFeatures();
        }
        
        private int _receiptCountSinceLastClearReminder = 0;
        private DateTime _lastClearReminderTime = DateTime.MinValue;
        private const int CLEAR_REMINDER_RECEIPT_COUNT = 50;
        private const int CLEAR_REMINDER_HOURS = 4;
        
        private void InitializeWindow()
        {
            // Load business info from environment
            BusinessNameText.Text = Environment.GetEnvironmentVariable("BUSINESS_NAME") ?? "🍕 PIZZERIA DELIZIOSO";
            CashierNumberText.Text = Environment.GetEnvironmentVariable("DEFAULT_CASHIER") ?? "01";
            CashierNameText.Text = Environment.GetEnvironmentVariable("CASHIER_NAME") ?? "Arkëtari";
            
            // Initialize new receipt
            NewReceipt();
            
            // Bind data grid
            ReceiptItemsDataGrid.ItemsSource = _receiptItems;
            SearchResultsListBox.ItemsSource = _searchResults;
            
            // Initialize receipt tabs
            _receiptTabs[1] = _receiptItems;
            
            // Focus on search box with delay
            Dispatcher.BeginInvoke(new Action(() =>
            {
                BarcodeSearchBox.Focus();
                BarcodeSearchBox.CaretIndex = BarcodeSearchBox.Text.Length;
                BarcodeSearchBox.SelectionStart = BarcodeSearchBox.Text.Length;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            _lastClearReminderTime = DateTime.Now;
        }
        
        private void InitializeEnterpriseFeatures()
        {
            // Initialize live clock
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            UpdateClock();
            
            // Initialize metrics update timer
            _metricsTimer = new DispatcherTimer();
            _metricsTimer.Interval = TimeSpan.FromMinutes(1);
            _metricsTimer.Tick += MetricsTimer_Tick;
            _metricsTimer.Start();
            UpdateDashboardMetrics();
            CheckPendingKitchenOrders();
        }
        
        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateClock();
        }
        
        private void UpdateClock()
        {
            var now = DateTime.Now;
            CurrentDateText.Text = now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("sq-AL"));
            CurrentTimeText.Text = now.ToString("HH:mm:ss");
        }
        
        private void MetricsTimer_Tick(object sender, EventArgs e)
        {
            UpdateDashboardMetrics();
            CheckPendingKitchenOrders();
        }

        private void CheckPendingKitchenOrders()
        {
            try
            {
                using var context = new POSDbContext();
                int pendingCount = context.KitchenOrders
                    .Count(o => o.ReceiptId == -1 && o.Status != "Served" && o.Status != "Cancelled");

                Dispatcher.Invoke(() =>
                {
                    if (KitchenOrdersButton != null)
                    {
                        KitchenOrdersButton.Visibility = pendingCount > 0
                            ? Visibility.Visible : Visibility.Collapsed;
                        var badge = KitchenOrdersButton.Template
                            .FindName("badgeText", KitchenOrdersButton) as System.Windows.Controls.TextBlock;
                        if (badge != null) badge.Text = pendingCount.ToString();
                    }
                });
            }
            catch { /* table might not exist yet */ }
        }

        private void ImportKitchenOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                var pendingOrders = context.KitchenOrders
                    .Where(o => o.ReceiptId == -1 && o.Status != "Served" && o.Status != "Cancelled")
                    .OrderBy(o => o.ReceivedAt)
                    .ToList();

                if (!pendingOrders.Any())
                {
                    MessageBox.Show("Nuk ka porosi nga kuzhina në pritje.",
                        "Kuzhina", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dlg = new Window
                {
                    Title = "\U0001F373 Porosi nga Kuzhina – Zgjidhni porosinë",
                    Width = 640, Height = 480,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this, ResizeMode = ResizeMode.NoResize,
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(248, 250, 252))
                };

                var root = new System.Windows.Controls.Grid();
                root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
                root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
                root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

                var hdr = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(217, 119, 6)),
                    Padding = new Thickness(20, 14, 20, 14)
                };
                hdr.Child = new System.Windows.Controls.TextBlock
                {
                    Text = "\U0001F373 Porosi nga Kuzhina",
                    FontSize = 18, FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White
                };
                System.Windows.Controls.Grid.SetRow(hdr, 0);
                root.Children.Add(hdr);

                var dg = new System.Windows.Controls.DataGrid
                {
                    AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
                    SelectionMode = System.Windows.Controls.DataGridSelectionMode.Single,
                    SelectionUnit = System.Windows.Controls.DataGridSelectionUnit.FullRow,
                    RowHeight = 42, FontSize = 13,
                    AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(248, 250, 252)),
                    Margin = new Thickness(12)
                };
                dg.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                    { Header = "#", Binding = new System.Windows.Data.Binding("OrderNumber"), Width = 60 });
                dg.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                    { Header = "Lloji", Binding = new System.Windows.Data.Binding("OrderType"), Width = 90 });
                dg.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                    { Header = "Klienti/Tavolina", Binding = new System.Windows.Data.Binding("CustomerName"), Width = 150 });
                dg.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                    { Header = "Artikujt", Binding = new System.Windows.Data.Binding("OrderItems"), Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star) });
                dg.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                    { Header = "Statusi", Binding = new System.Windows.Data.Binding("Status"), Width = 90 });
                dg.ItemsSource = pendingOrders;
                System.Windows.Controls.Grid.SetRow(dg, 1);
                root.Children.Add(dg);

                var ftr = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(12)
                };
                var btnImport = new Button
                {
                    Content = "✓ Importo Porosinë", Width = 160, Height = 38,
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(34, 197, 94)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold,
                    FontSize = 14, Cursor = System.Windows.Input.Cursors.Hand
                };
                var btnClose = new Button
                {
                    Content = "✗ Anulo", Width = 90, Height = 38, Margin = new Thickness(10, 0, 0, 0),
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(100, 116, 139)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0), FontSize = 14,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnClose.Click += (_, __) => dlg.Close();
                btnImport.Click += (_, __) =>
                {
                    if (dg.SelectedItem is not Models.KitchenOrder selected)
                    {
                        MessageBox.Show("Zgjidhni një porosi nga lista.",
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // Add the kitchen order's items to the current receipt as a note
                    var note = $"[Kuzhina #{selected.OrderNumber}] {selected.OrderItems}";
                    MessageBox.Show(
                        $"Porosia #{selected.OrderNumber} u importua!\n\nArtikujt:\n{selected.OrderItems}\n\n" +
                        "Ju lutem shtoni artikujt manualisht te kasa dhe procesoni pagesën.",
                        "Importuar nga Kuzhina", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Mark as served in kitchen
                    try
                    {
                        using var ctx = new POSDbContext();
                        var kitchenOrder = ctx.KitchenOrders.Find(selected.Id);
                        if (kitchenOrder != null)
                        {
                            kitchenOrder.ReceiptId = 0; // clear sentinel
                            kitchenOrder.Status = "Served";
                            kitchenOrder.ServedAt = DateTime.Now;
                            ctx.SaveChanges();
                        }
                    }
                    catch { }

                    dlg.Close();
                    CheckPendingKitchenOrders();
                };
                ftr.Children.Add(btnImport);
                ftr.Children.Add(btnClose);
                System.Windows.Controls.Grid.SetRow(ftr, 2);
                root.Children.Add(ftr);
                dlg.Content = root;
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të porosive:\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateDashboardMetrics()
        {
            try
            {
                using var context = new POSDbContext();
                var today = DateTime.Now.Date;
                var tomorrow = today.AddDays(1);
                
                // SQL Server mode - use DitariD with Include for sales data
                // Must include DitariH to access Data property (Data is NotMapped in DitariD)
                var todaySales = context.DitariD
                    .Include(d => d.DitariH)
                    .Where(d => d.DitariH != null && d.DitariH.Data.HasValue && 
                                d.DitariH.Data.Value >= today && d.DitariH.Data.Value < tomorrow)
                    .ToList();
                
                _todayRevenue = (decimal)todaySales.Sum(d => d.VleraMeTvsh ?? 0);
                // Use DitariHID (the actual column) instead of Numri (NotMapped property)
                _transactionCount = todaySales.Select(d => d.DitariHID).Distinct().Count();
                _itemsSold = (int)todaySales.Sum(d => d.Sasia ?? 0);
                
                // Animate counter updates
                AnimateCounter(TodayRevenueText, $"{_todayRevenue:N2} €");
                AnimateCounter(TransactionCountText, _transactionCount.ToString());
                AnimateCounter(ItemsSoldText, _itemsSold.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating metrics: {ex.Message}");
            }
        }
        
        private void AnimateCounter(TextBlock textBlock, string newValue)
        {
            if (textBlock.Text == newValue) return;
            
            var animation = new DoubleAnimation(1, 0.5, TimeSpan.FromMilliseconds(100));
            animation.Completed += (s, e) =>
            {
                textBlock.Text = newValue;
                var fadeIn = new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(100));
                textBlock.BeginAnimation(TextBlock.OpacityProperty, fadeIn);
            };
            textBlock.BeginAnimation(TextBlock.OpacityProperty, animation);
        }
        
        private void CheckClearArticleReminder()
        {
            _receiptCountSinceLastClearReminder++;
            
            bool shouldRemind = false;
            string reason = "";
            
            if (_receiptCountSinceLastClearReminder >= CLEAR_REMINDER_RECEIPT_COUNT)
            {
                shouldRemind = true;
                reason = $"Keni printuar {_receiptCountSinceLastClearReminder} fatura.";
            }
            else if ((DateTime.Now - _lastClearReminderTime).TotalHours >= CLEAR_REMINDER_HOURS)
            {
                shouldRemind = true;
                reason = $"Kanë kaluar {CLEAR_REMINDER_HOURS} orë.";
            }
            
            if (shouldRemind)
            {
                var result = MessageBox.Show(
                    $"💡 SUGJERIM: {reason}\n\n" +
                    "Është mirë të pastroni memorien e printerit fiskal për performancë më të mirë.\n\n" +
                    "Dëshironi të pastroni tani?",
                    "Sugjerim - Pastro Artikujt",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                
                if (result == MessageBoxResult.Yes)
                {
                    ClearCacheButton_Click(this, new RoutedEventArgs());
                }
                
                _receiptCountSinceLastClearReminder = 0;
                _lastClearReminderTime = DateTime.Now;
            }
        }
        
        private void BarcodeSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            BarcodeSearchBox.CaretIndex = BarcodeSearchBox.Text.Length;
            BarcodeSearchBox.SelectionStart = BarcodeSearchBox.Text.Length;
        }
        
        private void BarcodeSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Keep placeholder behavior on focus lost
        }
        
        // Quick Quantity Box Handlers for easier quantity entry
        private void QuickQuantityBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numbers and decimal point
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[\d\.,]$");
        }
        
        private void QuickQuantityBox_GotFocus(object sender, RoutedEventArgs e)
        {
            QuickQuantityBox.SelectAll();
        }
        
        private decimal GetQuickQuantity()
        {
            if (decimal.TryParse(QuickQuantityBox.Text.Replace(",", "."), 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, 
                out decimal qty) && qty > 0)
            {
                return qty;
            }
            return 1;
        }
        
        private void ResetQuickQuantity()
        {
            QuickQuantityBox.Text = "1";
        }
        
        private void NewReceipt()
        {
            _selectedCustomerId = null;
            _currentReceipt = new Receipt
            {
                ReceiptNumber = GenerateReceiptNumber(),
                Date = DateTime.Now,
                CashierNumber = CashierNumberText.Text,
                CashierName = CashierNameText.Text,
                BuyerName = BuyerNameTextBox.Text
            };

            _receiptItems.Clear();
            UpdateTotals();
        }
        
        private string GenerateReceiptNumber()
        {
            var cashierNumber = CashierNumberText.Text;
            var sequenceNumber = GetNextSequenceNumber();
            return $"{cashierNumber}-KO{sequenceNumber:D6}";
        }
        
        private int GetNextSequenceNumber()
        {
            try
            {
                using var context = new POSDbContext();
                
                // SQL Server mode - use DitariH table for sequence (header contains receipt number)
                // Use Id instead of Numri (Numri is NotMapped alias for Id)
                // Check if table exists and has data
                if (context.DitariH.Any())
                {
                    var lastId = context.DitariH.Max(d => d.Id);
                    return (int)lastId + 1;
                }
                
                return 1; // Start from 1 if table is empty
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting next sequence number: {ex.Message}");
                // Fallback: use timestamp-based sequence
                return (int)(DateTime.Now.Ticks % 1000000);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // KEYBOARD SHORTCUTS HANDLER
        // ═══════════════════════════════════════════════════════════════════════════════
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!IsLoaded) return;
            
            // Don't handle keys when DataGrid cell is being edited
            if (ReceiptItemsDataGrid.IsKeyboardFocusWithin && 
                (ReceiptItemsDataGrid.CurrentCell.Column != null) &&
                !e.Key.ToString().StartsWith("F") && e.Key != Key.Escape && e.Key != Key.Delete)
            {
                return;
            }
            
            // Don't handle keys when text boxes are focused (allow normal text entry)
            if ((BuyerNameTextBox.IsFocused || RemarkTextBox.IsFocused || PaidAmountTextBox.IsFocused) &&
                !e.Key.ToString().StartsWith("F") && e.Key != Key.Escape)
            {
                return;
            }
            
            // Show keyboard shortcuts overlay with ? key
            if (e.Key == Key.OemQuestion && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                ShortcutsOverlay.Visibility = ShortcutsOverlay.Visibility == Visibility.Visible 
                    ? Visibility.Collapsed : Visibility.Visible;
                e.Handled = true;
                return;
            }
            
            switch (e.Key)
            {
                case Key.F5:
                    PrintReceiptButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F6:
                    CopyButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F3:
                    ClearRowButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F10:
                    e.Handled = true;
                    DiscardReceiptButton_Click(this, new RoutedEventArgs());
                    break;
                case Key.F7:
                    CashPaymentRadio.IsChecked = true;
                    e.Handled = true;
                    break;
                case Key.F8:
                    CardPaymentRadio.IsChecked = true;
                    e.Handled = true;
                    break;
                case Key.F9:
                    FiscalReceiptCheck.IsChecked = !FiscalReceiptCheck.IsChecked;
                    e.Handled = true;
                    break;
                case Key.F11:
                    SimpleReceiptCheck.IsChecked = !SimpleReceiptCheck.IsChecked;
                    e.Handled = true;
                    break;
                case Key.F12:
                    WaybillReceiptCheck.IsChecked = !WaybillReceiptCheck.IsChecked;
                    e.Handled = true;
                    break;
                case Key.F4:
                    ApplyDiscountButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (ReceiptItemsDataGrid.SelectedItem != null)
                    {
                        ClearRowButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    if (ShortcutsOverlay.Visibility == Visibility.Visible)
                    {
                        ShortcutsOverlay.Visibility = Visibility.Collapsed;
                    }
                    else if (SearchResultsPopup.IsOpen)
                    {
                        SearchResultsPopup.IsOpen = false;
                    }
                    else if (NumpadPopup.IsOpen)
                    {
                        NumpadPopup.IsOpen = false;
                    }
                    else
                    {
                        BarcodeSearchBox.Clear();
                        BarcodeSearchBox.Focus();
                    }
                    e.Handled = true;
                    break;
            }
            
            // Handle Ctrl key combinations
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        ZReportButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.X:
                        XReportButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.S:
                        SoldArticlesButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.F:
                        BarcodeSearchBox.Focus();
                        BarcodeSearchBox.SelectAll();
                        e.Handled = true;
                        break;
                    case Key.K:
                        CustomerLookupButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.T:
                        AddReceiptTabButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                }
            }
            
            // Redirect numeric keys to search box
            if (!BarcodeSearchBox.IsFocused && 
                !ReceiptItemsDataGrid.IsKeyboardFocusWithin &&
                !PaidAmountTextBox.IsFocused &&
                !BuyerNameTextBox.IsFocused &&
                !RemarkTextBox.IsFocused &&
                ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)))
            {
                BarcodeSearchBox.Focus();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // ENTERPRISE FEATURE HANDLERS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var themeService = Services.ThemeService.Instance;
            themeService.ToggleTheme();
            _isDarkMode = themeService.IsDarkMode;
            
            // Update the icon to reflect the current theme
            UpdateThemeIcon();
        }
        
        private void UpdateThemeIcon()
        {
            // Find the icon TextBlock inside the ThemeToggleButton
            // The icon shows moon for light mode (click to go dark) and sun for dark mode (click to go light)
            if (ThemeToggleButton.Template?.FindName("icon", ThemeToggleButton) is System.Windows.Controls.TextBlock iconText)
            {
                iconText.Text = _isDarkMode ? "☀️" : "🌙";
            }
        }
        
        private void ShortcutsOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            ShortcutsOverlay.Visibility = Visibility.Collapsed;
        }
        
        private void ReturnModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isReturnMode = !_isReturnMode;
            
            if (_isReturnMode)
            {
                ReturnModeIndicator.Visibility = Visibility.Visible;
                ReturnModeButton.Content = "✗ Anulo Kthimin";
                ReturnModeButton.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Light red background
                ReturnModeButton.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red text
                
                // Change header gradient to red for return mode
                // This would require modifying the header border background
                MessageBox.Show(
                    "↩️ Modaliteti i KTHIMIT/RIMBURSIMIT është aktivizuar.\n\n" +
                    "Artikujt e shtuar do të trajtohen si kthime.\n" +
                    "Kliko përsëri butonin për të dalë nga ky modalitet.",
                    "Kthim/Rimbursim", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ReturnModeIndicator.Visibility = Visibility.Collapsed;
                ReturnModeButton.Content = "↩️ Kthim";
                ReturnModeButton.Background = new SolidColorBrush(Colors.Transparent);
                ReturnModeButton.Foreground = new SolidColorBrush(Color.FromRgb(79, 70, 229)); // Primary color
            }
        }
        
        private void AddReceiptTabButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if we already have 5 tabs
            if (_receiptTabs.Count >= 5)
            {
                MessageBox.Show("Maksimumi i faturave të hapura është 5.", "Kufi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Find the next available tab number (1-5)
            int newTabNumber = 1;
            for (int i = 1; i <= 5; i++)
            {
                if (!_receiptTabs.ContainsKey(i))
                {
                    newTabNumber = i;
                    break;
                }
            }
            
            // Save current receipt items
            _receiptTabs[_activeReceiptTab] = new ObservableCollection<ReceiptItem>(_receiptItems);
            
            // Create new tab
            _receiptTabs[newTabNumber] = new ObservableCollection<ReceiptItem>();
            _activeReceiptTab = newTabNumber;
            _receiptItems = _receiptTabs[newTabNumber];
            ReceiptItemsDataGrid.ItemsSource = _receiptItems;
            
            NewReceipt();
            UpdateReceiptTabs();
        }
        
        private void ReceiptTab_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int tabNumber))
            {
                SwitchToReceiptTab(tabNumber);
            }
        }
        
        private void SwitchToReceiptTab(int tabNumber)
        {
            if (tabNumber == _activeReceiptTab) return;
            
            // Save current
            _receiptTabs[_activeReceiptTab] = new ObservableCollection<ReceiptItem>(_receiptItems);
            
            // Switch to new
            _activeReceiptTab = tabNumber;
            _receiptItems = _receiptTabs[tabNumber];
            ReceiptItemsDataGrid.ItemsSource = _receiptItems;
            
            UpdateTotals();
            UpdateReceiptTabs();
        }
        
        private void UpdateReceiptTabs()
        {
            // Clear existing dynamic tabs (keep only the panel structure)
            var itemsToRemove = ReceiptTabsPanel.Children.OfType<Border>()
                .Where(b => b != Receipt1Tab && b.Name != "Receipt1Tab")
                .ToList();
            
            foreach (var item in itemsToRemove)
            {
                ReceiptTabsPanel.Children.Remove(item);
            }
            
            // Update Receipt1Tab visibility and styling
            if (_receiptTabs.ContainsKey(1))
            {
                Receipt1Tab.Visibility = Visibility.Visible;
                Receipt1Tab.Background = _activeReceiptTab == 1 
                    ? new SolidColorBrush(Color.FromRgb(79, 70, 229)) 
                    : new SolidColorBrush(Color.FromRgb(241, 245, 249));
                
                var textBlocks = Receipt1Tab.Child as StackPanel;
                if (textBlocks != null)
                {
                    foreach (var child in textBlocks.Children.OfType<TextBlock>())
                    {
                        if (child.Text != "🧾")
                            child.Foreground = _activeReceiptTab == 1 
                                ? new SolidColorBrush(Colors.White) 
                                : new SolidColorBrush(Color.FromRgb(71, 85, 105));
                    }
                }
            }
            else
            {
                Receipt1Tab.Visibility = Visibility.Collapsed;
            }
            
            // Add dynamic tabs for other open receipts (2-5)
            int insertIndex = ReceiptTabsPanel.Children.IndexOf(Receipt1Tab) + 1;
            
            foreach (var tabNumber in _receiptTabs.Keys.OrderBy(k => k).Where(k => k > 1))
            {
                var tabBorder = new Border
                {
                    Background = _activeReceiptTab == tabNumber 
                        ? new SolidColorBrush(Color.FromRgb(79, 70, 229)) 
                        : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 8, 16, 8),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    Tag = tabNumber.ToString()
                };
                tabBorder.MouseLeftButtonDown += ReceiptTab_Click;
                
                var tabContent = new StackPanel { Orientation = Orientation.Horizontal };
                tabContent.Children.Add(new TextBlock { Text = "🧾", Margin = new Thickness(0, 0, 6, 0) });
                tabContent.Children.Add(new TextBlock 
                { 
                    Text = $"Fatura {tabNumber}", 
                    Foreground = _activeReceiptTab == tabNumber 
                        ? new SolidColorBrush(Colors.White) 
                        : new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13
                });
                
                // Add close button for non-active tabs
                var closeBtn = new TextBlock 
                { 
                    Text = "×", 
                    Margin = new Thickness(8, 0, 0, 0),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = _activeReceiptTab == tabNumber 
                        ? new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                        : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Cursor = Cursors.Hand,
                    Tag = tabNumber
                };
                closeBtn.MouseLeftButtonDown += CloseReceiptTab_Click;
                tabContent.Children.Add(closeBtn);
                
                tabBorder.Child = tabContent;
                ReceiptTabsPanel.Children.Insert(insertIndex, tabBorder);
                insertIndex++;
            }
        }
        
        private void CloseReceiptTab_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent tab click from firing
            
            if (sender is TextBlock closeBtn && closeBtn.Tag is int tabNumber)
            {
                // Check if there are items in this tab
                if (_receiptTabs.ContainsKey(tabNumber) && _receiptTabs[tabNumber].Count > 0)
                {
                    var result = MessageBox.Show(
                        $"Fatura {tabNumber} ka artikuj. A doni ta mbyllni?", 
                        "Konfirmimi", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes) return;
                }
                
                // Remove the tab
                _receiptTabs.Remove(tabNumber);
                
                // If we closed the active tab, switch to another
                if (_activeReceiptTab == tabNumber)
                {
                    var nextTab = _receiptTabs.Keys.OrderBy(k => k).FirstOrDefault();
                    if (nextTab > 0)
                    {
                        _activeReceiptTab = nextTab;
                        _receiptItems = _receiptTabs[nextTab];
                        ReceiptItemsDataGrid.ItemsSource = _receiptItems;
                        UpdateTotals();
                    }
                    else
                    {
                        // No tabs left, create tab 1
                        _receiptTabs[1] = new ObservableCollection<ReceiptItem>();
                        _activeReceiptTab = 1;
                        _receiptItems = _receiptTabs[1];
                        ReceiptItemsDataGrid.ItemsSource = _receiptItems;
                        NewReceipt();
                    }
                }
                
                UpdateReceiptTabs();
            }
        }
        
        private void CustomerLookupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<Customer> customers;
                using (var ctx = new POSDbContext())
                {
                    customers = ctx.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                }

                var lookupWindow = new Window
                {
                    Title = "\u2302 Zgjidh Klientin",
                    Width = 620,
                    Height = 560,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
                };

                var rootGrid = new Grid();
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Header
                var hdrBorder = new Border { Padding = new Thickness(20, 16, 20, 16) };
                hdrBorder.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(3, 105, 161), 0);
                hdrBorder.Child = new TextBlock { Text = "\u2302 Klient\u00ebt e Regjistruar", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                Grid.SetRow(hdrBorder, 0);
                rootGrid.Children.Add(hdrBorder);

                // Search
                var searchBox = new TextBox
                {
                    Height = 40, FontSize = 14, Padding = new Thickness(12, 0, 12, 0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                    BorderThickness = new Thickness(1), Margin = new Thickness(16, 12, 16, 8)
                };
                Grid.SetRow(searchBox, 1);
                rootGrid.Children.Add(searchBox);

                // Customer list
                var listBox = new ListBox
                {
                    FontSize = 14,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(16, 0, 16, 8)
                };

                void RefreshList(string filter)
                {
                    listBox.Items.Clear();
                    var filtered = string.IsNullOrWhiteSpace(filter)
                        ? customers
                        : customers.Where(c =>
                            c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            (c.Phone != null && c.Phone.Contains(filter)) ||
                            (c.LoyaltyCardNumber != null && c.LoyaltyCardNumber.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        ).ToList();

                    foreach (var c in filtered)
                    {
                        var item = new ListBoxItem
                        {
                            Tag = c,
                            Padding = new Thickness(12, 8, 12, 8)
                        };
                        var panel = new StackPanel { Orientation = Orientation.Horizontal };
                        panel.Children.Add(new TextBlock { Text = "\u2302 ", FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
                        var info = new StackPanel();
                        info.Children.Add(new TextBlock { Text = c.Name, FontWeight = FontWeights.SemiBold, FontSize = 14 });
                        info.Children.Add(new TextBlock
                        {
                            Text = $"\u260E {c.Phone ?? "-"}  |  \u2605 {c.LoyaltyPoints} pika  |  {c.Tier}",
                            FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
                        });
                        panel.Children.Add(info);
                        item.Content = panel;
                        listBox.Items.Add(item);
                    }
                }

                searchBox.TextChanged += (s, ev) => RefreshList(searchBox.Text);
                RefreshList(string.Empty);

                Grid.SetRow(listBox, 2);
                rootGrid.Children.Add(listBox);

                // Buttons
                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(16, 8, 16, 16)
                };
                var btnSelect = new Button
                {
                    Content = "✓ Zgjidh Klientin", Width = 170, Height = 40,
                    Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                    Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14,
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0)
                };
                var btnManual = new Button
                {
                    Content = "✏️ Klient Manual", Width = 150, Height = 40,
                    Background = new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                    Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14,
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0)
                };
                var btnClose = new Button
                {
                    Content = "✗ Anulo", Width = 100, Height = 40,
                    Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14,
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand
                };

                btnClose.Click += (s, ev) => lookupWindow.Close();
                btnManual.Click += (s, ev) =>
                {
                    var manualDlg = new Window
                    {
                        Title = "Klient Manual", Width = 340, Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = lookupWindow, ResizeMode = ResizeMode.NoResize
                    };
                    var sp = new StackPanel { Margin = new Thickness(20) };
                    sp.Children.Add(new TextBlock { Text = "Emri i klientit:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
                    var tbManual = new TextBox { Height = 36, FontSize = 14, Padding = new Thickness(8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
                    sp.Children.Add(tbManual);
                    var manualBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
                    var bOk = new Button { Content = "✓ OK", Width = 80, Height = 34, Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0) };
                    var bCn = new Button { Content = "✗ Anulo", Width = 80, Height = 34, Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
                    bCn.Click += (_, __) => manualDlg.Close();
                    bOk.Click += (_, __) =>
                    {
                        if (!string.IsNullOrWhiteSpace(tbManual.Text))
                        {
                            BuyerNameTextBox.Text = tbManual.Text.Trim();
                            manualDlg.Close();
                            lookupWindow.Close();
                        }
                    };
                    manualBtnPanel.Children.Add(bOk);
                    manualBtnPanel.Children.Add(bCn);
                    sp.Children.Add(manualBtnPanel);
                    manualDlg.Content = sp;
                    tbManual.Focus();
                    manualDlg.ShowDialog();
                };
                btnSelect.Click += (s, ev) =>
                {
                    if (listBox.SelectedItem is ListBoxItem item && item.Tag is Customer customer)
                    {
                        BuyerNameTextBox.Text = customer.Name;
                        _selectedCustomerId = customer.Id;
                        lookupWindow.Close();
                        MessageBox.Show(
                            $"\u2705 Klienti u zgjodh:\n\n{customer.Name}\n{customer.Phone ?? "N/A"}\n\u2B50 {customer.LoyaltyPoints} pika besnik\u00ebrie\nNiveli: {customer.Tier}",
                            "Klienti u Zgjodh", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Ju lutem zgjidhni një klient nga lista.",
                            "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
                listBox.MouseDoubleClick += (s, ev) => btnSelect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                btnPanel.Children.Add(btnSelect);
                btnPanel.Children.Add(btnManual);
                btnPanel.Children.Add(btnClose);
                Grid.SetRow(btnPanel, 3);
                rootGrid.Children.Add(btnPanel);

                lookupWindow.Content = rootGrid;
                lookupWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                // If Customers table doesn't exist yet, fall back to a simple manual entry dialog
                var fallbackDlg = new Window
                {
                    Title = "Klienti", Width = 340, Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this, ResizeMode = ResizeMode.NoResize
                };
                var sp2 = new StackPanel { Margin = new Thickness(20) };
                sp2.Children.Add(new TextBlock { Text = "Emri i klientit:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
                var tbFallback = new TextBox { Height = 36, FontSize = 14, Padding = new Thickness(8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
                sp2.Children.Add(tbFallback);
                var fbPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
                var bOk2 = new Button { Content = "✓ OK", Width = 80, Height = 34, Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0) };
                var bCn2 = new Button { Content = "✗ Anulo", Width = 80, Height = 34, Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
                bOk2.Click += (_, __) => { if (!string.IsNullOrWhiteSpace(tbFallback.Text)) BuyerNameTextBox.Text = tbFallback.Text.Trim(); fallbackDlg.Close(); };
                bCn2.Click += (_, __) => fallbackDlg.Close();
                fbPanel.Children.Add(bOk2);
                fbPanel.Children.Add(bCn2);
                sp2.Children.Add(fbPanel);
                fallbackDlg.Content = sp2;
                tbFallback.Focus();
                fallbackDlg.ShowDialog();
            }
        }
        
        private void AddFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funksioni i favoritve do të aktivizohet së shpejti.", "Favorites", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SendToKitchen(long receiptId, List<ReceiptItem> items, string? buyerName)
        {
            try
            {
                using var ctx = new POSDbContext();
                int nextNum = ctx.KitchenOrders.Any() ? ctx.KitchenOrders.Max(o => o.OrderNumber) + 1 : 1;

                // Build items summary
                var itemsSummary = string.Join("\n", items.Select(i => $"× {i.Quantity:F0}  {i.ArticleName}"));

                // Determine station from item names
                string station = "Kitchen";
                if (items.Any(i => i.ArticleName?.ToLower().Contains("piz") == true ||
                                   i.ArticleName?.ToLower().Contains("topping") == true))
                    station = "Pizza";
                else if (items.Any(i => i.ArticleName?.ToLower().Contains("grill") == true ||
                                        i.ArticleName?.ToLower().Contains("biftek") == true ||
                                        i.ArticleName?.ToLower().Contains("qofte") == true))
                    station = "Grill";
                else if (items.Any(i => i.ArticleName?.ToLower().Contains("pij") == true ||
                                        i.ArticleName?.ToLower().Contains("drink") == true ||
                                        i.ArticleName?.ToLower().Contains("kafe") == true))
                    station = "Drinks";

                ctx.KitchenOrders.Add(new KitchenOrder
                {
                    ReceiptId = (int)receiptId,
                    OrderNumber = nextNum,
                    OrderType = "DineIn",
                    CustomerName = (buyerName == "Qytetar" || string.IsNullOrWhiteSpace(buyerName)) ? null : buyerName,
                    OrderItems = itemsSummary,
                    SpecialInstructions = RemarkTextBox?.Text,
                    Status = "New",
                    Priority = "Normal",
                    Station = station,
                    ReceivedAt = DateTime.Now,
                    TargetTime = 15
                });
                ctx.SaveChanges();
            }
            catch
            {
                // Kitchen Display is an optional feature — silently continue if table is missing
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // NUMPAD HANDLERS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void QuantityDisplay_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is ReceiptItem item)
            {
                _numpadTargetItem = item;
                NumpadInput.Text = item.Quantity.ToString("F2");
                NumpadPopup.PlacementTarget = border;
                NumpadPopup.IsOpen = true;
                NumpadInput.Focus();
                NumpadInput.SelectAll();
            }
        }
        
        private void NumpadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                NumpadInput.Text += btn.Content.ToString();
            }
        }
        
        private void NumpadBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (NumpadInput.Text.Length > 0)
            {
                NumpadInput.Text = NumpadInput.Text.Substring(0, NumpadInput.Text.Length - 1);
            }
        }
        
        private void NumpadCancel_Click(object sender, RoutedEventArgs e)
        {
            NumpadPopup.IsOpen = false;
            _numpadTargetItem = null;
        }
        
        private void NumpadConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (_numpadTargetItem != null && decimal.TryParse(NumpadInput.Text, out decimal quantity))
            {
                if (quantity <= 0)
                {
                    var result = MessageBox.Show(
                        $"Dëshironi të fshini '{_numpadTargetItem.ArticleName}' nga fatura?",
                        "Konfirmoni fshirjen",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        _receiptItems.Remove(_numpadTargetItem);
                    }
                }
                else
                {
                    _numpadTargetItem.Quantity = quantity;
                }
                UpdateTotals();
            }
            
            NumpadPopup.IsOpen = false;
            _numpadTargetItem = null;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // PRICE OVERRIDE HANDLERS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void PriceOverrideCancel_Click(object sender, RoutedEventArgs e)
        {
            PriceOverridePopup.IsOpen = false;
            ManagerPinBox.Clear();
        }
        
        private void PriceOverrideConfirm_Click(object sender, RoutedEventArgs e)
        {
            // Verify manager PIN (simplified - in production use secure PIN storage)
            if (ManagerPinBox.Password == "1234")
            {
                MessageBox.Show("Autorizimi u pranua. Mund të ndryshoni çmimin.", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                PriceOverridePopup.IsOpen = false;
            }
            else
            {
                MessageBox.Show("PIN i gabuar. Provoni përsëri.", "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            ManagerPinBox.Clear();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // DRAG AND DROP HANDLERS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void ReceiptItemsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }
        
        private void ReceiptItemsDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;
                
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var dataGrid = sender as DataGrid;
                    var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
                    
                    if (row != null && dataGrid != null)
                    {
                        var item = row.Item as ReceiptItem;
                        if (item != null)
                        {
                            DataObject dragData = new DataObject("ReceiptItem", item);
                            DragDrop.DoDragDrop(dataGrid, dragData, DragDropEffects.Move);
                        }
                    }
                }
            }
        }
        
        private void ReceiptItemsDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ReceiptItem"))
            {
                var droppedItem = e.Data.GetData("ReceiptItem") as ReceiptItem;
                var target = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
                
                if (target != null && droppedItem != null)
                {
                    var targetItem = target.Item as ReceiptItem;
                    if (targetItem != null && droppedItem != targetItem)
                    {
                        int oldIndex = _receiptItems.IndexOf(droppedItem);
                        int newIndex = _receiptItems.IndexOf(targetItem);
                        
                        if (oldIndex != -1 && newIndex != -1)
                        {
                            _receiptItems.Move(oldIndex, newIndex);
                        }
                    }
                }
            }
        }
        
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // CHANGE DIALOG - Show change to return to customer prominently
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void ShowChangeDialog(decimal changeAmount, decimal paidAmount, decimal totalAmount)
        {
            // Create a modal dialog to show the change prominently
            var changeWindow = new Window
            {
                Title = "💰 Kusur për klientin",
                Width = 450,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true
            };
            
            var mainBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(30),
                Margin = new Thickness(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 5,
                    Direction = 270,
                    BlurRadius = 20,
                    Opacity = 0.3
                }
            };
            
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            
            // Header
            var header = new TextBlock
            {
                Text = "💰 KUSUR",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stack.Children.Add(header);
            
            // Change amount - very prominent
            var changeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(30, 20, 30, 20),
                Margin = new Thickness(0, 0, 0, 20)
            };
            var changeText = new TextBlock
            {
                Text = $"€ {changeAmount:N2}",
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            changeBorder.Child = changeText;
            stack.Children.Add(changeBorder);
            
            // Details
            var detailsGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.RowDefinitions.Add(new RowDefinition());
            detailsGrid.RowDefinitions.Add(new RowDefinition());
            
            var totalLabel = new TextBlock { Text = "Totali:", FontSize = 14, Foreground = Brushes.Gray };
            var totalValue = new TextBlock { Text = $"€ {totalAmount:N2}", FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
            var paidLabel = new TextBlock { Text = "Paguar:", FontSize = 14, Foreground = Brushes.Gray };
            var paidValue = new TextBlock { Text = $"€ {paidAmount:N2}", FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
            
            Grid.SetRow(totalLabel, 0); Grid.SetColumn(totalLabel, 0);
            Grid.SetRow(totalValue, 0); Grid.SetColumn(totalValue, 1);
            Grid.SetRow(paidLabel, 1); Grid.SetColumn(paidLabel, 0);
            Grid.SetRow(paidValue, 1); Grid.SetColumn(paidValue, 1);
            
            detailsGrid.Children.Add(totalLabel);
            detailsGrid.Children.Add(totalValue);
            detailsGrid.Children.Add(paidLabel);
            detailsGrid.Children.Add(paidValue);
            stack.Children.Add(detailsGrid);
            
            // OK Button
            var okButton = new Button
            {
                Content = "✓ Në rregull",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                Padding = new Thickness(30, 12, 30, 12),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            okButton.Template = CreateRoundedButtonTemplate();
            okButton.Click += (s, e) => changeWindow.Close();
            stack.Children.Add(okButton);
            
            mainBorder.Child = stack;
            changeWindow.Content = mainBorder;
            
            // Auto-close after 5 seconds
            var autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            autoCloseTimer.Tick += (s, e) =>
            {
                autoCloseTimer.Stop();
                if (changeWindow.IsVisible)
                    changeWindow.Close();
            };
            autoCloseTimer.Start();
            
            changeWindow.ShowDialog();
        }
        
        private ControlTemplate CreateRoundedButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            return template;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // SUCCESS ANIMATION
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void ShowSuccessAnimation(string receiptNumber)
        {
            SuccessReceiptNumber.Text = $"Fatura: {receiptNumber}";
            SuccessOverlay.Visibility = Visibility.Visible;
            
            // Auto-hide after 1.5 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            timer.Tick += (s, e) =>
            {
                SuccessOverlay.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // RECEIPT ITEM MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════════
        
        public void AddArticleToReceipt(Article article, decimal quantity = -1)
        {
            // If quantity is -1 (default), use the quick quantity box value
            if (quantity < 0)
            {
                quantity = GetQuickQuantity();
            }
            
            var existingItem = _receiptItems.FirstOrDefault(i => i.ArticleId == article.Id);
            
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var item = new ReceiptItem
                {
                    ArticleId = article.Id,
                    Article = article,
                    Barcode = article.Barcode,
                    ArticleName = article.Name,
                    PLU = article.PLU ?? (article.Id + 1000),
                    Quantity = _isReturnMode ? -quantity : quantity,
                    Price = article.SalesPrice,
                    VATRate = 0,
                    DiscountPercent = 0,
                    DiscountValue = 0
                };
                
                item.PropertyChanged += ReceiptItem_PropertyChanged;
                _receiptItems.Add(item);
            }
            
            UpdateTotals();
            ResetQuickQuantity(); // Reset after adding
            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                BarcodeSearchBox.Focus();
                BarcodeSearchBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        
        private void ReceiptItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateTotals();
        }
        
        private void UpdateTotals()
        {
            var total = _receiptItems.Sum(i => i.TotalValue);
            var tax = _receiptItems.Sum(i => i.VATValue);
            var itemCount = _receiptItems.Count;
            
            TotalDisplayText.Text = total.ToString("F2");
            TaxDisplayText.Text = $"TVSH: {tax:F2} €";
            ItemCountText.Text = $"{itemCount} artikuj";
            
            UpdateLeftAmount();
        }
        
        private void PaidAmountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateLeftAmount();
        }
        
        private void UpdateLeftAmount()
        {
            if (PaidAmountTextBox == null || TotalDisplayText == null || LeftAmountTextBox == null)
                return;
                
            if (decimal.TryParse(PaidAmountTextBox.Text, out var paid))
            {
                // Remove euro symbol and extra spaces from total text
                var totalText = TotalDisplayText.Text?.Replace("€", "").Replace(" ", "").Trim() ?? "0";
                if (decimal.TryParse(totalText, out var total))
                {
                    var change = paid - total;
                    LeftAmountTextBox.Text = change.ToString("F2");
                    
                    // Update background color based on change value
                    if (change > 0)
                    {
                        // Positive change - show in green
                        LeftAmountTextBox.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Light green
                        LeftAmountTextBox.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)); // Dark green
                    }
                    else if (change < 0)
                    {
                        // Negative - show in red (customer owes)
                        LeftAmountTextBox.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Light red
                        LeftAmountTextBox.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28)); // Dark red
                    }
                    else
                    {
                        // Zero change - neutral
                        LeftAmountTextBox.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Original light yellow
                        LeftAmountTextBox.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)); // Original brown
                    }
                }
            }
        }
        
        private void PaymentMethodChanged(object sender, RoutedEventArgs e)
        {
            if (CardPaymentRadio != null && CardPaymentRadio.IsChecked == true)
            {
                if (decimal.TryParse(TotalDisplayText?.Text, out var total))
                {
                    PaidAmountTextBox.Text = total.ToString("F2");
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // MAIN RECEIPT OPERATIONS
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void PrintReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_receiptItems.Count == 0)
            {
                MessageBox.Show("Nuk ka artikuj në faturë!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var total = decimal.Parse(TotalDisplayText.Text);
            
            var paymentWindow = new QuickPaymentWindow(total);
            if (paymentWindow.ShowDialog() != true)
            {
                return;
            }
            
            PaidAmountTextBox.Text = paymentWindow.CashReceived.ToString("F2");
            UpdateLeftAmount();
            
            try
            {
                _currentReceipt.BuyerName = BuyerNameTextBox.Text;
                _currentReceipt.CustomerId = _selectedCustomerId;
                _currentReceipt.Remark = RemarkTextBox.Text;
                _currentReceipt.Items = _receiptItems.ToList();
                _currentReceipt.TotalAmount = total;
                _currentReceipt.PaidAmount = paymentWindow.CashReceived;
                _currentReceipt.LeftAmount = decimal.Parse(LeftAmountTextBox.Text);
                _currentReceipt.PaymentMethod = CashPaymentRadio.IsChecked == true ? "Para në dorë" : "Kartë";
                _currentReceipt.TaxAmount = _receiptItems.Sum(i => i.VATValue);

                // Support multiple receipt types - collect all selected types
                var selectedReceiptTypes = new List<ReceiptType>();
                
                if (FiscalReceiptCheck.IsChecked == true)
                    selectedReceiptTypes.Add(ReceiptType.Fiscal);
                if (SimpleReceiptCheck.IsChecked == true)
                    selectedReceiptTypes.Add(ReceiptType.Simple);
                if (WaybillReceiptCheck.IsChecked == true)
                    selectedReceiptTypes.Add(ReceiptType.Waybill);
                
                // Default to fiscal if nothing selected
                if (selectedReceiptTypes.Count == 0)
                    selectedReceiptTypes.Add(ReceiptType.Fiscal);
                
                // Set primary receipt type (first selected)
                _currentReceipt.ReceiptType = selectedReceiptTypes[0];
                
                // Store additional types for multi-print
                var printAllTypes = selectedReceiptTypes;
                
                string? fiscalFilePath = null;
                bool fiscalSuccess = false;
                bool printingAttempted = false;
                
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fiscal_debug.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] PrintReceiptButton_Click started - Types: {string.Join(", ", selectedReceiptTypes)}\n");
                
                // Print fiscal receipt if selected
                if (printAllTypes.Contains(ReceiptType.Fiscal))
                {
                    var fiscalEnabledEnv = Environment.GetEnvironmentVariable("FISCAL_ENABLED");
                    var isFiscal = bool.Parse(fiscalEnabledEnv ?? "false");
                    
                    if (isFiscal)
                    {
                        try
                        {
                            printingAttempted = true;
                            var fiscalService = ServiceLocator.Instance.FiscalPrinter;
                            fiscalFilePath = fiscalService.GenerateFiscalReceipt(_currentReceipt);
                            fiscalSuccess = fiscalService.SendToFiscalPrinterAndWait(fiscalFilePath, out string fiscalError);
                            
                            if (fiscalSuccess)
                            {
                                _currentReceipt.IsFiscal = true;
                                _currentReceipt.FiscalFilePath = fiscalFilePath;
                            }
                            else
                            {
                                var result = MessageBox.Show(
                                    $"⚠️ Printeri fiskal nuk mund të printojë faturën!\n\n" +
                                    $"Detaje: {fiscalError}\n\n" +
                                    "Zgjidhni:\n" +
                                    "• [Po] - Ruaj faturën pa printim\n" +
                                    "• [Jo] - Anulo dhe provo përsëri",
                                    "Gabim Printimi Fiskal", 
                                    MessageBoxButton.YesNo, 
                                    MessageBoxImage.Warning);
                                
                                if (result == MessageBoxResult.No)
                                {
                                    return;
                                }
                                
                                _currentReceipt.IsFiscal = false;
                                _currentReceipt.FiscalFilePath = "PENDING: " + fiscalError;
                            }
                        }
                        catch (Exception ex)
                        {
                            var result = MessageBox.Show(
                                $"❌ Gabim në krijimin e skedarit fiskal!\n\n" +
                                $"Detaje: {ex.Message}\n\n" +
                                "Zgjidhni:\n" +
                                "• [Po] - Ruaj faturën pa printim\n" +
                                "• [Jo] - Anulo dhe provo përsëri", 
                                "Gabim Fiskal", 
                                MessageBoxButton.YesNo, 
                                MessageBoxImage.Error);
                            
                            if (result == MessageBoxResult.No)
                            {
                                return;
                            }
                            
                            _currentReceipt.IsFiscal = false;
                            _currentReceipt.FiscalFilePath = "FAILED: " + ex.Message;
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "⚠️ Printeri fiskal nuk është i aktivizuar!\n\n" +
                            "Fatura do të ruhet por nuk do të printohet.",
                            "Paralajmërim", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Warning);
                        
                        _currentReceipt.IsFiscal = false;
                    }
                }
                else
                {
                    _currentReceipt.IsFiscal = false;
                    printingAttempted = true;
                }
                
                // Save to database
                using (var context = new POSDbContext())
                {
                    // SQL Server mode - save to DitariD table
                    var receiptNumber = GetNextSequenceNumber();
                    _currentReceipt.ReceiptNumber = receiptNumber.ToString();
                    
                    // First create header
                    var ditariH = new Models.BMDData.DitariH
                    {
                        Data = DateTime.Now,
                        KlientiID = null,
                        Klienti = "Klient i përgjithshëm",
                        ArkaID = 1,
                        Arka = "Arka 1",
                        Shuma = (double)_currentReceipt.Items.Sum(i => i.TotalValue - i.VATValue),
                        Zbritja = (double)_currentReceipt.Items.Sum(i => i.DiscountValue),
                        Tatimi = (double)_currentReceipt.Items.Sum(i => i.VATValue),
                        Totali = (double)_currentReceipt.TotalAmount,
                        Paguar = (double)_currentReceipt.PaidAmount,
                        Mbetur = 0,
                        PunetoriID = null,
                        Punetori = _currentReceipt.CashierName,
                        Verejtje = _currentReceipt.Remark,
                        Filiala = 1,
                        TipiPageses = _currentReceipt.PaymentMethod
                    };
                    context.DitariH.Add(ditariH);
                    context.SaveChanges(); // Get ID
                    
                    // Now add detail records
                    foreach (var item in _currentReceipt.Items)
                    {
                        var ditariD = new Models.BMDData.DitariD
                        {
                            DitariHID = ditariH.Id,
                            ArtikujID = item.ArticleId,
                            Barkodi = item.Barcode,
                            Emertimi = item.ArticleName,
                            Sasia = (double)item.Quantity,
                            Cmimi = (double)item.Price,
                            Zbritja = (double)item.DiscountValue,
                            Tatimi = (double)item.VATValue,
                            Totali = (double)item.TotalValue,
                            Vat = (double)item.VATRate
                        };
                        context.DitariD.Add(ditariD);
                    }
                    
                    // Update stock in Artikujt table
                    foreach (var item in _currentReceipt.Items)
                    {
                        var artikull = context.Artikujt.FirstOrDefault(a => a.Barkodi == item.Barcode || a.Id == item.ArticleId);
                        if (artikull != null)
                        {
                            artikull.Sasia = (artikull.Sasia ?? 0) - (double)item.Quantity;
                            // Create low stock alert if stock falls below minimum
                            var newQty = (decimal)(artikull.Sasia ?? 0);
                            var minQty = (decimal)(artikull.StoguMinimal ?? 5);
                            if (newQty <= minQty)
                            {
                                try
                                {
                                    var alertType = newQty <= 0 ? "OutOfStock" : "LowStock";
                                    var exists = context.StockAlerts.Any(a =>
                                        a.ArticleId == (int)artikull.Id && a.Status == "Active" && a.AlertType == alertType);
                                    if (!exists)
                                    {
                                        context.StockAlerts.Add(new Models.StockAlert
                                        {
                                            ArticleId = (int)artikull.Id,
                                            ArticleName = artikull.Emertimi ?? item.ArticleName,
                                            AlertType = alertType,
                                            CurrentQuantity = newQty,
                                            ThresholdQuantity = minQty,
                                            MinimumQuantity = minQty,
                                            Message = newQty <= 0
                                                ? $"Artikulli '{artikull.Emertimi}' ka mbaruar stokun!"
                                                : $"Artikulli '{artikull.Emertimi}' ka stok të ulët: {newQty:N0}",
                                            Status = "Active",
                                            CreatedAt = DateTime.Now
                                        });
                                    }
                                }
                                catch { /* StockAlerts table may not exist yet */ }
                            }
                        }
                    }
                    
                    context.SaveChanges();
                    _currentReceipt.Id = receiptNumber;

                    // Silently push order to Kitchen Display System
                    SendToKitchen(ditariH.Id, _currentReceipt.Items, _currentReceipt.BuyerName);
                }
                
                // Print non-fiscal receipt if needed (Simple or Waybill)
                bool nonFiscalSuccess = false;
                if (printAllTypes.Contains(ReceiptType.Simple) || printAllTypes.Contains(ReceiptType.Waybill))
                {
                    try
                    {
                        var receiptService = ServiceLocator.Instance.ReceiptPrinter;
                        
                        // Print simple receipt if selected
                        if (printAllTypes.Contains(ReceiptType.Simple))
                        {
                            _currentReceipt.ReceiptType = ReceiptType.Simple;
                            receiptService.PrintNonFiscalReceipt(_currentReceipt);
                        }
                        
                        // Print waybill if selected
                        if (printAllTypes.Contains(ReceiptType.Waybill))
                        {
                            _currentReceipt.ReceiptType = ReceiptType.Waybill;
                            receiptService.PrintNonFiscalReceipt(_currentReceipt);
                        }
                        
                        nonFiscalSuccess = true;
                    }
                    catch (Exception printEx)
                    {
                        var result = MessageBox.Show(
                            $"⚠️ Gabim gjatë printimit të faturës!\n\n" +
                            $"Detaje: {printEx.Message}\n\n" +
                            "Fatura u ruajt në databazë por nuk u printua.",
                            "Gabim Printimi",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    
                    // Restore primary receipt type
                    _currentReceipt.ReceiptType = selectedReceiptTypes[0];
                }
                
                _currentReceipt.IsPrinted = fiscalSuccess || nonFiscalSuccess;
                
                // Show change amount prominently with a popup dialog
                var changeAmount = _currentReceipt.PaidAmount - _currentReceipt.TotalAmount;
                if (changeAmount > 0)
                {
                    LeftAmountTextBox.Text = changeAmount.ToString("F2");
                    LeftAmountTextBox.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Light green
                    
                    // Show a prominent change popup
                    ShowChangeDialog(changeAmount, _currentReceipt.PaidAmount, _currentReceipt.TotalAmount);
                }
                else if (changeAmount == 0)
                {
                    LeftAmountTextBox.Text = "0.00";
                    LeftAmountTextBox.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Yellow for exact
                    
                    // Show dialog for exact payment too
                    ShowChangeDialog(0, _currentReceipt.PaidAmount, _currentReceipt.TotalAmount);
                }
                else
                {
                    // Negative change means customer didn't pay enough (credit sale or partial payment)
                    LeftAmountTextBox.Text = Math.Abs(changeAmount).ToString("F2");
                    LeftAmountTextBox.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Light red for remaining
                    
                    // Show what's remaining to pay
                    MessageBox.Show(
                        $"⚠️ Klienti nuk ka paguar shumën e plotë!\n\n" +
                        $"Totali: € {_currentReceipt.TotalAmount:N2}\n" +
                        $"Paguar: € {_currentReceipt.PaidAmount:N2}\n" +
                        $"Mbetur: € {Math.Abs(changeAmount):N2}",
                        "Pagesa e pjesshme",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                
                // Show success animation
                ShowSuccessAnimation(_currentReceipt.ReceiptNumber);
                
                // Update dashboard metrics
                UpdateDashboardMetrics();
                
                // Start new receipt (delay a bit so user can see the change)
                var savedReceiptNumber = _currentReceipt.ReceiptNumber;
                
                // Keep the change displayed for 3 seconds before clearing
                var changeDisplayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                changeDisplayTimer.Tick += (s, args) =>
                {
                    changeDisplayTimer.Stop();
                    NewReceipt();
                    BuyerNameTextBox.Text = "Qytetar";
                    RemarkTextBox.Text = "";
                    PaidAmountTextBox.Text = "0.00";
                    LeftAmountTextBox.Background = Brushes.Transparent;
                };
                changeDisplayTimer.Start();
                
                CheckClearArticleReminder();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit të faturës:\n{ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                
                var today = DateTime.Now.Date;

                // SQL Server mode - filter by DitariH.Data (mapped), then get related DitariD
                var todayHeaderIds = context.DitariH
                    .Where(h => h.Data.HasValue && h.Data >= today)
                    .Select(h => h.Id)
                    .ToList();
                var ditariD = context.DitariD
                    .Include(d => d.DitariH)
                    .Where(d => d.DitariHID.HasValue && todayHeaderIds.Contains(d.DitariHID.Value))
                    .ToList();
                
                var receiptGroups = ditariD
                    .GroupBy(d => new { d.Numri, d.Data })
                    .Take(20)
                    .Select(g => new
                    {
                        Id = g.First().Id,
                        ReceiptNumber = g.Key.Numri?.ToString() ?? $"R-{g.First().Id}",
                        Date = g.Key.Data ?? DateTime.Now,
                        BuyerName = g.First().Verejtje ?? "Qytetar",
                        ItemCount = g.Count(),
                        TotalAmount = g.Sum(d => d.VleraMeTvsh ?? 0),
                        ReceiptTypeName = "Fiskal",
                        PrintedStatus = "✅",
                        Items = g.ToList()
                    })
                    .ToList();
                
                if (receiptGroups.Count == 0)
                {
                    MessageBox.Show("Nuk ka fatura të printuara sot!", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                ShowRecentReceiptsWindow(receiptGroups);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ShowRecentReceiptsWindow(dynamic receiptData)
        {
            var receiptsWindow = new Window
            {
                Title = "📋 Faturat e Fundit",
                Width = 950,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
            };
            
            var mainBorder = new Border
            {
                Margin = new Thickness(20),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20)
            };
            mainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 4,
                BlurRadius = 16,
                Opacity = 0.1
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var titleText = new TextBlock
            {
                Text = "📋 Faturat e Fundit - Sot",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);
            
            var listView = new ListView
            {
                Margin = new Thickness(0, 0, 0, 15),
                FontSize = 14,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240))
            };
            
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn { Header = "Nr. Faturës", DisplayMemberBinding = new System.Windows.Data.Binding("ReceiptNumber"), Width = 140 });
            gridView.Columns.Add(new GridViewColumn { Header = "Ora", DisplayMemberBinding = new System.Windows.Data.Binding("Date") { StringFormat = "HH:mm:ss" }, Width = 80 });
            gridView.Columns.Add(new GridViewColumn { Header = "Blerësi", DisplayMemberBinding = new System.Windows.Data.Binding("BuyerName"), Width = 140 });
            gridView.Columns.Add(new GridViewColumn { Header = "Artikuj", DisplayMemberBinding = new System.Windows.Data.Binding("ItemCount"), Width = 70 });
            gridView.Columns.Add(new GridViewColumn { Header = "Totali (€)", DisplayMemberBinding = new System.Windows.Data.Binding("TotalAmount") { StringFormat = "N2" }, Width = 100 });
            gridView.Columns.Add(new GridViewColumn { Header = "Lloji", DisplayMemberBinding = new System.Windows.Data.Binding("ReceiptTypeName"), Width = 100 });
            gridView.Columns.Add(new GridViewColumn { Header = "Printuar", DisplayMemberBinding = new System.Windows.Data.Binding("PrintedStatus"), Width = 80 });
            
            listView.View = gridView;
            listView.ItemsSource = receiptData;
            
            Grid.SetRow(listView, 1);
            grid.Children.Add(listView);
            
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            var loadButton = new Button
            {
                Content = "📥 Ngarko në Faturë",
                Width = 160,
                Height = 44,
                Margin = new Thickness(0, 0, 12, 0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            loadButton.Click += (s, args) =>
            {
                if (listView.SelectedItem != null)
                {
                    dynamic selected = listView.SelectedItem;
                    
                    NewReceipt();
                    
                    // Load from DitariD items (SQL Server)
                    foreach (var item in selected.Items)
                    {
                        var article = ArticleDataService.FindByBarcode(item.Barkodi ?? "");
                        if (article != null)
                        {
                            var newItem = new ReceiptItem
                            {
                                ArticleId = article.Id,
                                Barcode = item.Barkodi ?? "",
                                ArticleName = item.Artikulli ?? article.Name,
                                PLU = article.PLU ?? (article.Id + 1000),
                                Quantity = (decimal)(item.Sasia ?? 1),
                                Price = (decimal)(item.QmimiShumices ?? item.Qmimi ?? article.SalesPrice),
                                VATRate = (decimal)(item.Vat ?? 0),
                                DiscountPercent = 0,
                                DiscountValue = 0
                            };
                            newItem.PropertyChanged += ReceiptItem_PropertyChanged;
                            _receiptItems.Add(newItem);
                        }
                    }
                    
                    UpdateTotals();
                    receiptsWindow.Close();
                }
                else
                {
                    MessageBox.Show("Zgjidhni një faturë!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            
            var closeButton = new Button
            {
                Content = "Mbyll",
                Width = 100,
                Height = 44,
                FontSize = 14,
                Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            closeButton.Click += (s, args) => receiptsWindow.Close();
            
            buttonPanel.Children.Add(loadButton);
            buttonPanel.Children.Add(closeButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);
            
            mainBorder.Child = grid;
            receiptsWindow.Content = mainBorder;
            receiptsWindow.ShowDialog();
        }
        
        private void ClearRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReceiptItemsDataGrid.SelectedItem is ReceiptItem selectedItem)
            {
                _receiptItems.Remove(selectedItem);
                UpdateTotals();
            }
            else
            {
                MessageBox.Show("Zgjidhni një rresht për ta fshirë!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void ApplyDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            var userSession = UserSessionService.Instance;
            if (!userSession.CanGiveDiscounts)
            {
                MessageBox.Show("Nuk keni të drejta për të aplikuar zbritje!", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (ReceiptItemsDataGrid.SelectedItem is ReceiptItem selectedItem)
            {
                var discountWindow = new DiscountWindow(
                    selectedItem.ArticleName,
                    selectedItem.Price * selectedItem.Quantity,
                    selectedItem.DiscountPercent
                );
                
                if (discountWindow.ShowDialog() == true)
                {
                    if (discountWindow.DiscountPercent > userSession.MaxDiscountPercent)
                    {
                        MessageBox.Show($"Zbritja maksimale e lejuar është {userSession.MaxDiscountPercent:F1}%.", 
                            "Zbritje e pa-lejuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    selectedItem.DiscountPercent = discountWindow.DiscountPercent;
                    UpdateTotals();
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për të aplikuar zbritje!", 
                    "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void DiscardReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_receiptItems.Count > 0)
            {
                var result = MessageBox.Show("A jeni i sigurt që doni ta anuloni faturën?", 
                    "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    NewReceipt();
                    BuyerNameTextBox.Text = "Qytetar";
                    RemarkTextBox.Text = "";
                    PaidAmountTextBox.Text = "0.00";
                }
            }
        }
        
        private void ZReportButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("A jeni i sigurt që doni të gjeneroni Z-Raportin?\nKjo do të mbyllë ditën fiskale!", 
                "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                var fiscalService = ServiceLocator.Instance.FiscalPrinter;
                var filePath = fiscalService.GenerateZReport();
                fiscalService.SendToFiscalPrinter(filePath);
            }
        }
        
        private void XReportButton_Click(object sender, RoutedEventArgs e)
        {
            var fiscalService = ServiceLocator.Instance.FiscalPrinter;
            var filePath = fiscalService.GenerateXReport();
            fiscalService.SendToFiscalPrinter(filePath);
        }
        
        private void SoldArticlesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                
                var today = DateTime.Now.Date;
                var tomorrow = today.AddDays(1);
                
                // SQL Server mode - filter by DitariH.Data (mapped), then get related DitariD
                var soldHeaderIds = context.DitariH
                    .Where(h => h.Data.HasValue && h.Data >= today && h.Data < tomorrow)
                    .Select(h => h.Id)
                    .ToList();
                var ditariD = context.DitariD
                    .Include(d => d.DitariH)
                    .Where(d => d.DitariHID.HasValue && soldHeaderIds.Contains(d.DitariHID.Value))
                    .ToList();
                
                var soldArticles = ditariD
                    .GroupBy(d => new { d.ArtikullId, d.Artikulli, d.Barkodi, Price = d.QmimiShumices ?? d.Qmimi ?? 0 })
                    .Select(g => new
                    {
                        Barcode = g.Key.Barkodi ?? "",
                        ArticleName = g.Key.Artikulli ?? "Artikull",
                        Quantity = g.Sum(d => d.Sasia ?? 0),
                        Price = g.Key.Price,
                        TotalValue = g.Sum(d => d.VleraMeTvsh ?? 0),
                        TransactionCount = g.Select(d => d.Numri).Distinct().Count()
                    })
                    .OrderByDescending(x => x.TotalValue)
                    .ToList();
                
                if (soldArticles.Count == 0)
                {
                    MessageBox.Show("Nuk ka shitje për ditën e sotme!", "Informacion",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var soldWindow = new Window
                {
                    Title = "📊 Raporti i Artikujve të Shitur",
                    Width = 1000,
                    Height = 650,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
                };
                
                var mainBorder = new Border
                {
                    Margin = new Thickness(20),
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(20)
                };
                
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var headerPanel = new StackPanel();
                headerPanel.Children.Add(new TextBlock
                {
                    Text = "📊 Artikujt e Shitur Sot",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
                });
                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"Data: {today:dd/MM/yyyy} | Artikuj: {soldArticles.Count}",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Margin = new Thickness(0, 5, 0, 20)
                });
                Grid.SetRow(headerPanel, 0);
                grid.Children.Add(headerPanel);
                
                var dataGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    IsReadOnly = true,
                    CanUserAddRows = false,
                    FontSize = 14,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240))
                };
                
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Barkodi", Binding = new System.Windows.Data.Binding("Barcode"), Width = 120 });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Artikulli", Binding = new System.Windows.Data.Binding("ArticleName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Sasia", Binding = new System.Windows.Data.Binding("Quantity") { StringFormat = "N2" }, Width = 100 });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Çmimi", Binding = new System.Windows.Data.Binding("Price") { StringFormat = "N2" }, Width = 100 });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Vlera", Binding = new System.Windows.Data.Binding("TotalValue") { StringFormat = "N2" }, Width = 120 });
                
                dataGrid.ItemsSource = soldArticles;
                Grid.SetRow(dataGrid, 1);
                grid.Children.Add(dataGrid);
                
                decimal totalQuantity = soldArticles.Sum(x => (decimal)x.Quantity);
                decimal totalValue = soldArticles.Sum(x => (decimal)x.TotalValue);
                
                var summaryPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 16, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                summaryPanel.Child = new TextBlock
                {
                    Text = $"TOTALI: {totalQuantity:N2} artikuj | {totalValue:N2} €",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105))
                };
                Grid.SetRow(summaryPanel, 2);
                grid.Children.Add(summaryPanel);
                
                var closeButton = new Button
                {
                    Content = "Mbyll",
                    Width = 120,
                    Height = 44,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0),
                    Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0)
                };
                closeButton.Click += (s, args) => soldWindow.Close();
                Grid.SetRow(closeButton, 3);
                grid.Children.Add(closeButton);
                
                mainBorder.Child = grid;
                soldWindow.Content = mainBorder;
                soldWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "⚠️ Kjo do të pastrojë artikujt nga memoria e printerit fiskal!\n\n" +
                "A jeni i sigurt?",
                "Konfirmimi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var tempPath = @"C:\Temp\ClearArticle.inp";
                    var command = "O,1,______,_,__;ALL";
                    
                    var dir = System.IO.Path.GetDirectoryName(tempPath);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    
                    System.IO.File.WriteAllText(tempPath, command);
                    
                    try
                    {
                        var fiscalService = ServiceLocator.Instance.FiscalPrinter;
                        var filePath = fiscalService.ClearPrinterCache();
                        fiscalService.SendToFiscalPrinter(filePath);
                    }
                    catch { }
                    
                    MessageBox.Show("✅ Komanda u dërgua!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle shortcuts overlay
            ShortcutsOverlay.Visibility = ShortcutsOverlay.Visibility == Visibility.Visible 
                ? Visibility.Collapsed : Visibility.Visible;
        }
        
        private void ReceiptItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        
        private void ReceiptItemsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateTotals()), DispatcherPriority.Background);
        }
        
        private void ReceiptItemsDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e) { }
        
        private void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ReceiptItem item)
            {
                item.Quantity += 1;
                UpdateTotals();
            }
        }
        
        private void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ReceiptItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity -= 1;
                    UpdateTotals();
                }
                else
                {
                    var result = MessageBox.Show(
                        $"Dëshironi të fshini '{item.ArticleName}'?",
                        "Konfirmoni",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        _receiptItems.Remove(item);
                        UpdateTotals();
                    }
                }
            }
        }
        
        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ReceiptItem item)
            {
                _receiptItems.Remove(item);
                UpdateTotals();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // PRODUCT SEARCH
        // ═══════════════════════════════════════════════════════════════════════════════
        
        private void BarcodeSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (SearchResultsPopup.IsOpen && SearchResultsListBox.SelectedItem is Article selectedArticle)
                {
                    _isNavigatingWithKeyboard = false;
                    AddArticleToReceipt(selectedArticle);
                    BarcodeSearchBox.Clear();
                    SearchResultsPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }
                SearchAndAddProduct();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && SearchResultsPopup.IsOpen && SearchResultsListBox.Items.Count > 0)
            {
                _isNavigatingWithKeyboard = true;
                if (SearchResultsListBox.SelectedIndex < SearchResultsListBox.Items.Count - 1)
                    SearchResultsListBox.SelectedIndex++;
                else
                    SearchResultsListBox.SelectedIndex = 0;
                SearchResultsListBox.ScrollIntoView(SearchResultsListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && SearchResultsPopup.IsOpen && SearchResultsListBox.Items.Count > 0)
            {
                _isNavigatingWithKeyboard = true;
                if (SearchResultsListBox.SelectedIndex > 0)
                    SearchResultsListBox.SelectedIndex--;
                else
                    SearchResultsListBox.SelectedIndex = SearchResultsListBox.Items.Count - 1;
                SearchResultsListBox.ScrollIntoView(SearchResultsListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && SearchResultsPopup.IsOpen)
            {
                SearchResultsPopup.IsOpen = false;
                _isNavigatingWithKeyboard = false;
                e.Handled = true;
            }
        }
        
        private void BarcodeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _isNavigatingWithKeyboard = false;
            var searchText = BarcodeSearchBox.Text.Trim();
            
            if (string.IsNullOrEmpty(searchText))
            {
                SearchResultsPopup.IsOpen = false;
                return;
            }
            
            if (searchText.Length >= 2)
            {
                PerformSearch(searchText);
            }
        }
        
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchAndAddProduct();
        }
        
        private bool _isNavigatingWithKeyboard = false;
        
        private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isNavigatingWithKeyboard) return;
                
            if (SearchResultsListBox.SelectedItem is Article selectedArticle)
            {
                AddArticleToReceipt(selectedArticle);
                BarcodeSearchBox.Clear();
                SearchResultsPopup.IsOpen = false;
                BarcodeSearchBox.Focus();
            }
        }
        
        private void SearchResultsListBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is Article selectedArticle)
            {
                AddArticleToReceipt(selectedArticle);
                BarcodeSearchBox.Clear();
                SearchResultsPopup.IsOpen = false;
                BarcodeSearchBox.Focus();
            }
        }
        
        private void PerformSearch(string searchText)
        {
            try
            {
                // Use ArticleDataService for SQL Server/SQLite compatibility
                var results = ArticleDataService.SearchArticles(searchText, 15);
                
                _searchResults.Clear();
                foreach (var article in results)
                {
                    _searchResults.Add(article);
                }
                
                SearchResultsPopup.IsOpen = _searchResults.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SearchAndAddProduct()
        {
            var searchText = BarcodeSearchBox.Text.Trim();
            
            if (string.IsNullOrEmpty(searchText)) return;
            
            try
            {
                // Use ArticleDataService for SQL Server/SQLite compatibility
                var article = ArticleDataService.FindByBarcode(searchText);
                
                if (article != null)
                {
                    AddArticleToReceipt(article);
                    BarcodeSearchBox.Clear();
                    SearchResultsPopup.IsOpen = false;
                }
                else
                {
                    PerformSearch(searchText);
                    
                    if (_searchResults.Count == 1)
                    {
                        AddArticleToReceipt(_searchResults[0]);
                        BarcodeSearchBox.Clear();
                        SearchResultsPopup.IsOpen = false;
                    }
                    else if (_searchResults.Count == 0)
                    {
                        MessageBox.Show($"Nuk u gjet: {searchText}", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
