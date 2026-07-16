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
    public partial class LoyaltyProgramWindow : Window
    {
        private List<Customer> _customers = new List<Customer>();
        private List<Customer> _filteredCustomers = new List<Customer>();
        private List<Campaign> _campaigns = new List<Campaign>();
        private List<LoyaltyTransaction> _transactions = new List<LoyaltyTransaction>();

        public LoyaltyProgramWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            LoadCustomers();
            LoadCampaigns();
            LoadTransactions();
            UpdateStatistics();
        }

        private void LoadCustomers()
        {
            try
            {
                using var context = new POSDbContext();
                _customers = context.Customers
                    .OrderByDescending(c => c.LoyaltyPoints)
                    .ToList();

                // Create a separate copy for filtering
                _filteredCustomers = _customers.ToList();
                dgCustomers.ItemsSource = null; // force refresh
                dgCustomers.ItemsSource = _filteredCustomers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të klientëve:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCampaigns()
        {
            try
            {
                using var context = new POSDbContext();
                _campaigns = context.Campaigns
                    .OrderByDescending(c => c.IsActive)
                    .ThenBy(c => c.StartDate)
                    .ToList();

                if (dgCampaigns != null)
                    dgCampaigns.ItemsSource = _campaigns;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Invalid object name") && ex.Message.Contains("Campaigns"))
                {
                    DatabaseHelper.ShowTableMissingError("Campaigns", "Kampanjat");
                    _campaigns = new List<Campaign>();
                    if (dgCampaigns != null)
                        dgCampaigns.ItemsSource = _campaigns;
                }
                else
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të kampanjave:\n\n{ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadTransactions()
        {
            try
            {
                using var context = new POSDbContext();
                _transactions = context.LoyaltyTransactions
                    .OrderByDescending(t => t.TransactionDate)
                    .Take(500) // Last 500 transactions
                    .ToList();

                if (dgTransactions != null)
                    dgTransactions.ItemsSource = _transactions;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Invalid object name") && ex.Message.Contains("LoyaltyTransactions"))
                {
                    DatabaseHelper.ShowTableMissingError("LoyaltyTransactions", "Transaksionet e Besnikërisë");
                    _transactions = new List<LoyaltyTransaction>();
                    if (dgTransactions != null)
                        dgTransactions.ItemsSource = _transactions;
                }
                else
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të transaksioneve:\n\n{ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateStatistics()
        {
            if (txtTotalCustomers != null)
                txtTotalCustomers.Text = _customers.Count.ToString();

            if (txtActiveCustomers != null)
                txtActiveCustomers.Text = _customers.Count(c => c.IsActive).ToString();

            if (txtTotalPoints != null)
                txtTotalPoints.Text = _customers.Sum(c => c.LoyaltyPoints).ToString("N0");

            var avgSpent = _customers.Any() ? _customers.Average(c => c.TotalSpent) : 0;
            if (txtAverageSpent != null)
                txtAverageSpent.Text = $"€{avgSpent:N2}";
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TierFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // Prevent null reference during XAML initialization
            if (txtSearch == null || cmbTierFilter == null || dgCustomers == null)
                return;

            var searchText = txtSearch.Text?.ToLower() ?? string.Empty;
            var selectedTier = ((ComboBoxItem)cmbTierFilter.SelectedItem)?.Content?.ToString();

            _filteredCustomers = _customers.Where(c =>
            {
                // Search filter
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                   c.Name.ToLower().Contains(searchText) ||
                                   (c.Phone != null && c.Phone.Contains(searchText)) ||
                                   (c.LoyaltyCardNumber != null && c.LoyaltyCardNumber.ToLower().Contains(searchText));

                // Tier filter
                bool matchesTier = selectedTier == null ||
                                 selectedTier.Contains("Të Gjitha") ||
                                 selectedTier.Contains(c.Tier);

                return matchesSearch && matchesTier;
            }).ToList();

            dgCustomers.ItemsSource = _filteredCustomers;
        }

        private void AddPoints_Click(object sender, RoutedEventArgs e)
        {
            var selectedCustomer = dgCustomers.SelectedItem as Customer;
            if (selectedCustomer == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një klient nga lista para se të shtoni pikë.",
                    "Klient i Pazgjedhur", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Window
            {
                Title = $"⭐ Shto Pikë – {selectedCustomer.Name}",
                Width = 420, Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var sp = new StackPanel { Margin = new Thickness(28) };

            // Info banner
            var infoBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199)),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 18)
            };
            infoBorder.Child = new TextBlock
            {
                Text = $"Klienti: {selectedCustomer.Name}\nPika aktuale: {selectedCustomer.LoyaltyPoints:N0}",
                FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(146, 64, 14))
            };
            sp.Children.Add(infoBorder);

            sp.Children.Add(new TextBlock { Text = "Numri i Pikëve: *", FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 6) });
            var txtPoints = new TextBox { Height = 38, FontSize = 14, Padding = new Thickness(10, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center, BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14) };
            sp.Children.Add(txtPoints);

            sp.Children.Add(new TextBlock { Text = "Arsyeja (opsionale):", FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 6) });
            var txtReason = new TextBox { Height = 38, FontSize = 14, Padding = new Thickness(10, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center, BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 20) };
            sp.Children.Add(txtReason);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnSave = new Button { Content = "✓ Shto Pikët", Width = 130, Height = 38, Margin = new Thickness(0, 0, 10, 0), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, FontSize = 14, Cursor = System.Windows.Input.Cursors.Hand };
            var btnCancel = new Button { Content = "✗ Anulo", Width = 90, Height = 38, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)), Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, FontSize = 14, Cursor = System.Windows.Input.Cursors.Hand };
            btnCancel.Click += (_, __) => dlg.Close();
            btnSave.Click += (_, __) =>
            {
                txtPoints.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtPoints.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                if (!int.TryParse(txtPoints.Text.Trim(), out int points) || points == 0)
                {
                    txtPoints.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtPoints.BorderThickness = new Thickness(2);
                    MessageBox.Show("Fusha 'Numri i Pikëve' duhet të jetë numër i plotë jo zero.",
                        "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPoints.Focus();
                    return;
                }
                try
                {
                    using var ctx = new Database.POSDbContext();
                    var customer = ctx.Customers.Find(selectedCustomer.Id);
                    if (customer == null) { MessageBox.Show("Klienti nuk u gjet!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error); return; }

                    customer.LoyaltyPoints += points;
                    if (points > 0) customer.TotalPointsEarned += points;
                    else customer.TotalPointsRedeemed += Math.Abs(points);
                    // Every earned point represents €0.01 spent – keep TotalSpent in sync
                    if (points > 0)
                    {
                        customer.TotalSpent += points * 0.01m;
                        customer.OrderCount++;
                        customer.LastVisit = DateTime.Now;
                    }
                    customer.UpdatedAt = DateTime.Now;

                    // Update tier based on total points earned
                    customer.Tier = customer.TotalPointsEarned >= 5000 ? "Platinum"
                        : customer.TotalPointsEarned >= 2000 ? "Gold"
                        : customer.TotalPointsEarned >= 500 ? "Silver" : "Bronze";

                    ctx.LoyaltyTransactions.Add(new LoyaltyTransaction
                    {
                        CustomerId = customer.Id,
                        CustomerName = customer.Name,
                        TransactionType = points > 0 ? "ManualAdd" : "ManualDeduct",
                        Points = points,
                        Amount = points > 0 ? points * 0.01m : 0,
                        Description = string.IsNullOrWhiteSpace(txtReason.Text)
                            ? (points > 0 ? "Shtim manual i pikëve" : "Zbritje manuale e pikëve")
                            : txtReason.Text.Trim(),
                        TransactionDate = DateTime.Now,
                        CreatedAt = DateTime.Now
                    });

                    ctx.SaveChanges();
                    dlg.Close();
                    LoadData();
                    string action = points > 0 ? "shtuan" : "zbritën";
                    MessageBox.Show($"Pikët u {action} me sukses!\nKlienti {customer.Name} tani ka {customer.LoyaltyPoints:N0} pikë.",
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë shtimit të pikëve:\n{ex.Message}\n{ex.InnerException?.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            btnRow.Children.Add(btnSave);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        // ── Add Balance / Store Credit ────────────────────────────────────────────
        private void AddBalance_Click(object sender, RoutedEventArgs e)
        {
            var selectedCustomer = dgCustomers.SelectedItem as Customer;
            if (selectedCustomer == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një klient nga lista para se të shtoni saldo.",
                    "Klient i Pazgjedhur", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Window
            {
                Title = $"💰 Shto balanc – {selectedCustomer.Name}",
                Width = 460,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header strip
            var hdr = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105)),
                Padding = new Thickness(20, 14, 20, 14)
            };
            hdr.Child = new TextBlock
            {
                Text = "💰 Shto balanc monetar / Kredit dyqani",
                FontSize = 17, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White
            };
            Grid.SetRow(hdr, 0);
            rootGrid.Children.Add(hdr);

            var sp = new StackPanel { Margin = new Thickness(24, 16, 24, 10) };

            // Info banner
            var infoBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 250, 229)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 16)
            };
            infoBorder.Child = new TextBlock
            {
                Text = $"Klienti: {selectedCustomer.Name}\n" +
                       $"Kredit aktual i dyqanit: €{selectedCustomer.StoreCredit:N2}\n" +
                       $"Pika aktuale: {selectedCustomer.LoyaltyPoints:N0}",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 95, 70))
            };
            sp.Children.Add(infoBorder);

            sp.Children.Add(new TextBlock
            {
                Text = "Shuma (€): *",
                FontWeight = FontWeights.SemiBold, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105))
            });
            var txtAmount = new TextBox
            {
                Height = 40, FontSize = 15, FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105)),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 0, 0, 4)
            };
            sp.Children.Add(txtAmount);
            sp.Children.Add(new TextBlock
            {
                Text = "Pikët do të llogariten automatikisht (1 pikë = €0.01)",
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 14)
            });

            sp.Children.Add(new TextBlock
            {
                Text = "Lloji i operacionit:",
                FontWeight = FontWeights.SemiBold, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105))
            });
            var cmbType = new ComboBox { Height = 38, FontSize = 13, Margin = new Thickness(0, 0, 0, 14) };
            foreach (var t in new[] { "Pagesë manuale / Blerje", "Kredit dyqani", "Rimbursim", "Korrigjim" })
                cmbType.Items.Add(t);
            cmbType.SelectedIndex = 0;
            sp.Children.Add(cmbType);

            sp.Children.Add(new TextBlock
            {
                Text = "Shënim (opsionale):",
                FontWeight = FontWeights.SemiBold, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105))
            });
            var txtNote = new TextBox
            {
                Height = 38, FontSize = 13,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 0)
            };
            sp.Children.Add(txtNote);

            Grid.SetRow(sp, 1);
            rootGrid.Children.Add(sp);

            // Footer buttons
            var btnRow = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 12, 20, 12)
            };
            var btnRowSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            Button MakeBtn(string label, System.Windows.Media.Color bg, double w)
            {
                var b = new Button
                {
                    Content = label, Width = w, Height = 40,
                    Background = new System.Windows.Media.SolidColorBrush(bg),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold, FontSize = 13,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                var bf = new FrameworkElementFactory(typeof(Border));
                bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                bf.SetBinding(Border.BackgroundProperty,
                    new System.Windows.Data.Binding("Background")
                    { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                var cp2 = new FrameworkElementFactory(typeof(ContentPresenter));
                cp2.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp2.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                bf.AppendChild(cp2);
                b.Template = new ControlTemplate(typeof(Button)) { VisualTree = bf };
                return b;
            }

            var btnCancel = MakeBtn("✗ Anulo", System.Windows.Media.Color.FromRgb(100, 116, 139), 100);
            var btnSave = MakeBtn("✓ Shto balancin", System.Windows.Media.Color.FromRgb(5, 150, 105), 140);

            btnCancel.Click += (_, __) => dlg.Close();
            btnSave.Click += (_, __) =>
            {
                txtAmount.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtAmount.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                if (!decimal.TryParse(txtAmount.Text.Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal amount) || amount <= 0)
                {
                    txtAmount.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtAmount.BorderThickness = new Thickness(2);
                    MessageBox.Show("Fusha 'Shuma' duhet të jetë numër pozitiv.",
                        "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtAmount.Focus();
                    return;
                }

                try
                {
                    using var ctx = new POSDbContext();
                    var customer = ctx.Customers.Find(selectedCustomer.Id);
                    if (customer == null)
                    {
                        MessageBox.Show("Klienti nuk u gjet!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    int earnedPoints = (int)Math.Floor(amount * 100); // 100 points per €1
                    var operationType = cmbType.SelectedItem?.ToString() ?? "Pagesë manuale / Blerje";
                    // StoreCredit is the spendable wallet balance
                    customer.StoreCredit += amount;
                    customer.LoyaltyPoints += earnedPoints;
                    customer.TotalPointsEarned += earnedPoints;
                    // Update TotalSpent and OrderCount for purchase-type operations
                    if (operationType.Contains("Blerje") || operationType.Contains("Pagesë"))
                    {
                        customer.TotalSpent += amount;
                        customer.OrderCount++;
                    }
                    customer.LastVisit = DateTime.Now;
                    customer.UpdatedAt = DateTime.Now;

                    // Recalculate tier
                    customer.Tier = customer.TotalPointsEarned >= 5000 ? "Platinum"
                        : customer.TotalPointsEarned >= 2000 ? "Gold"
                        : customer.TotalPointsEarned >= 500 ? "Silver" : "Bronze";

                    ctx.LoyaltyTransactions.Add(new LoyaltyTransaction
                    {
                        CustomerId = customer.Id,
                        CustomerName = customer.Name,
                        TransactionType = "Earned",
                        Points = earnedPoints,
                        Amount = amount,
                        PurchaseAmount = amount,
                        Description = string.IsNullOrWhiteSpace(txtNote.Text)
                            ? $"Saldo e shtuar manuale – {cmbType.SelectedItem}"
                            : txtNote.Text.Trim(),
                        TransactionDate = DateTime.Now,
                        CreatedAt = DateTime.Now
                    });

                    ctx.SaveChanges();
                    dlg.Close();
                    LoadData();
                    MessageBox.Show(
                        $"✅ Saldo u shtua me sukses!\n\n" +
                        $"Klienti: {customer.Name}\n" +
                        $"Shuma e shtuar: €{amount:N2}\n" +
                        $"Pikë të fituara: +{earnedPoints}\n" +
                        $"Niveli: {customer.Tier}",
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë shtimit të saldos:\n{ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            btnRowSp.Children.Add(btnCancel);
            btnRowSp.Children.Add(btnSave);
            btnRow.Child = btnRowSp;
            Grid.SetRow(btnRow, 2);
            rootGrid.Children.Add(btnRow);

            dlg.Content = rootGrid;
            dlg.ShowDialog();
        }

        private void AddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CustomerEditDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var context = new POSDbContext();
                    var customer = new Customer
                    {
                        Name = dialog.CustomerName,
                        Phone = dialog.CustomerPhone,
                        Email = dialog.CustomerEmail,
                        LoyaltyCardNumber = GenerateLoyaltyCardNumber(),
                        LoyaltyPoints = 0,
                        TotalPointsEarned = 0,
                        TotalPointsRedeemed = 0,
                        Tier = "Bronze",
                        TotalSpent = 0,
                        OrderCount = 0,
                        Birthday = dialog.CustomerBirthday,
                        Address = dialog.CustomerAddress,
                        City = dialog.CustomerCity,
                        MarketingOptIn = dialog.MarketingOptIn,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    context.Customers.Add(customer);
                    context.SaveChanges();

                    MessageBox.Show($"Klienti u shtua me sukses!\nNumri i kartës: {customer.LoyaltyCardNumber}",
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);

                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë shtimit të klientit:\n\n{ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GenerateLoyaltyCardNumber()
        {
            // Generate unique loyalty card number: LOY-YYYYMMDD-XXXX
            var date = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random().Next(1000, 9999);
            return $"LOY-{date}-{random}";
        }

        private void AddCampaign_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CampaignEditDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Pre-fix: make any old NOT NULL columns in Campaigns table nullable
                    // so EF Core INSERT succeeds on databases with legacy schema
                    TryFixCampaignsSchema();

                    using var context = new POSDbContext();
                    var campaign = new Campaign
                    {
                        Name = dialog.CampaignName,
                        Description = dialog.CampaignDescription,
                        CampaignType = dialog.CampaignType,
                        PointsRequired = dialog.PointsRequired,
                        DiscountAmount = dialog.DiscountAmount,
                        TargetTier = dialog.TargetTier,
                        MinimumPurchase = 0,
                        ApplicableArticles = null,
                        StartDate = dialog.StartDate,
                        EndDate = dialog.EndDate,
                        IsActive = true,
                        Status = "Active",
                        UsageCount = 0,
                        TotalRevenue = 0,
                        Value = dialog.DiscountAmount,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    context.Campaigns.Add(campaign);
                    context.SaveChanges();

                    MessageBox.Show("Kampanja u krijua me sukses!",
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);

                    LoadData();
                }
                catch (Exception ex)
                {
                    var innerMsg = ex.InnerException?.Message ?? string.Empty;
                    MessageBox.Show($"Gabim gjatë krijimit të kampanjës:\n\n{ex.Message}" +
                                    (string.IsNullOrEmpty(innerMsg) ? "" : $"\n\nDetaje: {innerMsg}"),
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Ensures the Campaigns table schema matches the EF model.
        /// Adds missing columns and makes legacy NOT NULL columns nullable.
        /// </summary>
        private static void TryFixCampaignsSchema()
        {
            try
            {
                var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
                var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
                var connStr = $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=10;";

                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                conn.Open();

                // Check if table exists first
                using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Campaigns'", conn))
                {
                    if ((int)checkCmd.ExecuteScalar() == 0) return; // Table will be created by migration
                }

                // Ensure all EF-model columns exist
                var requiredColumns = new (string name, string definition)[]
                {
                    ("Name", "NVARCHAR(200) NOT NULL DEFAULT ''"),
                    ("Description", "NVARCHAR(MAX) NULL"),
                    ("CampaignType", "NVARCHAR(50) NOT NULL DEFAULT 'Discount'"),
                    ("PointsRequired", "INT NOT NULL DEFAULT 0"),
                    ("DiscountAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0"),
                    ("Value", "DECIMAL(18,2) NOT NULL DEFAULT 0"),
                    ("TargetTier", "NVARCHAR(20) NULL"),
                    ("MinimumPurchase", "DECIMAL(18,2) NULL"),
                    ("ApplicableArticles", "NVARCHAR(MAX) NULL"),
                    ("StartDate", "DATETIME2 NOT NULL DEFAULT GETDATE()"),
                    ("EndDate", "DATETIME2 NOT NULL DEFAULT GETDATE()"),
                    ("IsActive", "BIT NOT NULL DEFAULT 1"),
                    ("Status", "NVARCHAR(20) NOT NULL DEFAULT 'Draft'"),
                    ("UsageCount", "INT NOT NULL DEFAULT 0"),
                    ("TotalRevenue", "DECIMAL(18,2) NOT NULL DEFAULT 0"),
                    ("CreatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()"),
                    ("UpdatedAt", "DATETIME2 NOT NULL DEFAULT GETDATE()")
                };

                foreach (var (colName, colDef) in requiredColumns)
                {
                    try
                    {
                        using var chk = new Microsoft.Data.SqlClient.SqlCommand(
                            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Campaigns' AND COLUMN_NAME = '{colName}'", conn);
                        if ((int)chk.ExecuteScalar() == 0)
                        {
                            using var addCmd = new Microsoft.Data.SqlClient.SqlCommand(
                                $"ALTER TABLE [Campaigns] ADD [{colName}] {colDef}", conn);
                            addCmd.ExecuteNonQuery();
                        }
                    }
                    catch { /* per-column fix is non-fatal */ }
                }

                // Find NOT NULL columns not in the known model set and make them nullable
                var knownColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Id", "Name", "Description", "CampaignType", "PointsRequired", "DiscountAmount",
                    "Value", "TargetTier", "MinimumPurchase", "ApplicableArticles",
                    "StartDate", "EndDate", "IsActive", "Status", "UsageCount", "TotalRevenue",
                    "CreatedAt", "UpdatedAt"
                };

                var findSql = @"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Campaigns' AND IS_NULLABLE = 'NO' AND COLUMN_NAME <> 'Id'";
                var toFix = new System.Collections.Generic.List<(string name, string dataType, int? maxLen)>();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(findSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var colName = reader.GetString(0);
                        var dataType = reader.GetString(1);
                        var maxLen = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                        if (!knownColumns.Contains(colName))
                            toFix.Add((colName, dataType, maxLen));
                    }
                }

                foreach (var (colName, dataType, maxLen) in toFix)
                {
                    try
                    {
                        // Drop default constraint first
                        using var dropCmd = new Microsoft.Data.SqlClient.SqlCommand(
                            $@"DECLARE @cn NVARCHAR(256)
                               SELECT @cn = dc.name FROM sys.default_constraints dc
                               JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                               WHERE dc.parent_object_id = OBJECT_ID('Campaigns') AND c.name = '{colName}'
                               IF @cn IS NOT NULL EXEC('ALTER TABLE [Campaigns] DROP CONSTRAINT [' + @cn + ']')", conn);
                        dropCmd.ExecuteNonQuery();

                        var typeDef = maxLen.HasValue && maxLen > 0 ? $"{dataType}({maxLen})" : dataType;
                        using var alterCmd = new Microsoft.Data.SqlClient.SqlCommand(
                            $"ALTER TABLE [Campaigns] ALTER COLUMN [{colName}] {typeDef} NULL", conn);
                        alterCmd.ExecuteNonQuery();
                    }
                    catch { /* per-column fix failure is non-fatal */ }
                }

                // Explicitly ensure model-defined nullable columns are actually nullable in the DB
                var modelNullableCols = new (string cn, string td)[]
                {
                    ("MinimumPurchase", "DECIMAL(18,2)"),
                    ("ApplicableArticles", "NVARCHAR(MAX)"),
                    ("TargetTier", "NVARCHAR(20)"),
                    ("Description", "NVARCHAR(MAX)"),
                };
                foreach (var (cn, td) in modelNullableCols)
                {
                    try
                    {
                        using var isNullCmd = new Microsoft.Data.SqlClient.SqlCommand(
                            $"SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Campaigns' AND COLUMN_NAME = '{cn}'", conn);
                        if (isNullCmd.ExecuteScalar() as string == "NO")
                        {
                            using var dropDef = new Microsoft.Data.SqlClient.SqlCommand(
                                $@"DECLARE @dcon NVARCHAR(256)
                                   SELECT @dcon = dc.name FROM sys.default_constraints dc
                                   JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                                   WHERE dc.parent_object_id = OBJECT_ID('Campaigns') AND c.name = '{cn}'
                                   IF @dcon IS NOT NULL EXEC('ALTER TABLE [Campaigns] DROP CONSTRAINT [' + @dcon + ']')", conn);
                            dropDef.ExecuteNonQuery();
                            using var alterNullable = new Microsoft.Data.SqlClient.SqlCommand(
                                $"ALTER TABLE [Campaigns] ALTER COLUMN [{cn}] {td} NULL", conn);
                            alterNullable.ExecuteNonQuery();
                        }
                    }
                    catch { /* per-column fix is non-fatal */ }
                }
            }
            catch { /* schema fix is best-effort; EF Core will report the actual constraint error */ }
        }

        private void EditCampaign_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int campaignId)
            {
                try
                {
                    using var context = new POSDbContext();
                    var campaign = context.Campaigns.Find(campaignId);
                    if (campaign == null)
                    {
                        MessageBox.Show("Kampanja nuk u gjet!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var dialog = new CampaignEditDialog(campaign);
                    if (dialog.ShowDialog() == true)
                    {
                        campaign.Name = dialog.CampaignName;
                        campaign.Description = dialog.CampaignDescription;
                        campaign.CampaignType = dialog.CampaignType;
                        campaign.PointsRequired = dialog.PointsRequired;
                        campaign.DiscountAmount = dialog.DiscountAmount;
                        campaign.StartDate = dialog.StartDate;
                        campaign.EndDate = dialog.EndDate;
                        campaign.UpdatedAt = DateTime.Now;
                        context.SaveChanges();

                        MessageBox.Show($"Kampanja '{campaign.Name}' u përditësua me sukses!",
                            "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadData();
                    }
                }
                catch (Exception ex)
                {
                    var innerMsg = ex.InnerException?.Message ?? string.Empty;
                    MessageBox.Show($"Gabim gjatë modifikimit të kampanjës:\n\n{ex.Message}" +
                                    (string.IsNullOrEmpty(innerMsg) ? "" : $"\n\nDetaje: {innerMsg}"),
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        private void ToggleCampaign_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button && button.Tag is int campaignId))
                return;
            try
            {
                using var context = new POSDbContext();
                var campaign = context.Campaigns.Find(campaignId);
                if (campaign == null) return;
                campaign.IsActive = !campaign.IsActive;
                campaign.UpdatedAt = DateTime.Now;
                context.SaveChanges();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button && button.Tag is int customerId))
                return;
            try
            {
                using var context = new POSDbContext();
                var customer = context.Customers.Find(customerId);
                if (customer == null)
                {
                    MessageBox.Show("Klienti nuk u gjet!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialog = new CustomerEditDialog(customer);
                if (dialog.ShowDialog() == true)
                {
                    customer.Name = dialog.CustomerName;
                    customer.Phone = dialog.CustomerPhone;
                    customer.Email = dialog.CustomerEmail;
                    customer.Birthday = dialog.CustomerBirthday;
                    customer.Address = dialog.CustomerAddress;
                    customer.City = dialog.CustomerCity;
                    customer.MarketingOptIn = dialog.MarketingOptIn;
                    customer.UpdatedAt = DateTime.Now;
                    context.SaveChanges();

                    MessageBox.Show($"Klienti '{customer.Name}' u përditësua me sukses!",
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë modifikimit të klientit:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int customerId)
            {
                try
                {
                    using var context = new POSDbContext();
                    var customer = context.Customers.Find(customerId);
                    if (customer != null)
                    {
                        var history = context.LoyaltyTransactions
                            .Where(t => t.CustomerId == customerId)
                            .OrderByDescending(t => t.TransactionDate)
                            .ToList();

                        var message = $"Historiku i klientit: {customer.Name}\n\n" +
                                    $"Pika aktuale: {customer.LoyaltyPoints}\n" +
                                    $"Pika të fituara gjithsej: {customer.TotalPointsEarned}\n" +
                                    $"Pika të shpenzuara: {customer.TotalPointsRedeemed}\n" +
                                    $"Shuma totale: €{customer.TotalSpent:N2}\n" +
                                    $"Numri i porosive: {customer.OrderCount}\n\n" +
                                    $"Transaksionet e fundit: {history.Count}";

                        MessageBox.Show(message, "Historiku i Klientit",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të historikut:\n\n{ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CustomerRow_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgCustomers.SelectedItem is Customer customer)
            {
                ViewHistory_Click(new Button { Tag = customer.Id }, null!);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void EditCustomer_Click_Header(object sender, RoutedEventArgs e)
        {
            var selectedCustomer = dgCustomers.SelectedItem as Customer;
            if (selectedCustomer == null)
            {
                MessageBox.Show("Zgjidhni një klient nga lista para se të ndryshoni.",
                    "Klient i Pazgjedhur", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            EditCustomer_Click(new Button { Tag = selectedCustomer.Id }, e);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Simple dialogs for adding customers and campaigns
    public class CustomerEditDialog : Window
    {
        public string CustomerName { get; private set; } = string.Empty;
        public string? CustomerPhone { get; private set; }
        public string? CustomerEmail { get; private set; }
        public DateTime? CustomerBirthday { get; private set; }
        public string? CustomerAddress { get; private set; }
        public string? CustomerCity { get; private set; }
        public bool MarketingOptIn { get; private set; }

        public CustomerEditDialog(Customer? existingCustomer = null)
        {
            Title = existingCustomer != null ? $"Modifiko Klientin: {existingCustomer.Name}" : "Shto Klient të Ri";
            Width = 500;
            Height = 550;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int row = 0;

            // Name
            grid.Children.Add(CreateLabel("Emri:*", row));
            var txtName = CreateTextBox(row++);
            grid.Children.Add(txtName);

            // Phone
            grid.Children.Add(CreateLabel("Telefoni:", row));
            var txtPhone = CreateTextBox(row++);
            grid.Children.Add(txtPhone);

            // Email
            grid.Children.Add(CreateLabel("Email:", row));
            var txtEmail = CreateTextBox(row++);
            grid.Children.Add(txtEmail);

            // Birthday
            grid.Children.Add(CreateLabel("Dita e Lindjes:", row));
            var dpBirthday = new DatePicker { Margin = new Thickness(0, 5, 0, 10) };
            Grid.SetRow(dpBirthday, row++);
            grid.Children.Add(dpBirthday);

            // Address
            grid.Children.Add(CreateLabel("Adresa:", row));
            var txtAddress = CreateTextBox(row++);
            grid.Children.Add(txtAddress);

            // City
            grid.Children.Add(CreateLabel("Qyteti:", row));
            var txtCity = CreateTextBox(row++);
            grid.Children.Add(txtCity);

            // Marketing Opt-in
            var chkMarketing = new CheckBox
            {
                Content = "Pranon komunikime marketingu",
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(chkMarketing, row++);
            grid.Children.Add(chkMarketing);

            // Pre-populate fields if editing an existing customer
            if (existingCustomer != null)
            {
                txtName.Text = existingCustomer.Name;
                txtPhone.Text = existingCustomer.Phone ?? string.Empty;
                txtEmail.Text = existingCustomer.Email ?? string.Empty;
                dpBirthday.SelectedDate = existingCustomer.Birthday;
                txtAddress.Text = existingCustomer.Address ?? string.Empty;
                txtCity.Text = existingCustomer.City ?? string.Empty;
                chkMarketing.IsChecked = existingCustomer.MarketingOptIn;
            }

            // Buttons
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            Grid.SetRow(buttonsPanel, row + 1);

            var btnSave = new Button
            {
                Content = "\u2713 Ruaj",
                Width = 100,
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnSave.Click += (s, e) =>
            {
                txtName.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtName.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    txtName.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtName.BorderThickness = new Thickness(2);
                    MessageBox.Show("Fusha 'Emri' është e detyrueshme.", "Validim",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtName.Focus();
                    return;
                }

                CustomerName = txtName.Text;
                CustomerPhone = txtPhone.Text;
                CustomerEmail = txtEmail.Text;
                CustomerBirthday = dpBirthday.SelectedDate;
                CustomerAddress = txtAddress.Text;
                CustomerCity = txtCity.Text;
                MarketingOptIn = chkMarketing.IsChecked == true;

                DialogResult = true;
                Close();
            };

            var btnCancel = new Button
            {
                Content = "\u2716 Anulo",
                Width = 100,
                Padding = new Thickness(10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            buttonsPanel.Children.Add(btnSave);
            buttonsPanel.Children.Add(btnCancel);
            grid.Children.Add(buttonsPanel);

            Content = grid;
        }

        private TextBlock CreateLabel(string text, int row)
        {
            var label = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            Grid.SetRow(label, row);
            return label;
        }

        private TextBox CreateTextBox(int row)
        {
            var textBox = new TextBox
            {
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(textBox, row);
            return textBox;
        }
    }

    public class CampaignEditDialog : Window
    {
        public string CampaignName { get; private set; } = string.Empty;
        public string CampaignDescription { get; private set; } = string.Empty;
        public string CampaignType { get; private set; } = "Discount";
        public int PointsRequired { get; private set; }
        public decimal DiscountAmount { get; private set; }
        public string? TargetTier { get; private set; }
        public DateTime StartDate { get; private set; } = DateTime.Now;
        public DateTime EndDate { get; private set; } = DateTime.Now.AddMonths(1);

        public CampaignEditDialog(Campaign? existingCampaign = null)
        {
            Title = existingCampaign != null ? $"Modifiko Kampanjën: {existingCampaign.Name}" : "Krijo Kampanjë të Re";
            Width = 520;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var grid = new Grid { Margin = new Thickness(24) };
            for (int i = 0; i < 10; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int row = 0;

            // Name
            grid.Children.Add(CreateLabel("Emri i Kampanjës: *", row));
            var txtName = CreateTextBox(row++);
            grid.Children.Add(txtName);

            // Description
            grid.Children.Add(CreateLabel("Përshkrimi:", row));
            var txtDescription = CreateTextBox(row++);
            grid.Children.Add(txtDescription);

            // Campaign Type
            grid.Children.Add(CreateLabel("Lloji i Kampanjës:", row));
            var cmbType = new ComboBox
            {
                Padding = new Thickness(8),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10),
                Height = 36
            };
            cmbType.Items.Add("Discount");
            cmbType.Items.Add("BonusPoints");
            cmbType.Items.Add("FreeItem");
            cmbType.Items.Add("BuyOneGetOne");
            cmbType.SelectedItem = existingCampaign?.CampaignType ?? "Discount";
            Grid.SetRow(cmbType, row++);
            grid.Children.Add(cmbType);

            // Points Required
            grid.Children.Add(CreateLabel("Pika të Nevojshme: *", row));
            var txtPoints = CreateTextBox(row++);
            grid.Children.Add(txtPoints);

            // Discount Amount
            grid.Children.Add(CreateLabel("Zbritja (%): *", row));
            var txtDiscount = CreateTextBox(row++);
            grid.Children.Add(txtDiscount);

            // Target Tier
            grid.Children.Add(CreateLabel("Niveli i Synuar (opsionale):", row));
            var cmbTier = new ComboBox
            {
                Padding = new Thickness(8),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10),
                Height = 36
            };
            cmbTier.Items.Add("(Të gjithë)");
            cmbTier.Items.Add("Bronze");
            cmbTier.Items.Add("Silver");
            cmbTier.Items.Add("Gold");
            cmbTier.Items.Add("Platinum");
            cmbTier.SelectedItem = existingCampaign?.TargetTier ?? "(Të gjithë)";
            Grid.SetRow(cmbTier, row++);
            grid.Children.Add(cmbTier);

            // Start Date
            grid.Children.Add(CreateLabel("Data e Fillimit:", row));
            var dpStart = new DatePicker
            {
                Margin = new Thickness(0, 4, 0, 10),
                SelectedDate = existingCampaign?.StartDate ?? DateTime.Now,
                FontSize = 14
            };
            Grid.SetRow(dpStart, row++);
            grid.Children.Add(dpStart);

            // End Date
            grid.Children.Add(CreateLabel("Data e Mbarimit:", row));
            var dpEnd = new DatePicker
            {
                Margin = new Thickness(0, 4, 0, 10),
                SelectedDate = existingCampaign?.EndDate ?? DateTime.Now.AddMonths(1),
                FontSize = 14
            };
            Grid.SetRow(dpEnd, row++);
            grid.Children.Add(dpEnd);

            // Pre-fill if editing
            if (existingCampaign != null)
            {
                txtName.Text = existingCampaign.Name;
                txtDescription.Text = existingCampaign.Description ?? string.Empty;
                txtPoints.Text = existingCampaign.PointsRequired.ToString();
                txtDiscount.Text = existingCampaign.DiscountAmount.ToString("F2");
            }

            // Buttons
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            Grid.SetRow(buttonsPanel, row + 2);

            var btnSave = new Button
            {
                Content = existingCampaign != null ? "Ruaj Ndryshimet" : "Krijo Kampanjën",
                MinWidth = 140,
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            ApplyRoundedStyle(btnSave);

            btnSave.Click += (s, e) =>
            {
                txtName.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtName.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                txtPoints.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtPoints.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                txtDiscount.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtDiscount.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    txtName.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtName.BorderThickness = new Thickness(2);
                    MessageBox.Show("Fusha 'Emri i Kampanjës' është e detyrueshme.",
                        "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtName.Focus();
                    return;
                }
                if (!int.TryParse(txtPoints.Text, out int points) || points < 0)
                {
                    txtPoints.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtPoints.BorderThickness = new Thickness(2);
                    MessageBox.Show("Fusha 'Pika të Nevojshme' duhet të jetë numër i plotë jo negativ.",
                        "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPoints.Focus();
                    return;
                }
                if (!decimal.TryParse(txtDiscount.Text, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal discount) || discount < 0)
                {
                    txtDiscount.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtDiscount.BorderThickness = new Thickness(2);
                    MessageBox.Show("Fusha 'Zbritja' duhet të jetë numër decimal jo negativ (p.sh. 10.00).",
                        "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtDiscount.Focus();
                    return;
                }

                CampaignName = txtName.Text.Trim();
                CampaignDescription = txtDescription.Text.Trim();
                CampaignType = cmbType.SelectedItem?.ToString() ?? "Discount";
                PointsRequired = points;
                DiscountAmount = discount;
                TargetTier = cmbTier.SelectedItem?.ToString() == "(T\u00eb gjith\u00eb)" ? null : cmbTier.SelectedItem?.ToString();
                StartDate = dpStart.SelectedDate ?? DateTime.Now;
                EndDate = dpEnd.SelectedDate ?? DateTime.Now.AddMonths(1);

                DialogResult = true;
                Close();
            };

            var btnCancel = new Button
            {
                Content = "Anulo",
                MinWidth = 90,
                Padding = new Thickness(12, 10, 12, 10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            ApplyRoundedStyle(btnCancel);
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            buttonsPanel.Children.Add(btnSave);
            buttonsPanel.Children.Add(btnCancel);
            grid.Children.Add(buttonsPanel);

            scrollViewer.Content = grid;
            Content = scrollViewer;
        }

        private static void ApplyRoundedStyle(Button btn)
        {
            btn.Template = new System.Windows.Controls.ControlTemplate(typeof(Button))
            {
                VisualTree = CreateRoundedButtonFactory()
            };
        }

        private static System.Windows.FrameworkElementFactory CreateRoundedButtonFactory()
        {
            var border = new System.Windows.FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new System.Windows.PropertyPath("Background") });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetBinding(Border.PaddingProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new System.Windows.PropertyPath("Padding") });
            var cp = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            cp.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            return border;
        }

        private TextBlock CreateLabel(string text, int row)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 8, 0, 4)
            };
            Grid.SetRow(label, row);
            return label;
        }

        private TextBox CreateTextBox(int row)
        {
            var textBox = new TextBox
            {
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(textBox, row);
            return textBox;
        }
    }
}
