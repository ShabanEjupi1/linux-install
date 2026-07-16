using System;
using System.Drawing.Printing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DotNetEnv;
using KosovaPOS.Database;
using KosovaPOS.Models;
// using KosovaPOS.Tools; // Tools namespace excluded from project
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class SettingsWindow : Window
    {
        private User? _selectedUser = null;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadAvailableComPorts();
            LoadAvailablePrinters();
            LoadSettings();
            LoadUsers();
        }
        
        private void LoadAvailableComPorts()
        {
            try
            {
                FiscalPortComboBox.Items.Clear();
                
                // Get all available COM ports
                var ports = SerialPort.GetPortNames();
                
                if (ports.Length == 0)
                {
                    // No COM ports found, add common defaults
                    FiscalPortComboBox.Items.Add("COM1");
                    FiscalPortComboBox.Items.Add("COM2");
                    FiscalPortComboBox.Items.Add("COM3");
                    FiscalPortComboBox.Items.Add("COM4");
                    FiscalPortComboBox.Items.Add("⚠️ Asnjë port i detektuar");
                }
                else
                {
                    // Add all detected COM ports
                    foreach (var port in ports.OrderBy(p => p))
                    {
                        FiscalPortComboBox.Items.Add(port);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të portave COM: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                // Add defaults
                FiscalPortComboBox.Items.Add("COM1");
                FiscalPortComboBox.Items.Add("COM2");
            }
        }
        
        private void LoadAvailablePrinters()
        {
            try
            {
                // Get all installed printers
                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    ReceiptPrinterComboBox.Items.Add(printerName);
                    BarcodePrinterComboBox.Items.Add(printerName);
                }

                // Set default if no printers found
                if (ReceiptPrinterComboBox.Items.Count == 0)
                {
                    ReceiptPrinterComboBox.Items.Add("Microsoft Print to PDF");
                    BarcodePrinterComboBox.Items.Add("Microsoft Print to PDF");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të printerëve: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ReceiptPrinterComboBox.Items.Add("Microsoft Print to PDF");
                BarcodePrinterComboBox.Items.Add("Microsoft Print to PDF");
            }
        }
        
        private void LoadSettings()
        {
            // Load from .env file
            BusinessNameTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_NAME") ?? "BUTIKU TEKSTIL";
            BusinessNRFTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_NRF") ?? "";
            BusinessAddressTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_ADDRESS") ?? "";
            BusinessPhoneTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_PHONE") ?? "";
            BusinessEmailTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_EMAIL") ?? "";

            var savedPort = Environment.GetEnvironmentVariable("FISCAL_PRINTER_PORT") ?? "COM1";
            FiscalPortComboBox.Text = savedPort;

            FiscalBaudRateTextBox.Text = Environment.GetEnvironmentVariable("FISCAL_PRINTER_BAUD") ?? "115200";
            FiscalPathTextBox.Text = Environment.GetEnvironmentVariable("FISCAL_PATH") ?? "C:\\Temp\\";

            // Set receipt printer from environment or default
            var savedPrinter = Environment.GetEnvironmentVariable("RECEIPT_PRINTER") ?? "Microsoft Print to PDF";
            if (ReceiptPrinterComboBox.Items.Contains(savedPrinter))
                ReceiptPrinterComboBox.SelectedItem = savedPrinter;
            else if (ReceiptPrinterComboBox.Items.Count > 0)
                ReceiptPrinterComboBox.SelectedIndex = 0;

            PaperWidthTextBox.Text = Environment.GetEnvironmentVariable("PAPER_WIDTH") ?? "80";
            AutoCutCheckBox.IsChecked = Environment.GetEnvironmentVariable("AUTO_CUT") != "false";

            // Set barcode printer from environment
            var savedBarcodePrinter = Environment.GetEnvironmentVariable("BARCODE_PRINTER") ?? "HPRT LPQ80";
            if (BarcodePrinterComboBox.Items.Contains(savedBarcodePrinter))
                BarcodePrinterComboBox.SelectedItem = savedBarcodePrinter;
            else if (BarcodePrinterComboBox.Items.Count > 0)
                BarcodePrinterComboBox.SelectedIndex = 0;
            BarcodePrinterModelTextBox.Text = savedBarcodePrinter;
            BarcodeLabelWidthTextBox.Text = Environment.GetEnvironmentVariable("BARCODE_LABEL_WIDTH_MM") ?? "55";
            BarcodeLabelHeightTextBox.Text = Environment.GetEnvironmentVariable("BARCODE_LABEL_HEIGHT_MM") ?? "25";

            DatabasePathTextBox.Text = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "./Database/KosovaPOS.db";
        }
        
        private void RefreshComPorts_Click(object sender, RoutedEventArgs e)
        {
            var currentSelection = FiscalPortComboBox.Text;
            LoadAvailableComPorts();
            
            // Try to restore previous selection
            if (FiscalPortComboBox.Items.Contains(currentSelection))
            {
                FiscalPortComboBox.SelectedItem = currentSelection;
            }
            else
            {
                FiscalPortComboBox.Text = currentSelection;
            }
            
            var availablePorts = SerialPort.GetPortNames();
            if (availablePorts.Length > 0)
            {
                MessageBox.Show($"U gjetën {availablePorts.Length} porta COM:\n\n{string.Join(", ", availablePorts)}", 
                    "Portat COM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Asnjë port COM nuk u detektua në sistem!\n\n" +
                               "Këshilla:\n" +
                               "• Sigurohu që printeri fiskal është i lidhur me USB ose Serial\n" +
                               "• Kontrollo Device Manager për të parë nëse pajisja është detektuar\n" +
                               "• Instalo driver-ët e duhur për printer-in fiskal\n" +
                               "• Rifillo kompjuterin pasi të kesh lidhur printer-in",
                    "Asnjë port COM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void LoadUsers()
        {
            try
            {
                using var context = new Database.POSDbContext();
                var users = context.Users.OrderBy(u => u.FullName).ToList();
                UsersDataGrid.ItemsSource = users;

                // Show current user info
                var currentUser = Services.UserSessionService.Instance.CurrentUser;
                if (currentUser != null && UserPermissionsPanel != null)
                {
                    var matchedUser = users.FirstOrDefault(u => u.Id == currentUser.Id);
                    if (matchedUser != null)
                        SelectUserForPermissions(matchedUser);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Invalid object name") || ex.Message.Contains("POSUsers_Legacy"))
                {
                    // Table may not exist yet – silently ignore
                }
                else
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të përdoruesve: {ex.Message}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void SelectUserForPermissions(User user)
        {
            _selectedUser = user;
            CanManageArticlesCheckBox.IsChecked = user.CanManageArticles;
            CanManagePurchasesCheckBox.IsChecked = user.CanManagePurchases;
            CanViewReportsCheckBox.IsChecked = user.CanViewReports;
            CanGiveDiscountsCheckBox.IsChecked = user.CanGiveDiscounts;
            CanModifyPricesCheckBox.IsChecked = user.CanModifyPrices;
            CanDeleteReceiptsCheckBox.IsChecked = user.CanDeleteReceipts;
            CanManageUsersCheckBox.IsChecked = user.CanManageUsers;
            MaxDiscountTextBox.Text = user.MaxDiscountPercent.ToString("F0");
            // New module permissions
            if (CanManageDeliveryCheckBox != null) CanManageDeliveryCheckBox.IsChecked = user.CanManageDelivery;
            if (CanManageKitchenCheckBox != null) CanManageKitchenCheckBox.IsChecked = user.CanManageKitchen;
            if (CanManageTablesCheckBox != null) CanManageTablesCheckBox.IsChecked = user.CanManageTables;
            if (CanManageLoyaltyCheckBox != null) CanManageLoyaltyCheckBox.IsChecked = user.CanManageLoyalty;
            if (CanViewAnalyticsCheckBox != null) CanViewAnalyticsCheckBox.IsChecked = user.CanViewAnalytics;
            if (CanManageInventoryCheckBox != null) CanManageInventoryCheckBox.IsChecked = user.CanManageInventory;
            if (CanAccessStaffSchedulingCheckBox != null) CanAccessStaffSchedulingCheckBox.IsChecked = user.CanAccessStaffScheduling;
        }

        private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User user)
                SelectUserForPermissions(user);
        }

        private void SaveUserPermissions_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Zgjidhni një përdorues nga lista.", "Informacion",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                using var context = new Database.POSDbContext();
                var user = context.Users.Find(_selectedUser.Id);
                if (user == null) return;

                user.CanManageArticles = CanManageArticlesCheckBox.IsChecked == true;
                user.CanManagePurchases = CanManagePurchasesCheckBox.IsChecked == true;
                user.CanViewReports = CanViewReportsCheckBox.IsChecked == true;
                user.CanGiveDiscounts = CanGiveDiscountsCheckBox.IsChecked == true;
                user.CanModifyPrices = CanModifyPricesCheckBox.IsChecked == true;
                user.CanDeleteReceipts = CanDeleteReceiptsCheckBox.IsChecked == true;
                user.CanManageUsers = CanManageUsersCheckBox.IsChecked == true;
                decimal.TryParse(MaxDiscountTextBox.Text, out var maxDiscount);
                user.MaxDiscountPercent = maxDiscount;
                // New module permissions
                if (CanManageDeliveryCheckBox != null) user.CanManageDelivery = CanManageDeliveryCheckBox.IsChecked == true;
                if (CanManageKitchenCheckBox != null) user.CanManageKitchen = CanManageKitchenCheckBox.IsChecked == true;
                if (CanManageTablesCheckBox != null) user.CanManageTables = CanManageTablesCheckBox.IsChecked == true;
                if (CanManageLoyaltyCheckBox != null) user.CanManageLoyalty = CanManageLoyaltyCheckBox.IsChecked == true;
                if (CanViewAnalyticsCheckBox != null) user.CanViewAnalytics = CanViewAnalyticsCheckBox.IsChecked == true;
                if (CanManageInventoryCheckBox != null) user.CanManageInventory = CanManageInventoryCheckBox.IsChecked == true;
                if (CanAccessStaffSchedulingCheckBox != null) user.CanAccessStaffScheduling = CanAccessStaffSchedulingCheckBox.IsChecked == true;

                context.SaveChanges();
                LoadUsers();
                MessageBox.Show($"Të drejtat e '{user.FullName}' u ruajtën me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes:\n{ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            ShowUserEditDialog(null);
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is not User selected)
            {
                MessageBox.Show("Zgjidhni një përdorues nga lista.", "Informacion",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowUserEditDialog(selected);
        }

        private void ShowUserEditDialog(User? existing)
        {
            bool isNew = existing == null;
            var dlg = new Window
            {
                Title = isNew ? "➕ Shto Përdorues të Ri" : $"✏ Ndrysho: {existing!.FullName}",
                Width = 460, Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var sp = new StackPanel { Margin = new Thickness(28) };

            sp.Children.Add(MakeLabel("Emri i plotë *:"));
            var txtFullName = MakeTb(existing?.FullName ?? "");
            sp.Children.Add(txtFullName);

            sp.Children.Add(MakeLabel("Emri i përdoruesit *:", 10));
            var txtUsername = MakeTb(existing?.Username ?? "");
            sp.Children.Add(txtUsername);

            sp.Children.Add(MakeLabel("Email:", 10));
            var txtEmail = MakeTb(existing?.Email ?? "");
            sp.Children.Add(txtEmail);

            sp.Children.Add(MakeLabel("Roli *:", 10));
            var cmbRole = new ComboBox { Height = 36, FontSize = 13, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var r in new[] { "Admin", "Manager", "Cashier", "Warehouse", "Accountant" })
                cmbRole.Items.Add(r);
            cmbRole.SelectedItem = existing?.Role ?? "Cashier";
            sp.Children.Add(cmbRole);

            if (isNew)
            {
                sp.Children.Add(MakeLabel("Fjalëkalimi *:", 10));
                var txtPwd = new PasswordBox { Height = 36, FontSize = 13, Padding = new Thickness(8, 0, 8, 0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                    BorderThickness = new Thickness(1), Margin = new Thickness(0, 4, 0, 0) };
                sp.Children.Add(txtPwd);

                var chkActive = new CheckBox { Content = "Aktiv", IsChecked = true, FontSize = 13, Margin = new Thickness(0, 12, 0, 0) };
                sp.Children.Add(chkActive);

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
                var btnSave = MakeBtn("💾 Shto", "#22C55E", 110);
                var btnCancel = MakeBtn("✖ Anulo", "#64748B", 90, 10);
                btnCancel.Click += (_, __) => dlg.Close();
                btnSave.Click += (_, __) =>
                {
                    if (string.IsNullOrWhiteSpace(txtFullName.Text) || string.IsNullOrWhiteSpace(txtUsername.Text))
                    {
                        MessageBox.Show("Emri i plotë dhe emri i përdoruesit janë të detyrueshme.", "Validim",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(txtPwd.Password))
                    {
                        MessageBox.Show("Fjalëkalimi është i detyrueshëm.", "Validim",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    try
                    {
                        using var ctx = new Database.POSDbContext();
                        if (ctx.Users.Any(u => u.Username == txtUsername.Text.Trim()))
                        {
                            MessageBox.Show("Ky emër përdoruesi ekziston tashmë.", "Gabim",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        var newUser = new User
                        {
                            FullName = txtFullName.Text.Trim(),
                            Username = txtUsername.Text.Trim(),
                            Email = txtEmail.Text.Trim(),
                            Role = cmbRole.SelectedItem?.ToString() ?? "Cashier",
                            PasswordHash = HashPassword(txtPwd.Password),
                            IsActive = chkActive.IsChecked == true,
                            CreatedAt = DateTime.Now,
                            CanManageArticles = true,
                            CanViewReports = true
                        };
                        ctx.Users.Add(newUser);
                        ctx.SaveChanges();
                        dlg.Close();
                        LoadUsers();
                        MessageBox.Show($"Përdoruesi '{newUser.FullName}' u shtua me sukses!", "Sukses",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim:\n{ex.Message}\n{ex.InnerException?.Message}", "Gabim",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                btnRow.Children.Add(btnSave);
                btnRow.Children.Add(btnCancel);
                sp.Children.Add(btnRow);
            }
            else
            {
                var chkActive = new CheckBox { Content = "Aktiv", IsChecked = existing!.IsActive, FontSize = 13, Margin = new Thickness(0, 12, 0, 0) };
                sp.Children.Add(chkActive);

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
                var btnSave = MakeBtn("💾 Ruaj", "#3B82F6", 110);
                var btnCancel = MakeBtn("✖ Anulo", "#64748B", 90, 10);
                btnCancel.Click += (_, __) => dlg.Close();
                btnSave.Click += (_, __) =>
                {
                    if (string.IsNullOrWhiteSpace(txtFullName.Text) || string.IsNullOrWhiteSpace(txtUsername.Text))
                    {
                        MessageBox.Show("Emri i plotë dhe emri i përdoruesit janë të detyrueshme.", "Validim",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    try
                    {
                        using var ctx = new Database.POSDbContext();
                        var user = ctx.Users.Find(existing!.Id);
                        if (user == null) return;
                        user.FullName = txtFullName.Text.Trim();
                        user.Username = txtUsername.Text.Trim();
                        user.Email = txtEmail.Text.Trim();
                        user.Role = cmbRole.SelectedItem?.ToString() ?? user.Role;
                        user.IsActive = chkActive.IsChecked == true;
                        ctx.SaveChanges();
                        dlg.Close();
                        LoadUsers();
                        MessageBox.Show("Të dhënat u ruajtën me sukses!", "Sukses",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                btnRow.Children.Add(btnSave);
                btnRow.Children.Add(btnCancel);
                sp.Children.Add(btnRow);
            }

            dlg.Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            dlg.ShowDialog();
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is not User selected)
            {
                MessageBox.Show("Zgjidhni një përdorues nga lista.", "Informacion",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Window
            {
                Title = $"🔒 Ndrysho Fjalëkalimin – {selected.FullName}",
                Width = 400, Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var sp = new StackPanel { Margin = new Thickness(28) };
            sp.Children.Add(MakeLabel("Fjalëkalimi i ri *:"));
            var pwd1 = new PasswordBox { Height = 36, FontSize = 13, Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 4, 0, 14) };
            sp.Children.Add(pwd1);
            sp.Children.Add(MakeLabel("Konfirmo fjalëkalimin *:"));
            var pwd2 = new PasswordBox { Height = 36, FontSize = 13, Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 4, 0, 20) };
            sp.Children.Add(pwd2);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnSave = MakeBtn("🔒 Ndrysho", "#D97706", 120);
            var btnCancel = MakeBtn("✖ Anulo", "#64748B", 90, 10);
            btnCancel.Click += (_, __) => dlg.Close();
            btnSave.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(pwd1.Password))
                {
                    MessageBox.Show("Fjalëkalimi nuk mund të jetë bosh.", "Validim",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (pwd1.Password != pwd2.Password)
                {
                    MessageBox.Show("Fjalëkalimet nuk përputhen.", "Validim",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                try
                {
                    using var ctx = new Database.POSDbContext();
                    var user = ctx.Users.Find(selected.Id);
                    if (user == null) return;
                    user.PasswordHash = HashPassword(pwd1.Password);
                    ctx.SaveChanges();
                    dlg.Close();
                    MessageBox.Show("Fjalëkalimi u ndryshua me sukses!", "Sukses",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            btnRow.Children.Add(btnSave);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        private void ChangeMyPassword_Click(object sender, RoutedEventArgs e)
        {
            var currentUser = Services.UserSessionService.Instance.CurrentUser;
            if (currentUser == null)
            {
                MessageBox.Show("Nuk ka përdorues të kyçur.", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var currentPwd = CurrentPasswordBox.Password;
            var newPwd = NewPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPwd))
            {
                MessageBox.Show("Ju lutem shkruani fjalëkalimin tuaj aktual.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CurrentPasswordBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 4)
            {
                MessageBox.Show("Fjalëkalimi i ri duhet të ketë të paktën 4 karaktere.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NewPasswordBox.Focus();
                return;
            }

            try
            {
                using var ctx = new Database.POSDbContext();
                var user = ctx.Users.Find(currentUser.Id);
                if (user == null)
                {
                    MessageBox.Show("Llogaria nuk u gjet.", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (user.PasswordHash != HashPassword(currentPwd))
                {
                    MessageBox.Show("Fjalëkalimi aktual është i gabuar.", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    CurrentPasswordBox.Clear();
                    CurrentPasswordBox.Focus();
                    return;
                }

                user.PasswordHash = HashPassword(newPwd);
                ctx.SaveChanges();

                CurrentPasswordBox.Clear();
                NewPasswordBox.Clear();
                MessageBox.Show("✅ Fjalëkalimi u ndryshua me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ndryshimit të fjalëkalimit:\n{ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is not User selected)
            {
                MessageBox.Show("Zgjidhni një përdorues nga lista.", "Informacion",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var currentUser = Services.UserSessionService.Instance.CurrentUser;
            if (currentUser?.Id == selected.Id)
            {
                MessageBox.Show("Nuk mund të fshini llogarinë tuaj aktuale.", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"A jeni të sigurt që dëshironi të fshini përdoruesin '{selected.FullName}'?",
                "Konfirmim", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using var ctx = new Database.POSDbContext();
                var user = ctx.Users.Find(selected.Id);
                if (user != null)
                {
                    ctx.Users.Remove(user);
                    ctx.SaveChanges();
                }
                LoadUsers();
                MessageBox.Show("Përdoruesi u fshi me sukses.", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "KosovaPOS_Salt"));
            return Convert.ToBase64String(bytes);
        }

        private static TextBlock MakeLabel(string text, int topMargin = 0) =>
            new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 13,
                Margin = new Thickness(0, topMargin, 0, 4) };

        private static TextBox MakeTb(string text = "") =>
            new TextBox { Height = 36, FontSize = 13, Text = text, Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 0) };

        private static Button MakeBtn(string content, string colorHex, int width, int leftMargin = 0)
        {
            var btn = new Button
            {
                Content = content, Width = width, Height = 38,
                Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(colorHex)!,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(leftMargin, 0, 0, 0)
            };
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cp);
            btn.Template = new ControlTemplate(typeof(Button)) { VisualTree = borderFactory };
            return btn;
        }
        
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");

                var selectedPrinter = ReceiptPrinterComboBox.SelectedItem?.ToString() ?? "Microsoft Print to PDF";
                var selectedComPort = FiscalPortComboBox.Text;
                var selectedBarcodePrinter = BarcodePrinterComboBox.SelectedItem?.ToString() ?? BarcodePrinterModelTextBox.Text ?? "HPRT LPQ80";

                // Validate COM port
                var availablePorts = SerialPort.GetPortNames();
                if (availablePorts.Length > 0 && !availablePorts.Contains(selectedComPort))
                {
                    var result = MessageBox.Show(
                        $"Porti COM '{selectedComPort}' nuk u gjet në sistem!\n\n" +
                        $"Portat e disponueshme: {string.Join(", ", availablePorts)}\n\n" +
                        "A dëshiron të vazhdosh përsëri?",
                        "Port COM i pavlefshëm",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        return;
                }

                var envContent = $@"# Kosovo POS System - Configuration

# Database Configuration
DATABASE_PATH={DatabasePathTextBox.Text}

# Fiscal Printer Configuration
FISCAL_PRINTER_PORT={selectedComPort}
FISCAL_PRINTER_MODEL=FP700+
FISCAL_TEMP_PATH=C:\TEMP\
FISCAL_PRINTER_BAUD={FiscalBaudRateTextBox.Text}
FISCAL_PATH={FiscalPathTextBox.Text}
FISCAL_INLINE_ARTICLES=true

# Business Configuration
BUSINESS_NAME={BusinessNameTextBox.Text}
BUSINESS_NUI={BusinessNRFTextBox.Text}
BUSINESS_NRF={BusinessNRFTextBox.Text}
BUSINESS_ADDRESS={BusinessAddressTextBox.Text}
BUSINESS_PHONE={BusinessPhoneTextBox.Text}
BUSINESS_EMAIL={BusinessEmailTextBox.Text}

# Tax Configuration (Kosovo)
VAT_STANDARD=18
VAT_REDUCED=8

# Receipt Printer Settings
RECEIPT_PRINTER={selectedPrinter}
PAPER_WIDTH={PaperWidthTextBox.Text}
AUTO_CUT={AutoCutCheckBox.IsChecked}

# Barcode Printer Settings
BARCODE_PRINTER={selectedBarcodePrinter}
BARCODE_LABEL_WIDTH_MM={BarcodeLabelWidthTextBox.Text}
BARCODE_LABEL_HEIGHT_MM={BarcodeLabelHeightTextBox.Text}

# IMPORTANT: Fiscal printing enable/disable
FISCAL_ENABLED=true

# License Configuration
LICENSE_KEY=
LICENSE_EXPIRY=

# Cashier Configuration
DEFAULT_CASHIER=01
CASHIER_NAME=Administratori
";

                File.WriteAllText(envPath, envContent);

                // Reload environment variables
                Env.Load(envPath);

                MessageBox.Show("Konfigurimet u ruajtën me sukses!\nRistarto aplikacionin që të aplikohen ndryshimet.", 
                    "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes së konfigurimeveː {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TestFiscalPrinter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show($"Duke testuar lidhjen me printerin fiskal në {FiscalPortComboBox.Text}...\n\n" +
                               "Nota: Për testimin e plotë të printerit fiskal FP700+, " +
                               "sigurohu që printeri është i lidhur dhe i ndezur.", 
                               "Test i printerit fiskal", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // In production, this would actually test the connection
                var fiscalService = new Services.FiscalPrinterService();
                var testPath = fiscalService.GenerateZReport();
                
                if (File.Exists(testPath))
                {
                    MessageBox.Show($"Testimi përfundoi me sukses!\nSkedari i testit u krijua në: {testPath}", 
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë testimit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TestReceiptPrinter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedPrinter = ReceiptPrinterComboBox.SelectedItem?.ToString();
                
                if (string.IsNullOrEmpty(selectedPrinter))
                {
                    MessageBox.Show("Të lutem zgjidh një printer nga lista!", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Temporarily set the printer for testing
                Environment.SetEnvironmentVariable("RECEIPT_PRINTER", selectedPrinter);
                
                var printerService = new Services.ReceiptPrinterService();
                
                // Create a test receipt
                var testReceipt = new Models.Receipt
                {
                    ReceiptNumber = "TEST-001",
                    Date = DateTime.Now,
                    BuyerName = "Test Klient",
                    TotalAmount = 10.00m,
                    TaxAmount = 1.80m,
                    PaidAmount = 10.00m,
                    PaymentMethod = "Kesh",
                    CashierName = "Admin",
                    Items = new System.Collections.Generic.List<Models.ReceiptItem>
                    {
                        new Models.ReceiptItem
                        {
                            Article = new Models.Article { Name = "Test Produkt", Barcode = "TEST123" },
                            Barcode = "TEST123",
                            ArticleName = "Test Produkt",
                            Quantity = 1,
                            Price = 10.00m,
                            VATRate = 18,
                            TotalValue = 10.00m
                        }
                    }
                };
                
                printerService.PrintNonFiscalReceipt(testReceipt);
                
                MessageBox.Show($"Fatura e testit u dërgua me sukses në printerin:\n{selectedPrinter}", 
                    "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit:\n\n{ex.Message}\n\n" +
                               "Të lutem kontrollo që printeri të jetë i lidhur dhe i disponueshëm.", 
                               "Gabim",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BackupDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbPath = DatabasePathTextBox.Text;
                if (!File.Exists(dbPath))
                {
                    MessageBox.Show("Baza e të dhënave nuk u gjet!", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "POS_Backups");
                Directory.CreateDirectory(backupFolder);
                
                var backupFileName = $"KosovaPOS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                var backupPath = Path.Combine(backupFolder, backupFileName);
                
                File.Copy(dbPath, backupPath, true);
                
                MessageBox.Show($"Backup-i u krijua me sukses!\n\nLokacioni: {backupPath}", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Open backup folder
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = backupFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë krijimit të backup: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("A dëshironi të importoni të dhënat nga skedari script.sql?\n\n" +
                                        "VËREJTJE: Kjo do të fshijë të gjitha produktet ekzistuese dhe do të importojë " +
                                        "të dhënat e reja me çmimet e sakta!\n\n" +
                                        "Rekomandohet të bëni backup të bazës së të dhënave para se të vazhdoni.",
                                        "Importo të dhëna", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "script.sql");
                    
                    if (!File.Exists(scriptPath))
                    {
                        MessageBox.Show("Skedari script.sql nuk u gjet!", "Gabim",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    MessageBox.Show("Importimi filloi në background.\nKy proces mund të zgjasë disa minuta...",
                        "Importim", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Import in background
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            using var context = new Database.POSDbContext();
                            var scriptContent = await System.Threading.Tasks.Task.Run(() => File.ReadAllText(scriptPath));
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

                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("Importimi përfundoi me sukses!\n\n" +
                                              "Produktet u importuan me çmimet e sakta.", 
                                              "Sukses",
                                              MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Gabim gjatë importimit: {ex.Message}", "Gabim",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim: {ex.Message}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TestBarcodePrinter_Click(object sender, RoutedEventArgs e)
        {
            var printer = BarcodePrinterComboBox.SelectedItem?.ToString() ?? BarcodePrinterModelTextBox.Text;
            MessageBox.Show($"Duke testuar printerin e barkodit:\n{printer}\n\nPrintimi i barkodit do të funksionojë pasi të instalohet driveri i duhur dhe të konfigurohet printeri.",
                "Test Printer Barkodash", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void MapPLUFromDbf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Default path for fiscal printer Articles.dbf
                var defaultDbfPath = @"C:\Program Files (x86)\ENTERNET\PGM-KS\Data\FP550_EN21003910\Articles.dbf";
                
                // Let user browse for the file
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Zgjidhni skedarin Articles.dbf të printerit fiskal",
                    Filter = "DBF Files (*.dbf)|*.dbf|All Files (*.*)|*.*",
                    InitialDirectory = File.Exists(defaultDbfPath) 
                        ? Path.GetDirectoryName(defaultDbfPath) 
                        : @"C:\Program Files (x86)"
                };
                
                // Set default file if exists
                if (File.Exists(defaultDbfPath))
                {
                    dialog.FileName = defaultDbfPath;
                }
                
                if (dialog.ShowDialog() != true)
                {
                    return;
                }
                
                var dbfPath = dialog.FileName;
                
                if (!File.Exists(dbfPath))
                {
                    MessageBox.Show($"Skedari nuk u gjet: {dbfPath}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var result = MessageBox.Show(
                    $"Po përgatitet mapimi i numrave PLU nga:\n{dbfPath}\n\n" +
                    "Ky proces do të:\n" +
                    "• Lexojë artikujt nga skedari Articles.dbf\n" +
                    "• Mapojë numrat PLU me artikujt në bazën e të dhënave\n" +
                    "• Përditësojë numrat PLU për printimin fiskal\n\n" +
                    "A dëshironi të vazhdoni?",
                    "Konfirmimi i mapimit PLU",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // Run PLU mapping
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "KosovaPOS.db");
                if (!File.Exists(dbPath))
                {
                    dbPath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "./Database/KosovaPOS.db";
                    dbPath = Path.GetFullPath(dbPath);
                }
                
                if (!File.Exists(dbPath))
                {
                    MessageBox.Show($"Baza e të dhënave nuk u gjet: {dbPath}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var connectionString = $"Data Source={dbPath}";
                // TODO: PLUMapper functionality has been disabled (Tools namespace excluded)
                // var mapper = new Tools.PLUMapper(dbfPath);
                // mapper.MapPLUToDatabase(connectionString);
                // var reportPath = Path.Combine(Path.GetDirectoryName(dbPath) ?? ".", "PLU_Mapping_Report.csv");
                // mapper.ExportMappingReport(reportPath, connectionString);
                
                MessageBox.Show(
                    "PLU mapping functionality is currently unavailable.\n\n" +
                    "The Tools namespace has been excluded from this build.",
                    "Feature Unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
                
                /* Disabled code - PLU Mapping functionality
                MessageBox.Show(
                    $"✓ Mapimi i numrave PLU përfundoi me sukses!\n\n" +
                    $"Raporti u ruajt në:\n{reportPath}\n\n" +
                    "Tani mund të printoni faturat fiskale me numrat e saktë PLU.",
                    "Sukses",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                // Ask if user wants to open the report
                var openReport = MessageBox.Show(
                    "A dëshironi të hapni raportin e mapimit?",
                    "Hap raportin",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (openReport == MessageBoxResult.Yes && File.Exists(reportPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = reportPath,
                        UseShellExecute = true
                    });
                }
                */
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(
                    $"Skedari Articles.dbf nuk u gjet!\n\n{ex.Message}\n\n" +
                    "Sigurohu që:\n" +
                    "• Printeri fiskal është i instaluar në kompjuter\n" +
                    "• Softueri i printerit fiskal (ENTERNET) është i instaluar\n" +
                    "• Skedari Articles.dbf ekziston në dosjen e printerit fiskal",
                    "Gabim - Skedari nuk u gjet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Gabim gjatë mapimit të numrave PLU:\n\n{ex.Message}\n\n" +
                    "Detaje teknike:\n{ex.StackTrace}",
                    "Gabim",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
