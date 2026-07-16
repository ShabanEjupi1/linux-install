using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;

namespace KosovaPOS.Windows
{
    public partial class InventoryManagementWindow : Window
    {
        private List<InventoryStockViewModel> _stocks = new List<InventoryStockViewModel>();
        private List<InventoryStockViewModel> _filteredStocks = new List<InventoryStockViewModel>();

        public InventoryManagementWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            LoadStocks();
            LoadMovements();
            LoadSuppliers();
            LoadAlerts();
            UpdateStatistics();
        }

        private void LoadStocks()
        {
            try
            {
                using var context = new POSDbContext();
                var stocks = context.InventoryStocks.ToList();

                if (stocks.Any())
                {
                    _stocks = stocks.Select(s => new InventoryStockViewModel
                    {
                        Id = s.Id,
                        ArticleId = s.ArticleId,
                        ArticleName = s.ArticleName,
                        CurrentQuantity = s.CurrentQuantity,
                        MinimumQuantity = s.MinimumQuantity,
                        ReorderQuantity = s.ReorderQuantity,
                        Unit = s.Unit,
                        Location = s.Location,
                        LastRestockedAt = s.LastRestockedAt,
                        StockStatus = s.CurrentQuantity <= 0 ? "OutOfStock" :
                                    s.CurrentQuantity <= s.MinimumQuantity ? "Low" : "Good",
                        StockStatusText = s.CurrentQuantity <= 0 ? "❌ PA STOK" :
                                        s.CurrentQuantity <= s.MinimumQuantity ? "⚠️ I ULËT" : "✅ MIRË"
                    }).ToList();
                }
                else
                {
                    // Fall back to Artikujt.Sasia when InventoryStocks is empty
                    var artikujt = context.Artikujt.ToList();
                    _stocks = artikujt.Select(a => new InventoryStockViewModel
                    {
                        Id = (int)a.Id,
                        ArticleId = (int)a.Id,
                        ArticleName = a.Emertimi ?? "Pa emër",
                        CurrentQuantity = (decimal)(a.Sasia ?? 0),
                        MinimumQuantity = (decimal)(a.StoguMinimal ?? 0),
                        ReorderQuantity = 0,
                        Unit = a.NjesiaP ?? "copë",
                        Location = a.Vendi,
                        LastRestockedAt = null,
                        StockStatus = (a.Sasia ?? 0) <= 0 ? "OutOfStock" :
                                    (a.StoguMinimal ?? 0) > 0 && (a.Sasia ?? 0) <= (a.StoguMinimal ?? 0) ? "Low" : "Good",
                        StockStatusText = (a.Sasia ?? 0) <= 0 ? "❌ PA STOK" :
                                        ((a.StoguMinimal ?? 0) > 0 && (a.Sasia ?? 0) <= (a.StoguMinimal ?? 0)) ? "⚠️ I ULËT" : "✅ MIRË"
                    }).OrderBy(s => s.ArticleName).ToList();
                }

                _filteredStocks = _stocks;
                dgStock.ItemsSource = _filteredStocks;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Invalid object name") && ex.Message.Contains("InventoryStocks"))
                {
                    DatabaseHelper.ShowTableMissingError("InventoryStocks", "Menaxhimi i Inventarit");
                    _stocks = new List<InventoryStockViewModel>();
                    _filteredStocks = _stocks;
                    if (dgStock != null)
                        dgStock.ItemsSource = _filteredStocks;
                }
                else
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të stoqeve:\n\n{ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadMovements()
        {
            try
            {
                using var context = new POSDbContext();
                var movements = context.InventoryMovements
                    .OrderByDescending(m => m.MovementDate)
                    .Take(500)
                    .ToList();

                dgMovements.ItemsSource = movements;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të lëvizjeve:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSuppliers()
        {
            try
            {
                using var context = new POSDbContext();
                var suppliers = context.InventorySuppliers
                    .OrderBy(s => s.Name)
                    .ToList();

                dgSuppliers.ItemsSource = suppliers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të furnizuesve:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAlerts()
        {
            try
            {
                using var context = new POSDbContext();
                var alerts = context.StockAlerts
                    .Where(a => a.Status == "Active")
                    .OrderByDescending(a => a.CreatedAt)
                    .ToList();

                dgAlerts.ItemsSource = alerts;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të alerteve:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatistics()
        {
            txtTotalItems.Text = _stocks.Count.ToString();
            txtLowStock.Text = _stocks.Count(s => s.StockStatus == "Low" || s.StockStatus == "OutOfStock").ToString();
            txtGoodStock.Text = _stocks.Count(s => s.StockStatus == "Good").ToString();
            txtTotalValue.Text = "€0.00"; // Calculate based on prices
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void StockFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // Guard against null references during initialization
            if (txtSearch == null || cmbStockFilter == null || _stocks == null)
                return;

            var searchText = txtSearch.Text?.ToLower() ?? "";
            var selectedFilter = ((ComboBoxItem)cmbStockFilter.SelectedItem)?.Content?.ToString();

            _filteredStocks = _stocks.Where(s =>
            {
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                   s.ArticleName.ToLower().Contains(searchText);

                bool matchesFilter = selectedFilter == null ||
                                   selectedFilter.Contains("Të Gjitha") ||
                                   (selectedFilter.Contains("I Ulët") && s.StockStatus == "Low") ||
                                   (selectedFilter.Contains("Mjaftueshëm") && s.StockStatus == "Good") ||
                                   (selectedFilter.Contains("Pa Stok") && s.StockStatus == "OutOfStock");

                return matchesSearch && matchesFilter;
            }).ToList();

            if (dgStock != null)
                dgStock.ItemsSource = _filteredStocks;
        }

        private void StockIn_Click(object sender, RoutedEventArgs e)
        {
            // Check if an item is selected
            if (dgStock.SelectedItem is not InventoryStockViewModel selectedStock)
            {
                MessageBox.Show("Ju lutem zgjidhni nj\u00EB artikull nga lista!", 
                    "Paralajm\u00EBrim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show input dialog for quantity
            var inputWindow = new Window
            {
                Title = "Hyrje e Stoqeve",
                Width = 450,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = $"Artikulli: {selectedStock.ArticleName}", 
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10) 
            });

            stack.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = $"Stoku aktual: {selectedStock.CurrentQuantity} {selectedStock.Unit}", 
                Margin = new Thickness(0, 0, 0, 15) 
            });

            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Sasia q\u00EB hyn n\u00EB stok:" });
            var quantityBox = new System.Windows.Controls.TextBox 
            { 
                Margin = new Thickness(0, 5, 0, 10),
                FontSize = 14,
                Text = "1"
            };
            stack.Children.Add(quantityBox);

            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "P\u00EBrshkrimi (opsional):" });
            var descriptionBox = new System.Windows.Controls.TextBox 
            { 
                Margin = new Thickness(0, 5, 0, 15),
                Height = 60,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };
            stack.Children.Add(descriptionBox);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnSave = new System.Windows.Controls.Button
            {
                Content = "Ruaj",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(10, 5, 10, 5)
            };
            btnSave.Click += (s, args) =>
            {
                if (decimal.TryParse(quantityBox.Text, out decimal quantity) && quantity > 0)
                {
                    try
                    {
                        using var context = new POSDbContext();

                        // Update stock quantity
                        var stock = context.InventoryStocks.FirstOrDefault(st => st.Id == selectedStock.Id);
                        if (stock != null)
                        {
                            stock.CurrentQuantity += quantity;
                            stock.LastRestockedAt = DateTime.Now;

                            // Add movement record
                            var movement = new InventoryMovement
                            {
                                ArticleId = stock.ArticleId,
                                ArticleName = stock.ArticleName,
                                MovementType = "In",
                                Quantity = quantity,
                                Unit = stock.Unit,
                                MovementDate = DateTime.Now,
                                Description = descriptionBox.Text,
                                Reference = $"MANUAL-IN-{DateTime.Now:yyyyMMddHHmmss}"
                            };
                            context.InventoryMovements.Add(movement);

                            context.SaveChanges();

                            MessageBox.Show($"Stoku u p\u00EBrditesua me sukses!\\nStoku i ri: {stock.CurrentQuantity} {stock.Unit}", 
                                "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);

                            inputWindow.Close();
                            LoadData(); // Refresh data
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjat\u00EB ruajtjes:\\n\\n{ex.Message}", 
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Ju lutem vendosni nj\u00EB sasi t\u00EB vlefshme!", 
                        "Paralajm\u00EBrim", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Anulo",
                Width = 80,
                Padding = new Thickness(10, 5, 10, 5)
            };
            btnCancel.Click += (s, args) => inputWindow.Close();

            buttonPanel.Children.Add(btnSave);
            buttonPanel.Children.Add(btnCancel);
            stack.Children.Add(buttonPanel);

            inputWindow.Content = stack;
            quantityBox.Focus();
            quantityBox.SelectAll();
            inputWindow.ShowDialog();
        }

        private void StockOut_Click(object sender, RoutedEventArgs e)
        {
            // Check if an item is selected
            if (dgStock.SelectedItem is not InventoryStockViewModel selectedStock)
            {
                MessageBox.Show("Ju lutem zgjidhni nj\u00EB artikull nga lista!", 
                    "Paralajm\u00EBrim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show input dialog for quantity
            var inputWindow = new Window
            {
                Title = "Dalje e Stoqeve",
                Width = 450,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = $"Artikulli: {selectedStock.ArticleName}", 
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10) 
            });

            stack.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = $"Stoku aktual: {selectedStock.CurrentQuantity} {selectedStock.Unit}", 
                Foreground = selectedStock.CurrentQuantity <= selectedStock.MinimumQuantity 
                    ? System.Windows.Media.Brushes.Red 
                    : System.Windows.Media.Brushes.Black,
                Margin = new Thickness(0, 0, 0, 15) 
            });

            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Sasia q\u00EB del nga stoku:" });
            var quantityBox = new System.Windows.Controls.TextBox 
            { 
                Margin = new Thickness(0, 5, 0, 10),
                FontSize = 14,
                Text = "1"
            };
            stack.Children.Add(quantityBox);

            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "P\u00EBrshkrimi (opsional):" });
            var descriptionBox = new System.Windows.Controls.TextBox 
            { 
                Margin = new Thickness(0, 5, 0, 15),
                Height = 60,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };
            stack.Children.Add(descriptionBox);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnSave = new System.Windows.Controls.Button
            {
                Content = "Ruaj",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(10, 5, 10, 5)
            };
            btnSave.Click += (s, args) =>
            {
                if (decimal.TryParse(quantityBox.Text, out decimal quantity) && quantity > 0)
                {
                    if (quantity > selectedStock.CurrentQuantity)
                    {
                        var result = MessageBox.Show(
                            $"Sasia q\u00EB doni t\u00EB nxirrni ({quantity}) \u00EBsht\u00EB m\u00EB e madhe se stoku aktual ({selectedStock.CurrentQuantity}).\\n\\nA doni t\u00EB vazhdoni gjithsesi?",
                            "Paralajm\u00EBrim", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                            return;
                    }

                    try
                    {
                        using var context = new POSDbContext();

                        // Update stock quantity
                        var stock = context.InventoryStocks.FirstOrDefault(st => st.Id == selectedStock.Id);
                        if (stock != null)
                        {
                            stock.CurrentQuantity -= quantity;

                            // Add movement record
                            var movement = new InventoryMovement
                            {
                                ArticleId = stock.ArticleId,
                                ArticleName = stock.ArticleName,
                                MovementType = "Out",
                                Quantity = quantity,
                                Unit = stock.Unit,
                                MovementDate = DateTime.Now,
                                Description = descriptionBox.Text,
                                Reference = $"MANUAL-OUT-{DateTime.Now:yyyyMMddHHmmss}"
                            };
                            context.InventoryMovements.Add(movement);

                            context.SaveChanges();

                            MessageBox.Show($"Stoku u p\u00EBrditesua me sukses!\\nStoku i ri: {stock.CurrentQuantity} {stock.Unit}", 
                                "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);

                            inputWindow.Close();
                            LoadData(); // Refresh data
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjat\u00EB ruajtjes:\\n\\n{ex.Message}", 
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Ju lutem vendosni nj\u00EB sasi t\u00EB vlefshme!", 
                        "Paralajm\u00EBrim", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Anulo",
                Width = 80,
                Padding = new Thickness(10, 5, 10, 5)
            };
            btnCancel.Click += (s, args) => inputWindow.Close();

            buttonPanel.Children.Add(btnSave);
            buttonPanel.Children.Add(btnCancel);
            stack.Children.Add(buttonPanel);

            inputWindow.Content = stack;
            quantityBox.Focus();
            quantityBox.SelectAll();
            inputWindow.ShowDialog();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class InventoryStockViewModel
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string ArticleName { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public decimal MinimumQuantity { get; set; }
        public decimal ReorderQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? Location { get; set; }
        public DateTime? LastRestockedAt { get; set; }
        public string StockStatus { get; set; } = "Good";
        public string StockStatusText { get; set; } = "✅ MIRË";
    }
}
