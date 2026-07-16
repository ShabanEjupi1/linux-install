using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace KosovaPOS.Windows
{
    public partial class ArticlesWindow : Window
    {
        private ObservableCollection<Article> _articles = new ObservableCollection<Article>();
        private List<Article> _allArticles = new List<Article>(); // Store all articles for filtering
        
        public ArticlesWindow()
        {
            InitializeComponent();
            LoadArticles();
            LoadCategories();
        }
        
        private void LoadCategories()
        {
            try
            {
                // Get unique categories from articles
                var categories = _allArticles
                    .Select(a => a.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();
                
                PrintCategoryComboBox.Items.Clear();
                PrintCategoryComboBox.Items.Add(""); // Empty option for all
                foreach (var category in categories)
                {
                    PrintCategoryComboBox.Items.Add(category);
                }
                
                // Also populate the filter combobox
                CategoryFilterComboBox.Items.Clear();
                CategoryFilterComboBox.Items.Add("🔹 Të gjitha"); // All option
                foreach (var category in categories)
                {
                    CategoryFilterComboBox.Items.Add(category);
                }
                CategoryFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception)
            {
                // Ignore errors loading categories
            }
        }
        
        private void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryFilterComboBox.SelectedIndex <= 0 || CategoryFilterComboBox.SelectedItem == null)
            {
                // Show all articles
                FilterArticles();
                return;
            }
            
            string selectedCategory = CategoryFilterComboBox.SelectedItem.ToString();
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? string.Empty;
            
            // Filter by both search text and category
            var filtered = _allArticles.Where(a =>
            {
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                    (a.Name?.ToLower().Contains(searchText) ?? false) ||
                    (a.Barcode?.ToLower().Contains(searchText) ?? false) ||
                    (a.Category?.ToLower().Contains(searchText) ?? false) ||
                    (a.Supplier?.ToLower().Contains(searchText) ?? false);
                    
                bool matchesCategory = a.Category == selectedCategory;
                
                return matchesSearch && matchesCategory;
            }).ToList();
            
            _articles = new ObservableCollection<Article>(filtered);
            ArticlesDataGrid.ItemsSource = _articles;
            UpdateResultCount(_articles.Count, _allArticles.Count);
        }
        
        private void ViewStockButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lowStock = _allArticles.Where(a => a.StockQuantity <= 5 && a.StockQuantity > 0).OrderBy(a => a.StockQuantity).ToList();
                var outOfStock = _allArticles.Where(a => a.StockQuantity <= 0).ToList();
                var inStock = _allArticles.Count(a => a.StockQuantity > 5);

                var message = $"📊 GJENDJA E STOKUT\n" +
                              $"{'─',40}\n\n" +
                              $"✅ Artikuj në stok (>5):       {inStock}\n" +
                              $"⚠️  Stok i ulët (1-5):          {lowStock.Count}\n" +
                              $"❌ Jashtë stoku (0):            {outOfStock.Count}\n" +
                              $"📦 Total artikuj:              {_allArticles.Count}\n";

                if (lowStock.Any())
                {
                    message += $"\n⚠\t&#xf071; Artikujt me stok të ulët:\n";
                    foreach (var a in lowStock.Take(10))
                        message += $"  • {a.Name} → Stok: {a.StockQuantity:F0}\n";
                    if (lowStock.Count > 10) message += $"  ... dhe {lowStock.Count - 10} të tjerë\n";
                }

                if (outOfStock.Any())
                {
                    message += $"\n❌ Jashtë stoku:\n";
                    foreach (var a in outOfStock.Take(10))
                        message += $"  • {a.Name}\n";
                    if (outOfStock.Count > 10) message += $"  ... dhe {outOfStock.Count - 10} të tjerë\n";
                }

                MessageBox.Show(message, "Gjendja e Stokut", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të stokut:\n\n{ex.Message}",
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ArticlesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double-click to edit article
            if (ArticlesDataGrid.SelectedItem is Article selectedArticle)
            {
                EditButton_Click(sender, e);
            }
        }
        
        private void LoadArticles()
        {
            try
            {
                // Use ArticleDataService which handles both SQLite and SQL Server
                _allArticles = ArticleDataService.GetAllArticles();
                _articles = new ObservableCollection<Article>(_allArticles);
                ArticlesDataGrid.ItemsSource = _articles;
                
                // Update status
                UpdateResultCount(_allArticles.Count, _allArticles.Count);
                this.Title = $"Artikujt - {_allArticles.Count} artikuj";
                
                // Update header statistics
                if (HeaderCountText != null)
                    HeaderCountText.Text = $"{_allArticles.Count} artikuj të gjithsej";
                if (TotalArticlesText != null)
                    TotalArticlesText.Text = _allArticles.Count.ToString();
                if (InStockText != null)
                    InStockText.Text = _allArticles.Count(a => a.StockQuantity > 0).ToString();
                
                // Clear search when refreshing
                if (SearchTextBox != null)
                    SearchTextBox.Text = string.Empty;
                
                // Reload categories
                LoadCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të artikujve:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Initialize empty collection to prevent further errors
                _allArticles = new List<Article>();
                _articles = new ObservableCollection<Article>();
                ArticlesDataGrid.ItemsSource = _articles;
            }
        }
        
        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            FilterArticles();
        }
        
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            FilterArticles();
            SearchTextBox.Focus();
        }
        
        private void FilterArticles()
        {
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? string.Empty;
            
            if (string.IsNullOrEmpty(searchText))
            {
                // Show all articles
                _articles = new ObservableCollection<Article>(_allArticles);
            }
            else
            {
                // Filter by name, barcode, category, or supplier
                var filtered = _allArticles.Where(a =>
                    (a.Name?.ToLower().Contains(searchText) ?? false) ||
                    (a.Barcode?.ToLower().Contains(searchText) ?? false) ||
                    (a.Category?.ToLower().Contains(searchText) ?? false) ||
                    (a.Supplier?.ToLower().Contains(searchText) ?? false)
                ).ToList();
                
                _articles = new ObservableCollection<Article>(filtered);
            }
            
            ArticlesDataGrid.ItemsSource = _articles;
            UpdateResultCount(_articles.Count, _allArticles.Count);
        }
        
        private void UpdateResultCount(int shown, int total)
        {
            if (ResultCountText != null)
            {
                if (shown == total)
                {
                    ResultCountText.Text = $"Duke shfaqur {total} artikuj";
                }
                else
                {
                    ResultCountText.Text = $"Duke shfaqur {shown} nga {total} artikuj";
                }
            }
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Ensure window is fully loaded before handling keyboard shortcuts
            if (!IsLoaded) return;
            
            // Handle Ctrl modifiers first
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.F: // Ctrl+F for search (universal shortcut)
                        SearchTextBox?.Focus();
                        SearchTextBox?.SelectAll();
                        e.Handled = true;
                        return;
                }
            }
            
            // Escape to clear search
            if (e.Key == Key.Escape && SearchTextBox != null && !string.IsNullOrEmpty(SearchTextBox.Text))
            {
                SearchTextBox.Text = string.Empty;
                e.Handled = true;
                return;
            }
            
            // Handle Up/Down arrows for DataGrid navigation
            if (e.Key == Key.Down && ArticlesDataGrid.SelectedIndex < ArticlesDataGrid.Items.Count - 1)
            {
                ArticlesDataGrid.SelectedIndex++;
                ArticlesDataGrid.ScrollIntoView(ArticlesDataGrid.SelectedItem);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up && ArticlesDataGrid.SelectedIndex > 0)
            {
                ArticlesDataGrid.SelectedIndex--;
                ArticlesDataGrid.ScrollIntoView(ArticlesDataGrid.SelectedItem);
                e.Handled = true;
                return;
            }
            
            // Enter to print barcode for selected article
            if (e.Key == Key.Enter && ArticlesDataGrid.SelectedItem != null)
            {
                BarcodeButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            
            // Handle Alt key shortcuts
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
            {
                switch (e.SystemKey) // Use SystemKey for Alt combinations
                {
                    case Key.K: // Alt+K for search (Kërko)
                        SearchTextBox?.Focus();
                        SearchTextBox?.SelectAll();
                        e.Handled = true;
                        break;
                    case Key.F:
                        if (ArticlesDataGrid.SelectedItem != null)
                            DeleteButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.E:
                        ExcelButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.R:
                        RefreshButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.N:
                        EditButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.O:
                        AddButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.B: // Alt+B for barcode printing
                        BarcodeButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.M:
                        CloseButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                }
            }
        }
        
        private void ListOfArticles_Click(object sender, RoutedEventArgs e)
        {
            LoadArticles();
        }
        
        private void PrintListButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_articles == null || _articles.Count == 0)
                {
                    MessageBox.Show("Nuk ka artikuj për të printuar!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                PrintArticlesList(_articles.ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private System.Windows.Documents.TableCell CreateTableCell(string text)
        {
            var paragraph = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text));
            paragraph.Margin = new Thickness(3);
            var cell = new System.Windows.Documents.TableCell(paragraph);
            cell.BorderBrush = System.Windows.Media.Brushes.Gray;
            cell.BorderThickness = new Thickness(0.5);
            return cell;
        }
        
        private string TruncateForPrint(string? text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 2) + "..";
        }
        
        private void PrintByCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedCategory = PrintCategoryComboBox.Text?.Trim();
                
                if (string.IsNullOrWhiteSpace(selectedCategory))
                {
                    MessageBox.Show("Ju lutem zgjidhni një kategori për printim!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Filter articles by category
                var articlesToPrint = _articles
                    .Where(a => string.Equals(a.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (articlesToPrint.Count == 0)
                {
                    MessageBox.Show($"Nuk ka artikuj në kategorinë '{selectedCategory}'!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                PrintArticlesList(articlesToPrint, $"Kategoria: {selectedCategory} ({articlesToPrint.Count} artikuj)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PrintSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected articles from DataGrid
                var selectedArticles = ArticlesDataGrid.SelectedItems.Cast<Article>().ToList();
                
                if (selectedArticles.Count == 0)
                {
                    MessageBox.Show("Ju lutem zgjidhni artikuj për printim!\n\nMbani CTRL ose SHIFT për të zgjedhur shumë artikuj.", 
                        "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                PrintArticlesList(selectedArticles, $"Seleksioni ({selectedArticles.Count} artikuj)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PrintArticlesList(List<Article> articles, string? subtitle = null)
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                // Create a FlowDocument for printing
                var document = new System.Windows.Documents.FlowDocument();
                document.PageWidth = printDialog.PrintableAreaWidth;
                document.PageHeight = printDialog.PrintableAreaHeight;
                document.PagePadding = new Thickness(40);
                document.ColumnWidth = double.MaxValue;
                
                // Title
                var title = new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run("Lista e Artikujve"))
                {
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center
                };
                document.Blocks.Add(title);
                
                // Subtitle with date and count
                var subtitleText = $"Data: {DateTime.Now:dd/MM/yyyy HH:mm}  |  Gjithsej: {articles.Count} artikuj";
                if (!string.IsNullOrEmpty(subtitle))
                    subtitleText += $"  |  {subtitle}";
                    
                var subtitlePara = new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run(subtitleText))
                {
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                document.Blocks.Add(subtitlePara);
                
                // Create table
                var table = new System.Windows.Documents.Table();
                table.CellSpacing = 0;
                table.BorderBrush = System.Windows.Media.Brushes.Black;
                table.BorderThickness = new Thickness(1);
                
                // Define columns
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(40) });  // Nr
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(100) }); // Barkodi
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(180) }); // Emri
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(50) });  // Njësia
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(80) });  // Çmimi
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(60) });  // Stoku
                
                var rowGroup = new System.Windows.Documents.TableRowGroup();
                table.RowGroups.Add(rowGroup);
                
                // Header row
                var headerRow = new System.Windows.Documents.TableRow();
                headerRow.Background = System.Windows.Media.Brushes.LightGray;
                headerRow.FontWeight = FontWeights.Bold;
                headerRow.FontSize = 10;
                
                headerRow.Cells.Add(CreateTableCell("Nr"));
                headerRow.Cells.Add(CreateTableCell("Barkodi"));
                headerRow.Cells.Add(CreateTableCell("Emri i Artikullit"));
                headerRow.Cells.Add(CreateTableCell("Njësia"));
                headerRow.Cells.Add(CreateTableCell("Çmimi"));
                headerRow.Cells.Add(CreateTableCell("Stoku"));
                rowGroup.Rows.Add(headerRow);
                
                // Data rows
                int rowNum = 1;
                foreach (var article in articles)
                {
                    var row = new System.Windows.Documents.TableRow();
                    row.FontSize = 9;
                    
                    row.Cells.Add(CreateTableCell(rowNum.ToString()));
                    row.Cells.Add(CreateTableCell(article.Barcode ?? ""));
                    row.Cells.Add(CreateTableCell(TruncateForPrint(article.Name, 30)));
                    row.Cells.Add(CreateTableCell(article.Unit ?? ""));
                    row.Cells.Add(CreateTableCell($"{article.SalesPrice:N2} €"));
                    row.Cells.Add(CreateTableCell($"{article.StockQuantity:N0}"));
                    rowGroup.Rows.Add(row);
                    rowNum++;
                }
                
                document.Blocks.Add(table);
                
                // Print
                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator;
                printDialog.PrintDocument(paginator, "Lista e Artikujve");
            }
        }
        
        private void ExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Ruaj listën si Excel",
                    FileName = $"Artikujt_{DateTime.Now:yyyyMMdd}.xlsx"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Artikujt");
                    
                    // Headers
                    worksheet.Cell(1, 1).Value = "Barkodi";
                    worksheet.Cell(1, 2).Value = "Emri i Artikullit";
                    worksheet.Cell(1, 3).Value = "Njësia";
                    worksheet.Cell(1, 4).Value = "Paketa";
                    worksheet.Cell(1, 5).Value = "Çmimi i Shitjes";
                    worksheet.Cell(1, 6).Value = "Kategoria";
                    worksheet.Cell(1, 7).Value = "Furnizuesi";
                    worksheet.Cell(1, 8).Value = "Stoku";
                    
                    // Style headers
                    var headerRange = worksheet.Range(1, 1, 1, 8);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    
                    // Data
                    int row = 2;
                    foreach (var article in _articles)
                    {
                        worksheet.Cell(row, 1).Value = article.Barcode;
                        worksheet.Cell(row, 2).Value = article.Name;
                        worksheet.Cell(row, 3).Value = article.Unit;
                        worksheet.Cell(row, 4).Value = article.Pack;
                        worksheet.Cell(row, 5).Value = article.SalesPrice;
                        worksheet.Cell(row, 6).Value = article.Category;
                        worksheet.Cell(row, 7).Value = article.Supplier;
                        worksheet.Cell(row, 8).Value = article.StockQuantity;
                        row++;
                    }
                    
                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();
                    
                    workbook.SaveAs(saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë eksportimit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadArticles();
        }
        
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has permission to manage articles
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanManageArticles)
            {
                MessageBox.Show("Nuk keni të drejta për të fshirë artikuj!\nKontaktoni administratorin për të dhënë të drejta.", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (ArticlesDataGrid.SelectedItem is Article selectedArticle)
            {
                var result = MessageBox.Show($"A jeni i sigurt që doni ta fshini artikullin '{selectedArticle.Name}'?", 
                    "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Use ArticleDataService for deletion
                    ArticleDataService.DeleteArticle(selectedArticle.Id);
                    LoadArticles();
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për ta fshirë!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has permission to manage articles
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanManageArticles)
            {
                MessageBox.Show("Nuk keni të drejta për të ndryshuar artikuj!\nKontaktoni administratorin për të dhënë të drejta.", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (ArticlesDataGrid.SelectedItem is Article selectedArticle)
            {
                // Store current state before editing
                string currentSearch = SearchTextBox?.Text ?? "";
                int selectedIndex = ArticlesDataGrid.SelectedIndex;
                var scrollViewer = GetScrollViewer(ArticlesDataGrid);
                double scrollOffset = scrollViewer?.VerticalOffset ?? 0;
                
                var editWindow = new ArticleEditWindow(selectedArticle);
                if (editWindow.ShowDialog() == true)
                {
                    // Refresh article data silently without resetting UI
                    RefreshArticlesSilently(currentSearch, selectedArticle.Id, scrollOffset);
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për ta ndryshuar!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has permission to manage articles
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanManageArticles)
            {
                MessageBox.Show("Nuk keni të drejta për të shtuar artikuj!\nKontaktoni administratorin për të dhënë të drejta.", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Store current state before adding
            string currentSearch = SearchTextBox?.Text ?? "";
            var scrollViewer = GetScrollViewer(ArticlesDataGrid);
            double scrollOffset = scrollViewer?.VerticalOffset ?? 0;
            
            var addWindow = new ArticleEditWindow(null);
            if (addWindow.ShowDialog() == true)
            {
                // Refresh article data silently without resetting UI
                RefreshArticlesSilently(currentSearch, -1, scrollOffset);
            }
        }
        
        /// <summary>
        /// Refresh articles silently without resetting search or scroll position
        /// </summary>
        private void RefreshArticlesSilently(string searchText, int selectedArticleId, double scrollOffset)
        {
            try
            {
                // Use ArticleDataService which handles both SQLite and SQL Server
                _allArticles = ArticleDataService.GetAllArticles();
                
                // Reapply filter if search was active
                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchLower = searchText.ToLower();
                    var filtered = _allArticles.Where(a =>
                        (a.Name?.ToLower().Contains(searchLower) ?? false) ||
                        (a.Barcode?.ToLower().Contains(searchLower) ?? false) ||
                        (a.Category?.ToLower().Contains(searchLower) ?? false) ||
                        (a.Supplier?.ToLower().Contains(searchLower) ?? false)
                    ).ToList();
                    _articles = new ObservableCollection<Article>(filtered);
                }
                else
                {
                    _articles = new ObservableCollection<Article>(_allArticles);
                }
                
                ArticlesDataGrid.ItemsSource = _articles;
                UpdateResultCount(_articles.Count, _allArticles.Count);
                this.Title = $"Artikujt - {_allArticles.Count} artikuj";
                
                // Try to restore selection to the edited/same article
                if (selectedArticleId > 0)
                {
                    var articleToSelect = _articles.FirstOrDefault(a => a.Id == selectedArticleId);
                    if (articleToSelect != null)
                    {
                        ArticlesDataGrid.SelectedItem = articleToSelect;
                        ArticlesDataGrid.ScrollIntoView(articleToSelect);
                    }
                }
                
                // Restore scroll position
                var scrollViewer = GetScrollViewer(ArticlesDataGrid);
                if (scrollViewer != null && scrollOffset > 0)
                {
                    // Need to dispatch to ensure layout is complete
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollOffset);
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë rifreskimit:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private System.Threading.CancellationTokenSource? _barcodePrintingCts;
        private bool _isPrintingBarcodes = false;
        
        private async void BarcodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPrintingBarcodes)
            {
                // Cancel ongoing printing
                _barcodePrintingCts?.Cancel();
                return;
            }
            
            if (ArticlesDataGrid.SelectedItem is Article selectedArticle)
            {
                if (int.TryParse(BarcodeCopiesTextBox.Text, out int copies) && copies > 0 && copies <= 1000)
                {
                    // Store current scroll position and selected index
                    int selectedIndex = ArticlesDataGrid.SelectedIndex;
                    var scrollViewer = GetScrollViewer(ArticlesDataGrid);
                    double scrollOffset = scrollViewer?.VerticalOffset ?? 0;
                    string currentSearch = SearchTextBox?.Text ?? "";
                    
                    try
                    {
                        _isPrintingBarcodes = true;
                        _barcodePrintingCts = new System.Threading.CancellationTokenSource();
                        
                        // Update button to show cancel option
                        BarcodeButton.Content = "⛔ Ndalo printimin";
                        BarcodeButton.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(244, 67, 54));
                        
                        var barcodePrinter = new Services.BarcodePrinterService();
                        
                        // Print barcodes one by one with progress
                        for (int i = 0; i < copies; i++)
                        {
                            if (_barcodePrintingCts.Token.IsCancellationRequested)
                            {
                                MessageBox.Show($"Printimi u ndal!\n\nU printuan {i} nga {copies} barkoda.", 
                                    "Printimi u ndal", MessageBoxButton.OK, MessageBoxImage.Information);
                                break;
                            }
                            
                            // Update progress in title or status
                            this.Title = $"Artikujt - Duke printuar barkodën {i + 1}/{copies}...";
                            
                            // Print single barcode
                            barcodePrinter.PrintBarcode(selectedArticle, 1);
                            
                            // Small delay to allow UI update and cancellation check
                            await System.Threading.Tasks.Task.Delay(50, _barcodePrintingCts.Token);
                        }
                        
                        if (!_barcodePrintingCts.Token.IsCancellationRequested)
                        {
                            // All printed successfully - no message for faster workflow
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        // Printing was cancelled - already showed message above
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjatë printimit të barkodës:\n\n{ex.Message}", 
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        _isPrintingBarcodes = false;
                        _barcodePrintingCts?.Dispose();
                        _barcodePrintingCts = null;
                        
                        // Reset button appearance to original
                        BarcodeButton.Content = "Alt+B - Printo barkod";
                        BarcodeButton.ClearValue(Button.BackgroundProperty);
                        
                        // Reset copies to 1 after printing for next use
                        BarcodeCopiesTextBox.Text = "1";
                        
                        // Restore scroll position and selection
                        this.Title = $"Artikujt - {_allArticles.Count} artikuj";
                        
                        // Restore search text if it was cleared
                        if (SearchTextBox != null && SearchTextBox.Text != currentSearch)
                        {
                            SearchTextBox.Text = currentSearch;
                        }
                        
                        // Restore selection and scroll position
                        if (selectedIndex >= 0 && selectedIndex < ArticlesDataGrid.Items.Count)
                        {
                            ArticlesDataGrid.SelectedIndex = selectedIndex;
                            ArticlesDataGrid.ScrollIntoView(ArticlesDataGrid.Items[selectedIndex]);
                        }
                        
                        if (scrollViewer != null)
                        {
                            scrollViewer.ScrollToVerticalOffset(scrollOffset);
                        }
                        
                        // Deselect the article after printing so user must actively select next one
                        ArticlesDataGrid.SelectedItem = null;
                        
                        // Focus back to search for next lookup
                        SearchTextBox?.Focus();
                    }
                }
                else
                {
                    MessageBox.Show("Ju lutem shkruani një numër valid (1-1000) të kopjeve!", 
                        "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BarcodeCopiesTextBox.Focus();
                    BarcodeCopiesTextBox.SelectAll();
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për të printuar barkodën!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private System.Windows.Controls.ScrollViewer? GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is System.Windows.Controls.ScrollViewer sv) return sv;
            
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
        
        private void BarcodeCopiesTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numeric input
            e.Handled = !int.TryParse(e.Text, out _);
        }
        
        private void BarcodeCopiesTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Up/Down arrows to change quantity
            if (e.Key == Key.Up)
            {
                IncreaseCopiesButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                DecreaseCopiesButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }
        
        private void IncreaseCopiesButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(BarcodeCopiesTextBox.Text, out int copies))
            {
                if (copies < 1000)
                {
                    BarcodeCopiesTextBox.Text = (copies + 1).ToString();
                }
            }
            else
            {
                BarcodeCopiesTextBox.Text = "1";
            }
        }
        
        private void DecreaseCopiesButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(BarcodeCopiesTextBox.Text, out int copies))
            {
                if (copies > 1)
                {
                    BarcodeCopiesTextBox.Text = (copies - 1).ToString();
                }
            }
            else
            {
                BarcodeCopiesTextBox.Text = "1";
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private async void WebSyncButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WebSyncButton.IsEnabled = false;
                WebSyncButton.Content = "🔄 Duke sinkronizuar...";
                
                var syncService = new Services.WebStoreSyncService();
                int syncedCount = await syncService.SyncPhotosFromWebAsync();
                
                if (syncedCount > 0)
                {
                    MessageBox.Show($"U sinkronizuan {syncedCount} foto nga web store!", 
                        "Sinkronizimi përfundoi", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadArticles(); // Refresh to show new photos
                }
                else
                {
                    MessageBox.Show("Nuk ka foto të reja për të sinkronizuar.", 
                        "Sinkronizimi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë sinkronizimit:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                WebSyncButton.IsEnabled = true;
                WebSyncButton.Content = "🌐 Sinkronizo Web";
            }
        }

        private async void WebPushButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WebPushButton.IsEnabled = false;
                WebPushButton.Content = "⏳ Duke kontrolluar...";
                
                var syncService = new Services.WebStoreSyncService();
                
                // First, preview what would be synced
                var preview = await syncService.PreviewSyncToWebStoreAsync();
                
                if (!preview.Success)
                {
                    MessageBox.Show($"Gabim gjatë kontrollimit:\n\n{preview.ErrorMessage}", 
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Show confirmation dialog with changes
                var message = $"📊 Përmbledhje e sinkronizimit:\n\n" +
                              $"• Produkte totale: {preview.TotalProducts}\n" +
                              $"• Produkte të reja: {preview.NewProducts}\n" +
                              $"• Produkte që do të përditësohen: {preview.UpdatedProducts}\n" +
                              $"• Pa ndryshime: {preview.UnchangedProducts}\n\n";
                
                if (preview.Changes.Count > 0 && preview.Changes.Count <= 10)
                {
                    message += "Ndryshimet:\n";
                    foreach (var change in preview.Changes.Take(10))
                    {
                        message += $"  • {change.Name}: {string.Join(", ", change.Changes ?? new List<string>())}\n";
                    }
                    if (preview.Changes.Count > 10)
                    {
                        message += $"  ... dhe {preview.Changes.Count - 10} të tjerë\n";
                    }
                }
                
                message += "\nDëshironi të vazhdoni me sinkronizimin?";
                
                var result = MessageBox.Show(message, "Konfirmoni Sinkronizimin", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // Perform the actual sync
                WebPushButton.Content = "📤 Duke dërguar...";
                var syncResult = await syncService.PushProductsToWebStoreAsync();
                
                if (syncResult.Success)
                {
                    MessageBox.Show($"✅ Sinkronizimi përfundoi me sukses!\n\n" +
                                    $"• Shtuar: {syncResult.SyncedCount}\n" +
                                    $"• Përditësuar: {syncResult.UpdatedCount}\n" +
                                    $"• Gabime: {syncResult.ErrorCount}\n" +
                                    $"• Totali: {syncResult.TotalCount}", 
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"❌ Gabim gjatë sinkronizimit:\n\n{syncResult.Message}", 
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë dërgimit:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                WebPushButton.IsEnabled = true;
                WebPushButton.Content = "📤 Dërgo në Web";
            }
        }
    }
}
