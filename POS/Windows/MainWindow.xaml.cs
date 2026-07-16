using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KosovaPOS.Database;
using KosovaPOS.Services;
using KosovaPOS.Models.BMDData;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        
        // Track open windows to prevent duplicates
        private static Dictionary<string, Window> _openWindows = new Dictionary<string, Window>();
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeWindow();
            InitializeTheme();
        }
        
        private void InitializeWindow()
        {
            // Load business name from environment
            var businessName = Environment.GetEnvironmentVariable("BUSINESS_NAME");
            if (!string.IsNullOrEmpty(businessName))
            {
                BusinessNameText.Text = businessName.ToUpper();
            }

            // Start clock
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            UpdateDateTime();

            // Kitchen ready badge update timer (every 15 seconds)
            var kitchenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            kitchenTimer.Tick += (s, e) => UpdateKitchenReadyBadge();
            kitchenTimer.Start();
            UpdateKitchenReadyBadge();

            // Add keyboard shortcuts for quick navigation
            this.KeyDown += MainWindow_KeyDown;
        }

        private void UpdateKitchenReadyBadge()
        {
            try
            {
                using var ctx = new POSDbContext();
                int readyCount = ctx.KitchenOrders
                    .Count(o => o.Status == "Ready" && o.ReceiptId == -1);
                if (KitchenReadyBadge != null)
                {
                    KitchenReadyBadge.Visibility = readyCount > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                    if (KitchenReadyCount != null) KitchenReadyCount.Text = readyCount.ToString();
                }
            }
            catch { /* KitchenOrders table may not exist yet */ }
        }
        
        private void InitializeTheme()
        {
            // Apply the current theme on startup
            ThemeService.Instance.ApplyTheme();
            UpdateThemeButton();
            
            // Subscribe to theme changes
            ThemeService.Instance.ThemeChanged += (s, isDark) => UpdateThemeButton();
        }
        
        private void UpdateThemeButton()
        {
            if (ThemeService.Instance.IsDarkMode)
            {
                ThemeToggleButton.Content = "\u2600\uFE0F Tema e Drit\u00EBs";
                ThemeToggleButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 193, 7)); // Yellow for light mode option
            }
            else
            {
                ThemeToggleButton.Content = "\U0001F319 Tema e Err\u00EBt";
                ThemeToggleButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(30, 64, 175)); // Blue for dark mode option
            }
        }
        
        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.Instance.ToggleTheme();
        }
        
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ensure window is fully loaded
            if (!IsLoaded) return;
            
            // Ctrl+T for theme toggle
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.T)
            {
                ThemeService.Instance.ToggleTheme();
                e.Handled = true;
                return;
            }
            
            // Quick access keyboard shortcuts
            switch (e.Key)
            {
                case Key.F1:
                    CashRegister_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F2:
                    PizzaManagement_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F3:
                    Sales_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F4:
                    Purchases_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F5:
                    BusinessPartners_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F6:
                    Reports_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F7:
                    Analytics_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F8:
                    Settings_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.F9:
                    Finance_Click(this, null!);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    // Ask to exit on Escape
                    Exit_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        
        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateDateTime();
        }
        
        private void UpdateDateTime()
        {
            DateTimeText.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy - HH:mm:ss");
        }
        
        private void ShowOrFocusWindow<T>(string windowKey, Func<T> createWindow, bool allowMultiple = true) where T : Window
        {
            // Check if window is already open
            if (_openWindows.TryGetValue(windowKey, out var existingWindow))
            {
                // Check if window is still valid and usable (not closed)
                try
                {
                    if (existingWindow != null && existingWindow.IsLoaded && existingWindow.IsVisible)
                    {
                        // Bring existing window to front
                        existingWindow.Activate();
                        existingWindow.Focus();
                        
                        // If minimized, restore it
                        if (existingWindow.WindowState == WindowState.Minimized)
                        {
                            existingWindow.WindowState = WindowState.Normal;
                        }
                        
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Window was closed, proceed to remove and create new
                }
                
                // Window was closed or invalid, remove from dictionary
                _openWindows.Remove(windowKey);
            }
            
            // Create new window
            var window = createWindow();
            _openWindows[windowKey] = window;
            
            // Remove from dictionary when closed
            window.Closed += (s, e) =>
            {
                if (_openWindows.ContainsKey(windowKey))
                {
                    _openWindows.Remove(windowKey);
                }
            };
            
            // Use Show() instead of ShowDialog() to allow multiple windows open at the same time
            if (allowMultiple)
            {
                window.Show();
            }
            else
            {
                window.ShowDialog();
            }
        }
        
        private void CashRegister_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("CashRegister", () => new CashRegisterWindow());
        }
        
        private void Articles_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("Articles", () => new ArticlesWindow());
        }
        
        private void PizzaManagement_Click(object sender, MouseButtonEventArgs e)
        {
            // Open dedicated Pizza Management window
            ShowOrFocusWindow("PizzaManagement", () => new PizzaManagementWindow());
        }
        
        private void Sales_Click(object sender, MouseButtonEventArgs e)
        {
            // Removed authentication requirement temporarily
            ShowOrFocusWindow("Sales", () => new SalesWindow());
        }

        private void Purchases_Click(object sender, MouseButtonEventArgs e)
        {
            // Removed authentication requirement temporarily
            ShowOrFocusWindow("Purchases", () => new PurchasesWindow());
        }
        
        private void BusinessPartners_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("BusinessPartners", () => new BusinessPartnersWindow());
        }
        
        private void Reports_Click(object sender, MouseButtonEventArgs e)
        {
            // Removed authentication requirement temporarily
            ShowOrFocusWindow("Reports", () => new ReportsWindow());
        }

        private void Analytics_Click(object sender, MouseButtonEventArgs e)
        {
            // Removed authentication requirement temporarily
            ShowOrFocusWindow("Analytics", () => new AnalyticsWindow());
        }
        
        private void Settings_Click(object sender, MouseButtonEventArgs e)
        {
            // Settings allow all users - basic system configuration
            ShowOrFocusWindow("Settings", () => new SettingsWindow());
        }
        
        private void Inventory_Click(object sender, MouseButtonEventArgs e)
        {
            // Route to ArticlesWindow - the primary article/stock management page
            ShowOrFocusWindow("Articles", () => new ArticlesWindow());
        }

        private void InventoryManagement_Click(object sender, MouseButtonEventArgs e)
        {
            // "STOQET" button now opens ArticlesWindow - the primary article management page
            // InventoryManagementWindow (with movements/suppliers/alerts) is accessible from within ArticlesWindow
            ShowOrFocusWindow("Articles", () => new ArticlesWindow());
        }

        private void BarcodePrinting_Click(object sender, MouseButtonEventArgs e)
        {
            // Open dedicated Barcode window for barcode printing functionality
            ShowOrFocusWindow("Barcode", () => new BarcodeWindow());
        }

        private void StaffScheduling_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("StaffScheduling", () => new StaffSchedulingWindow());
        }

        private void AdvancedReporting_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("AdvancedReporting", () => new AdvancedReportingWindow());
        }

        private void Finance_Click(object sender, MouseButtonEventArgs e)
        {
            // Removed authentication requirement temporarily
            ShowOrFocusWindow("Finance", () => new FinanceWindow());
        }

        private void TableManagement_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("TableManagement", () => new TableManagementWindow());
        }

        private void KitchenDisplay_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("KitchenDisplay", () => new KitchenDisplayWindow());
        }

        private void LoyaltyProgram_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("LoyaltyProgram", () => new LoyaltyProgramWindow());
        }

        private void DeliveryManagement_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("DeliveryManagement", () => new DeliveryManagementWindow());
        }

        /// <summary>
        /// Requires user authentication before accessing sensitive pages.
        /// Returns true if authenticated, false if cancelled or failed.
        /// </summary>
        private bool RequireAuthentication(string requiredPermission)
        {
            // Check if we're using SQL Server (POSUsers table is only in SQL Server)
            if (!POSDbContext.UseSqlServer)
            {
                // Allow access in SQLite mode (no user system)
                return true;
            }
            
            var loginWindow = new LoginWindow(requiredPermission);
            loginWindow.Owner = this;
            var result = loginWindow.ShowDialog();
            
            return result == true && loginWindow.IsAuthenticated;
        }
        
        private async void ImportData_Click(object sender, MouseButtonEventArgs e)
        {
            var result = MessageBox.Show(
                "A jeni i sigurt që doni të importoni të dhënat nga script.sql?\n\n" +
                "PARALAJMËRIM: Ky proces mund të zgjasë disa minuta dhe do të shtojë të dhëna në databazë.\n\n" +
                "Sigurohuni që skedari script.sql është në dosjen e projektit.",
                "Konfirmimi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Use application directory for script.sql
                    var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    var scriptPath = System.IO.Path.Combine(appDirectory, "script.sql");
                    
                    if (!System.IO.File.Exists(scriptPath))
                    {
                        MessageBox.Show($"Skedari script.sql nuk u gjet në:\n{scriptPath}", 
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    // Show progress window
                    var progressWindow = new Window
                    {
                        Title = "Importimi i të Dhënave",
                        Width = 400,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        ResizeMode = ResizeMode.NoResize
                    };
                    
                    var stack = new System.Windows.Controls.StackPanel
                    {
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    var label = new System.Windows.Controls.TextBlock
                    {
                        Text = "Duke importuar të dhënat...\nJu lutem prisni.",
                        TextAlignment = TextAlignment.Center,
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    
                    var progressBar = new System.Windows.Controls.ProgressBar
                    {
                        IsIndeterminate = true,
                        Height = 20,
                        Width = 300
                    };
                    
                    stack.Children.Add(label);
                    stack.Children.Add(progressBar);
                    progressWindow.Content = stack;
                    
                    progressWindow.Show();
                    
                    // Run import in background
                    await Task.Run(() =>
                    {
                        using (var context = new POSDbContext())
                        {
                            var scriptContent = System.IO.File.ReadAllText(scriptPath);
                            var commands = scriptContent.Split(new[] { "GO\r\n", "GO\n" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var cmd in commands)
                            {
                                var trimmed = cmd.Trim();
                                if (!string.IsNullOrWhiteSpace(trimmed))
                                {
                                    try { context.Database.ExecuteSqlRaw(trimmed); }
                                    catch { /* skip invalid statements */ }
                                }
                            }
                        }
                    });
                    
                    progressWindow.Close();
                    
                    MessageBox.Show("Importimi përfundoi me sukses!", "Sukses", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë importimit:\n{ex.Message}", "Gabim", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void Exit_Click(object sender, MouseButtonEventArgs e)
        {
            var result = MessageBox.Show("A jeni i sigurt që doni ta mbyllni aplikacionin?", 
                "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
        
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Exit_Click(this, null!);
        }
    }
}
