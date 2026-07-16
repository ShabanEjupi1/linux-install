using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KosovaPOS.Database;
using KosovaPOS.Services;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace KosovaPOS.Windows
{
    public partial class StockWindow : Window
    {
        private ObservableCollection<StockItem> _stockItems = new ObservableCollection<StockItem>();
        private List<StockItem> _allItems = new List<StockItem>();
        
        public class StockItem
        {
            public int Id { get; set; }
            public string Barcode { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public decimal SalesPrice { get; set; }
            public decimal StockQuantity { get; set; }
            public decimal StockIn { get; set; }
            public decimal StockOut { get; set; }
            public string Category { get; set; } = string.Empty;
            
            public bool IsLowStock => StockQuantity > 0 && StockQuantity < 10;
            public bool IsOutOfStock => StockQuantity <= 0;
        }
        
        public StockWindow()
        {
            InitializeComponent();
            LoadData();
        }
        
        private void LoadData()
        {
            try
            {
                var articles = ArticleDataService.GetAllArticles();
                
                _allItems = articles.Select(a => new StockItem
                {
                    Id = a.Id,
                    Barcode = a.Barcode ?? string.Empty,
                    Name = a.Name ?? string.Empty,
                    Unit = a.Unit ?? "copë",
                    SalesPrice = a.SalesPrice,
                    StockQuantity = a.StockQuantity,
                    StockIn = a.StockIn,
                    StockOut = a.StockOut,
                    Category = a.Category ?? string.Empty
                })
                .OrderBy(s => s.Name)
                .ToList();
                
                ApplyFilter();
                UpdateSummary();
                LastUpdatedText.Text = $"Përditësuar: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ApplyFilter()
        {
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? string.Empty;
            int filterIndex = FilterCombo?.SelectedIndex ?? 0;
            
            var filtered = _allItems.AsEnumerable();
            
            // Apply search
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(s =>
                    (s.Barcode?.ToLower().Contains(searchText) ?? false) ||
                    (s.Name?.ToLower().Contains(searchText) ?? false) ||
                    (s.Category?.ToLower().Contains(searchText) ?? false));
            }
            
            // Apply filter
            switch (filterIndex)
            {
                case 1: // In stock
                    filtered = filtered.Where(s => s.StockQuantity > 0);
                    break;
                case 2: // Out of stock
                    filtered = filtered.Where(s => s.StockQuantity <= 0);
                    break;
                case 3: // Low stock
                    filtered = filtered.Where(s => s.StockQuantity > 0 && s.StockQuantity < 10);
                    break;
                case 4: // Critical stock
                    filtered = filtered.Where(s => s.StockQuantity > 0 && s.StockQuantity < 5);
                    break;
            }
            
            _stockItems = new ObservableCollection<StockItem>(filtered.ToList());
            StockDataGrid.ItemsSource = _stockItems;
            
            ResultCountText.Text = $"Duke shfaqur {_stockItems.Count} nga {_allItems.Count} artikuj";
        }
        
        private void UpdateSummary()
        {
            TotalArticlesText.Text = _allItems.Count.ToString("N0");
            InStockText.Text = _allItems.Count(s => s.StockQuantity > 0).ToString("N0");
            LowStockText.Text = _allItems.Count(s => s.StockQuantity > 0 && s.StockQuantity < 10).ToString("N0");
            OutOfStockText.Text = _allItems.Count(s => s.StockQuantity <= 0).ToString("N0");
        }
        
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }
        
        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ApplyFilter();
            }
        }
        
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
        
        private void StockDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StockDataGrid.SelectedItem is StockItem selectedItem)
            {
                // Show stock adjustment dialog
                var adjustmentWindow = new StockAdjustmentWindow(selectedItem.Id, selectedItem.Name, selectedItem.StockQuantity);
                if (adjustmentWindow.ShowDialog() == true)
                {
                    LoadData(); // Refresh data after adjustment
                }
            }
        }
        
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Eksporto stoqet si Excel",
                    FileName = $"Stoqet_{DateTime.Now:yyyyMMdd}.xlsx"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Stoqet");
                    
                    // Headers
                    worksheet.Cell(1, 1).Value = "ID";
                    worksheet.Cell(1, 2).Value = "Barkodi";
                    worksheet.Cell(1, 3).Value = "Emri";
                    worksheet.Cell(1, 4).Value = "Njësia";
                    worksheet.Cell(1, 5).Value = "Çmimi";
                    worksheet.Cell(1, 6).Value = "Stoku";
                    worksheet.Cell(1, 7).Value = "Hyrje";
                    worksheet.Cell(1, 8).Value = "Dalje";
                    worksheet.Cell(1, 9).Value = "Kategoria";
                    
                    var headerRange = worksheet.Range(1, 1, 1, 9);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    
                    int row = 2;
                    foreach (var item in _stockItems)
                    {
                        worksheet.Cell(row, 1).Value = item.Id;
                        worksheet.Cell(row, 2).Value = item.Barcode;
                        worksheet.Cell(row, 3).Value = item.Name;
                        worksheet.Cell(row, 4).Value = item.Unit;
                        worksheet.Cell(row, 5).Value = item.SalesPrice;
                        worksheet.Cell(row, 6).Value = item.StockQuantity;
                        worksheet.Cell(row, 7).Value = item.StockIn;
                        worksheet.Cell(row, 8).Value = item.StockOut;
                        worksheet.Cell(row, 9).Value = item.Category;
                        row++;
                    }
                    
                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(saveFileDialog.FileName);
                    
                    MessageBox.Show("Eksportimi u krye me sukses!", "Sukses", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë eksportimit: {ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
    
    /// <summary>
    /// Simple dialog for adjusting stock quantities
    /// </summary>
    public class StockAdjustmentWindow : Window
    {
        private readonly int _articleId;
        private readonly TextBox _quantityBox;
        private readonly RadioButton _addRadio;
        private readonly RadioButton _subtractRadio;
        private readonly RadioButton _setRadio;
        
        public StockAdjustmentWindow(int articleId, string articleName, decimal currentStock)
        {
            _articleId = articleId;
            
            Title = "Ndryshimi i stoqes";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = System.Windows.Media.Brushes.White;
            
            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Article info
            var infoText = new TextBlock
            {
                Text = $"Artikulli: {articleName}\nStoku aktual: {currentStock:N0}",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(infoText, 0);
            mainGrid.Children.Add(infoText);
            
            // Operation type
            var operationLabel = new TextBlock { Text = "Operacioni:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(operationLabel, 1);
            mainGrid.Children.Add(operationLabel);
            
            var radioPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            _addRadio = new RadioButton { Content = "Shto", IsChecked = true, Margin = new Thickness(0, 0, 15, 0) };
            _subtractRadio = new RadioButton { Content = "Zbrit", Margin = new Thickness(0, 0, 15, 0) };
            _setRadio = new RadioButton { Content = "Vendos", Margin = new Thickness(0, 0, 15, 0) };
            radioPanel.Children.Add(_addRadio);
            radioPanel.Children.Add(_subtractRadio);
            radioPanel.Children.Add(_setRadio);
            Grid.SetRow(radioPanel, 2);
            mainGrid.Children.Add(radioPanel);
            
            // Quantity
            var quantityLabel = new TextBlock { Text = "Sasia:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(quantityLabel, 3);
            mainGrid.Children.Add(quantityLabel);
            
            _quantityBox = new TextBox { Height = 35, FontSize = 16, Padding = new Thickness(8, 5, 8, 5) };
            Grid.SetRow(_quantityBox, 4);
            mainGrid.Children.Add(_quantityBox);
            
            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            
            var cancelBtn = new Button
            {
                Content = "Anulo",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = System.Windows.Media.Brushes.Gray,
                Foreground = System.Windows.Media.Brushes.White
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            
            var saveBtn = new Button
            {
                Content = "Ruaj",
                Width = 100,
                Height = 35,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold
            };
            saveBtn.Click += SaveBtn_Click;
            
            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(saveBtn);
            Grid.SetRow(buttonPanel, 6);
            mainGrid.Children.Add(buttonPanel);
            
            Content = mainGrid;
            _quantityBox.Focus();
        }
        
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            _quantityBox.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
            _quantityBox.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);

            if (!decimal.TryParse(_quantityBox.Text, out decimal quantity) || quantity < 0)
            {
                _quantityBox.BorderBrush = System.Windows.Media.Brushes.Red;
                _quantityBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Sasia' duhet të jetë numër pozitiv.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _quantityBox.Focus();
                return;
            }
            
            try
            {
                using var context = new POSDbContext();
                
                if (POSDbContext.UseSqlServer)
                {
                    var article = context.Artikujt.FirstOrDefault(a => a.Id == _articleId);
                    if (article != null)
                    {
                        if (_addRadio.IsChecked == true)
                        {
                            article.Sasia = (article.Sasia ?? 0) + (double)quantity;
                            article.SasiaHyrje = (article.SasiaHyrje ?? 0) + (double)quantity;
                        }
                        else if (_subtractRadio.IsChecked == true)
                        {
                            article.Sasia = Math.Max(0, (article.Sasia ?? 0) - (double)quantity);
                            article.SasiaDalje = (article.SasiaDalje ?? 0) + (double)quantity;
                        }
                        else // Set
                        {
                            article.Sasia = (double)quantity;
                        }
                        context.SaveChanges();
                    }
                }
                else
                {
                    var article = context.Articles.FirstOrDefault(a => a.Id == _articleId);
                    if (article != null)
                    {
                        if (_addRadio.IsChecked == true)
                        {
                            article.StockQuantity += quantity;
                            article.StockIn += quantity;
                        }
                        else if (_subtractRadio.IsChecked == true)
                        {
                            article.StockQuantity = Math.Max(0, article.StockQuantity - quantity);
                            article.StockOut += quantity;
                        }
                        else // Set
                        {
                            article.StockQuantity = quantity;
                        }
                        context.SaveChanges();
                    }
                }
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
