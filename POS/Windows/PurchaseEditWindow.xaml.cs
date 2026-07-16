using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class PurchaseEditWindow : Window
    {
        private Purchase? _purchase;
        private ObservableCollection<PurchaseItemDisplay> _items = new ObservableCollection<PurchaseItemDisplay>();
        
        public PurchaseEditWindow(Purchase? purchase = null)
        {
            InitializeComponent();
            _purchase = purchase;
            
            LoadComboBoxes();
            
            ItemsDataGrid.ItemsSource = _items;
            
            // Subscribe to collection changes for real-time updates
            _items.CollectionChanged += (s, e) => UpdateTotals();
            
            if (_purchase != null)
            {
                Title = "Ndrysho blerjen";
                LoadPurchaseData();
            }
            else
            {
                DatePicker.SelectedDate = DateTime.Now;
                DocumentNumberTextBox.Text = GenerateDocumentNumber();
            }
        }
        
        private void LoadComboBoxes()
        {
            using var context = new POSDbContext();

            // Load suppliers from FurnitoriNew table (SQL Server only)
            var furnitoiret = context.FurnitoriNew
                .Where(f => f.F == true || f.Emri != null)
                .OrderBy(f => f.Emri)
                .ToList();

            var supplierDisplayList = furnitoiret.Select(f => new BusinessPartner
            {
                Id = (int)f.Id,
                Name = f.Emri ?? $"Furnitor {f.Id}",
                NRF = f.NRF,
                NUI = f.NIT,
                Address = f.Adresa,
                Phone = f.Telefoni,
                Email = f.Email,
                PartnerType = (f.F == true && f.K == true) ? "Të dy" :
                              (f.F == true) ? "Furnizues" :
                              (f.K == true) ? "Klient" : "Furnizues",
                IsActive = true
            }).ToList();
            SupplierComboBox.ItemsSource = supplierDisplayList;

            _allArticles = ArticleDataService.GetAllArticles();
            ArticleComboBox.ItemsSource = _allArticles;

            // Purchase types
            PurchaseTypeComboBox.ItemsSource = new List<string> { "Vendore", "Import", "Shpenzime" };
            PurchaseTypeComboBox.SelectedIndex = 0;
        }
        
        // Store all articles for filtering
        private List<Article> _allArticles = new List<Article>();
        private System.Windows.Threading.DispatcherTimer? _searchTimer;
        private bool _isUserTyping = false;
        
        private void ArticleComboBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            _isUserTyping = true;
            
            // Start/reset debounce timer for search - 2 second delay to allow completing typing
            if (_searchTimer == null)
            {
                _searchTimer = new System.Windows.Threading.DispatcherTimer();
                _searchTimer.Interval = TimeSpan.FromMilliseconds(2000); // 2 second delay for better typing experience
                _searchTimer.Tick += (s, args) =>
                {
                    _searchTimer.Stop();
                    // Only filter if still typing mode - do NOT auto-select
                    if (_isUserTyping)
                    {
                        FilterArticlesWithoutSelection();
                    }
                };
            }
            _searchTimer.Stop();
            _searchTimer.Start();
        }
        
        // Filter articles without auto-selecting any item
        private void FilterArticlesWithoutSelection()
        {
            var searchText = ArticleComboBox.Text?.Trim().ToLower() ?? "";
            
            // Remember current text
            var currentText = ArticleComboBox.Text;
            var currentCaretIndex = 0;
            
            // Get the inner textbox to preserve cursor position
            var textBox = ArticleComboBox.Template?.FindName("PART_EditableTextBox", ArticleComboBox) as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                currentCaretIndex = textBox.CaretIndex;
            }
            
            if (string.IsNullOrEmpty(searchText))
            {
                ArticleComboBox.ItemsSource = _allArticles;
            }
            else
            {
                var filtered = _allArticles.Where(a =>
                    (a.Name?.ToLower().Contains(searchText) ?? false) ||
                    (a.Barcode?.ToLower().Contains(searchText) ?? false))
                    .Take(50)
                    .ToList();
                
                ArticleComboBox.ItemsSource = filtered;
            }
            
            // Open dropdown but NEVER auto-select
            ArticleComboBox.IsDropDownOpen = true;
            ArticleComboBox.SelectedIndex = -1;  // Always clear selection
            ArticleComboBox.Text = currentText;  // Restore typed text
            
            // Restore cursor position
            if (textBox != null)
            {
                textBox.CaretIndex = Math.Min(currentCaretIndex, textBox.Text?.Length ?? 0);
            }
        }
        
        private void ArticleComboBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Arrow keys and Tab are handled by PreviewKeyDown - don't interfere
            if (e.Key == System.Windows.Input.Key.Down || 
                e.Key == System.Windows.Input.Key.Up ||
                e.Key == System.Windows.Input.Key.Tab)
            {
                return;
            }
            
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _isUserTyping = false;
                _searchTimer?.Stop();
                
                if (ArticleComboBox.SelectedItem != null)
                {
                    // User confirmed selection with Enter - fill prices and move to next field
                    if (ArticleComboBox.SelectedItem is Article selectedArticle)
                    {
                        PurchasePriceTextBox.Text = selectedArticle.PurchasePrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        SalesPriceTextBox.Text = selectedArticle.SalesPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    ArticleComboBox.IsDropDownOpen = false;
                    QuantityTextBox.Focus();
                    QuantityTextBox.SelectAll();
                    e.Handled = true;
                }
                return;
            }
            
            // Reset the timer on any keystroke to allow user to finish typing
            _isUserTyping = true;
            if (_searchTimer != null)
            {
                _searchTimer.Stop();
                _searchTimer.Start();
            }
        }
        
        // Improve arrow key navigation for selecting articles in dropdown
        private void ArticleComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!ArticleComboBox.IsDropDownOpen && 
                (e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Up))
            {
                // Open dropdown if it's closed
                ArticleComboBox.IsDropDownOpen = true;
                e.Handled = true;
                return;
            }
            
            if (ArticleComboBox.IsDropDownOpen)
            {
                var itemsSource = ArticleComboBox.ItemsSource as System.Collections.IList;
                if (itemsSource == null || itemsSource.Count == 0) return;
                
                int currentIndex = ArticleComboBox.SelectedIndex;
                
                if (e.Key == System.Windows.Input.Key.Down)
                {
                    if (currentIndex < itemsSource.Count - 1)
                    {
                        ArticleComboBox.SelectedIndex = currentIndex + 1;
                    }
                    else
                    {
                        ArticleComboBox.SelectedIndex = 0; // Wrap to first
                    }
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Up)
                {
                    if (currentIndex > 0)
                    {
                        ArticleComboBox.SelectedIndex = currentIndex - 1;
                    }
                    else
                    {
                        ArticleComboBox.SelectedIndex = itemsSource.Count - 1; // Wrap to last
                    }
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
                {
                    if (ArticleComboBox.SelectedItem != null)
                    {
                        // User explicitly confirmed selection - now fill the prices
                        _isUserTyping = false;
                        ArticleComboBox.IsDropDownOpen = false;
                        
                        // Fill prices now that user confirmed selection with Enter/Tab
                        if (ArticleComboBox.SelectedItem is Article selectedArticle)
                        {
                            PurchasePriceTextBox.Text = string.Empty;
                            SalesPriceTextBox.Text = string.Empty;
                            PurchasePriceTextBox.Text = selectedArticle.PurchasePrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                            SalesPriceTextBox.Text = selectedArticle.SalesPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        }
                        
                        QuantityTextBox.Focus();
                        QuantityTextBox.SelectAll();
                        e.Handled = true;
                    }
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    ArticleComboBox.IsDropDownOpen = false;
                    _isUserTyping = false;
                    e.Handled = true;
                }
            }
        }
        
        private void FilterArticles()
        {
            var searchText = ArticleComboBox.Text?.Trim().ToLower() ?? "";
            
            // Remember current selection state
            var currentText = ArticleComboBox.Text;
            var currentCaretIndex = 0;
            
            // Get the inner textbox to preserve cursor position
            var textBox = ArticleComboBox.Template?.FindName("PART_EditableTextBox", ArticleComboBox) as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                currentCaretIndex = textBox.CaretIndex;
            }
            
            if (string.IsNullOrEmpty(searchText))
            {
                ArticleComboBox.ItemsSource = _allArticles;
            }
            else
            {
                var filtered = _allArticles.Where(a =>
                    (a.Name?.ToLower().Contains(searchText) ?? false) ||
                    (a.Barcode?.ToLower().Contains(searchText) ?? false))
                    .Take(50)
                    .ToList();
                
                ArticleComboBox.ItemsSource = filtered;
            }
            
            // Open dropdown but DON'T auto-select any item while user is typing
            ArticleComboBox.IsDropDownOpen = true;
            
            // IMPORTANT: Reset selection when user is typing to prevent auto-selection
            if (_isUserTyping)
            {
                ArticleComboBox.SelectedIndex = -1;  // Clear selection while typing
                ArticleComboBox.Text = currentText;  // Restore typed text
                
                // Restore cursor position
                if (textBox != null)
                {
                    textBox.CaretIndex = Math.Min(currentCaretIndex, textBox.Text?.Length ?? 0);
                }
            }
        }
        
        private void ArticleComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Do NOT auto-fill prices here - only highlight the selection in dropdown
            // Prices will be filled when user presses Enter or Tab in PreviewKeyDown handler
            // This prevents unwanted price fills when user is still typing/navigating
        }
        
        // Numeric input validation for price and quantity textboxes
        private void NumericTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox == null) return;
            
            // Allow only digits and one decimal point
            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            
            // Check if the new text would be a valid decimal number
            e.Handled = !IsValidDecimalInput(newText);
        }
        
        private bool IsValidDecimalInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            
            // Count decimal points
            int decimalCount = text.Count(c => c == '.' || c == ',');
            if (decimalCount > 1) return false;
            
            // Try to parse as decimal to validate format
            text = text.Replace(',', '.');
            return decimal.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint, 
                System.Globalization.CultureInfo.InvariantCulture, out _) || 
                text == "." || text.EndsWith(".");
        }
        
        private void NumericTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                // Select all text when gaining focus to make it easy to replace
                textBox.Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
            }
        }
        
        private void LoadPurchaseData()
        {
            if (_purchase == null) return;
            
            DocumentNumberTextBox.Text = _purchase.DocumentNumber;
            DatePicker.SelectedDate = _purchase.Date;
            SupplierComboBox.SelectedValue = _purchase.SupplierId;
            PurchaseTypeComboBox.Text = _purchase.PurchaseType;
            IsPaidCheckBox.IsChecked = _purchase.IsPaid;
            
            using var context = new POSDbContext();
            
            if (POSDbContext.UseSqlServer)
            {
                // SQL Server mode - load from DitariH
                var items = context.DitariH
                    .Where(i => i.NrFatures == _purchase.DocumentNumber)
                    .ToList();
                
                foreach (var item in items)
                {
                    var displayItem = new PurchaseItemDisplay
                    {
                        ArticleId = (int)(item.ArtikullId ?? 0),
                        ArticleName = item.Artikulli ?? "",
                        Quantity = (decimal)(item.Sasia ?? 0),
                        PurchasePrice = (decimal)(item.CmimiFurn ?? 0),
                        SalesPrice = (decimal)(item.CmShitjes ?? 0),
                        RabatPercent = (decimal)(item.RabatiPer ?? 0),
                        VATRate = (decimal)(item.TvshPer ?? 0), // Default to 0% VAT
                        TotalValue = (decimal)(item.VleraMeTvsh ?? item.VleraFurn ?? 0)
                    };
                    
                    // Subscribe with protection against recursion - only recalculate on specific properties
                    displayItem.PropertyChanged += (s, args) => {
                        if (s is PurchaseItemDisplay di && 
                            args.PropertyName != nameof(PurchaseItemDisplay.TotalValue))
                        {
                            di.RecalculateTotal();
                            UpdateTotals();
                        }
                    };
                    
                    _items.Add(displayItem);
                }
            }

            UpdateTotals();
        }

        private string GenerateDocumentNumber()
        {
            using var context = new POSDbContext();
            var lastPurchase = context.DitariH.OrderByDescending(p => p.Id).FirstOrDefault();
            var nextNumber = (lastPurchase?.Id ?? 0) + 1;
            return $"BL-{DateTime.Now:yyyyMMdd}-{nextNumber:D4}";
        }
        
        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (ArticleComboBox.SelectedItem == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një artikull!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!decimal.TryParse(QuantityTextBox.Text, out var quantity) || quantity <= 0)
            {
                MessageBox.Show("Ju lutem shkruani një sasi të vlefshme!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!decimal.TryParse(PurchasePriceTextBox.Text, out var purchasePrice) || purchasePrice < 0)
            {
                MessageBox.Show("Ju lutem shkruani një çmim të vlefshëm!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Parse sales price (optional, defaults to article's current sales price)
            decimal salesPrice = 0;
            if (!decimal.TryParse(SalesPriceTextBox.Text, out salesPrice))
            {
                salesPrice = (ArticleComboBox.SelectedItem as Article)?.SalesPrice ?? 0;
            }
            
            // Parse rabat percentage (optional, defaults to 0)
            decimal rabatPercent = 0;
            decimal.TryParse(RabatTextBox.Text, out rabatPercent);
            
            var article = ArticleComboBox.SelectedItem as Article;
            if (article == null) return;
            
            // IMPORTANT: Check if item with SAME ArticleId AND SAME PurchasePrice exists
            // If different purchase price, add as NEW row (user requirement)
            var existingItem = _items.FirstOrDefault(i => 
                i.ArticleId == article.Id && 
                Math.Abs(i.PurchasePrice - purchasePrice) < 0.001m); // Same article AND same price
            
            if (existingItem != null)
            {
                // Same article with same purchase price - just add quantity
                existingItem.Quantity += quantity;
                existingItem.RecalculateTotal();
                ItemsDataGrid.Items.Refresh();
            }
            else
            {
                // Different purchase price OR new article - add as NEW ROW
                var rabatValue = purchasePrice * quantity * (rabatPercent / 100);
                var priceAfterRabat = purchasePrice * (1 - rabatPercent / 100);
                // Default VAT to 0 for small business (no mandatory tax) - user can change if needed
                var effectiveVATRate = 0m; // Set to 0 by default instead of article.VATRate
                var totalValue = quantity * priceAfterRabat * (1 + effectiveVATRate / 100);
                
                var newItem = new PurchaseItemDisplay
                {
                    ArticleId = article.Id,
                    ArticleName = article.Name,
                    Quantity = quantity,
                    PurchasePrice = purchasePrice,
                    SalesPrice = salesPrice,
                    RabatPercent = rabatPercent,
                    VATRate = 0, // Default to 0% VAT for small business (no mandatory tax)
                    TotalValue = totalValue
                };
                
                // Subscribe to property changes for real-time updates - only on non-TotalValue changes
                newItem.PropertyChanged += (s, args) => {
                    if (s is PurchaseItemDisplay item && 
                        args.PropertyName != nameof(PurchaseItemDisplay.TotalValue))
                    {
                        item.RecalculateTotal();
                        UpdateTotals();
                    }
                };
                
                _items.Add(newItem);
            }
            
            // Update article prices in database using SQL Server (Artikujt table)
            if (POSDbContext.UseSqlServer)
            {
                using var context = new POSDbContext();
                var dbArtikull = context.Artikujt.FirstOrDefault(a => a.Id == article.Id);
                if (dbArtikull != null)
                {
                    dbArtikull.CFurnizimit = (double)purchasePrice;
                    if (salesPrice > 0)
                    {
                        dbArtikull.CShitjes = (double)salesPrice;
                    }
                    context.SaveChanges();
                }
            }
            
            UpdateTotals();
            
            // Reset inputs
            ArticleComboBox.SelectedIndex = -1;
            QuantityTextBox.Text = "1";
            PurchasePriceTextBox.Text = "0.00";
            SalesPriceTextBox.Text = "0.00";
            RabatTextBox.Text = "0";
        }
        
        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.DataContext as PurchaseItemDisplay;
            if (item != null)
            {
                _items.Remove(item);
                UpdateTotals();
            }
        }
        
        private void UpdateTotals()
        {
            // Calculate totals including rabat
            var subtotal = _items.Sum(i => i.Quantity * i.PurchasePrice * (1 - i.RabatPercent / 100));
            var vat = _items.Sum(i => i.Quantity * i.PurchasePrice * (1 - i.RabatPercent / 100) * (i.VATRate / 100));
            var total = subtotal + vat;
            
            SubtotalText.Text = $"{subtotal:N2} €";
            VATText.Text = $"{vat:N2} €";
            TotalText.Text = $"{total:N2} €";
            
            // Refresh the grid to show updated values
            ItemsDataGrid.Items.Refresh();
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DocumentNumberTextBox.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
            DocumentNumberTextBox.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
            SupplierComboBox.ClearValue(System.Windows.Controls.ComboBox.BorderBrushProperty);
            SupplierComboBox.ClearValue(System.Windows.Controls.ComboBox.BorderThicknessProperty);

            if (string.IsNullOrWhiteSpace(DocumentNumberTextBox.Text))
            {
                DocumentNumberTextBox.BorderBrush = System.Windows.Media.Brushes.Red;
                DocumentNumberTextBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Numri i Dokumentit' është e detyrueshme.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DocumentNumberTextBox.Focus();
                return;
            }

            if (SupplierComboBox.SelectedValue == null)
            {
                SupplierComboBox.BorderBrush = System.Windows.Media.Brushes.Red;
                SupplierComboBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Furnizuesi' është e detyrueshme.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SupplierComboBox.Focus();
                return;
            }

            if (_items.Count == 0)
            {
                MessageBox.Show("Ju lutem shtoni të paktën një artikull!", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                using var context = new POSDbContext();

                var documentNumber = DocumentNumberTextBox.Text;
                var date = DatePicker.SelectedDate ?? DateTime.Now;
                var supplierId = (int)SupplierComboBox.SelectedValue;
                var purchaseType = PurchaseTypeComboBox.Text;
                var isPaid = IsPaidCheckBox.IsChecked ?? false;

                if (_purchase != null)
                {
                    var oldItems = context.DitariH.Where(d => d.NrFatures == _purchase.DocumentNumber).ToList();
                    context.DitariH.RemoveRange(oldItems);
                }

                var lastNumri = context.DitariH.Max(d => (long?)d.Numri) ?? 0;
                var newNumri = lastNumri + 1;

                foreach (var item in _items)
                {
                    var priceAfterRabat = item.PurchasePrice * (1 - item.RabatPercent / 100);
                    var vatValue = priceAfterRabat * item.Quantity * (item.VATRate / 100);
                    var totalWithVat = priceAfterRabat * item.Quantity + vatValue;

                    var artikull = context.Artikujt.FirstOrDefault(a => a.Id == item.ArticleId);
                    if (artikull != null)
                    {
                        artikull.Sasia = (artikull.Sasia ?? 0) + (double)item.Quantity;
                        artikull.CFurnizimit = (double)item.PurchasePrice;
                        if (item.SalesPrice > 0)
                        {
                            artikull.CShitjes = (double)item.SalesPrice;
                        }
                    }
                }

                context.SaveChanges();
                ArticleDataService.ClearStockCache();

                MessageBox.Show("Blerja u ruajt me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes: {ex.Message}\n\nDetaje: {ex.InnerException?.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        public class PurchaseItemDisplay : System.ComponentModel.INotifyPropertyChanged
        {
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            
            // Flag to prevent recursive property change notifications
            private bool _isRecalculating = false;
            
            private void OnPropertyChanged(string propertyName)
            {
                // Prevent recursive calls during recalculation
                if (!_isRecalculating)
                {
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
                }
            }
            
            public int ArticleId { get; set; }
            public string ArticleName { get; set; } = "";
            
            private decimal _quantity;
            public decimal Quantity 
            { 
                get => _quantity;
                set 
                { 
                    if (_quantity != value)
                    {
                        _quantity = value; 
                        OnPropertyChanged(nameof(Quantity)); 
                    }
                }
            }
            
            private decimal _purchasePrice;
            public decimal PurchasePrice 
            { 
                get => _purchasePrice;
                set 
                { 
                    if (_purchasePrice != value)
                    {
                        _purchasePrice = value; 
                        OnPropertyChanged(nameof(PurchasePrice)); 
                    }
                }
            }
            
            private decimal _salesPrice;
            public decimal SalesPrice 
            { 
                get => _salesPrice;
                set 
                { 
                    if (_salesPrice != value)
                    {
                        _salesPrice = value; 
                        OnPropertyChanged(nameof(SalesPrice)); 
                    }
                }
            }
            
            private decimal _rabatPercent;
            public decimal RabatPercent 
            { 
                get => _rabatPercent;
                set 
                { 
                    if (_rabatPercent != value)
                    {
                        _rabatPercent = value; 
                        OnPropertyChanged(nameof(RabatPercent)); 
                    }
                }
            }
            
            private decimal _vatRate;
            public decimal VATRate 
            { 
                get => _vatRate;
                set 
                { 
                    if (_vatRate != value)
                    {
                        _vatRate = value; 
                        OnPropertyChanged(nameof(VATRate)); 
                    }
                }
            }
            
            private decimal _totalValue;
            public decimal TotalValue 
            { 
                get => _totalValue;
                set 
                { 
                    if (_totalValue != value)
                    {
                        _totalValue = value; 
                        OnPropertyChanged(nameof(TotalValue)); 
                    }
                }
            }
            
            public void RecalculateTotal()
            {
                // Prevent recursive calls
                if (_isRecalculating) return;
                
                try
                {
                    _isRecalculating = true;
                    var priceAfterRabat = PurchasePrice * (1 - RabatPercent / 100);
                    var newTotal = Quantity * priceAfterRabat * (1 + VATRate / 100);
                    
                    // Only update if value actually changed (prevents unnecessary notifications)
                    if (Math.Abs(_totalValue - newTotal) > 0.001m)
                    {
                        _totalValue = newTotal;
                        // Directly invoke without triggering recursion
                        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TotalValue)));
                    }
                }
                finally
                {
                    _isRecalculating = false;
                }
            }
        }
    }
}