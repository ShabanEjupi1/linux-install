using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KosovaPOS.Database;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Windows
{
    public partial class FinanceWindow : Window
    {
        private List<ArkaHyrjeDalje> _allIncomes = new List<ArkaHyrjeDalje>();
        private List<ArkaHyrjeDalje> _allExpenses = new List<ArkaHyrjeDalje>();
        private ObservableCollection<Borxhi> _debts = new ObservableCollection<Borxhi>();
        private List<SupplierDebtDisplay> _supplierDebts = new List<SupplierDebtDisplay>();
        
        // Display class for supplier debts
        public class SupplierDebtDisplay
        {
            public string InvoiceNumber { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string SupplierName { get; set; } = string.Empty;
            public string? SupplierId { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal RemainingAmount { get; set; }
        }
        
        public FinanceWindow()
        {
            InitializeComponent();
            InitializeDates();
            LoadData();
        }
        
        private void InitializeDates()
        {
            // Default to current year
            var startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
            IncomeFromDate.SelectedDate = startOfYear;
            IncomeToDate.SelectedDate = DateTime.Now;
            ExpenseFromDate.SelectedDate = startOfYear;
            ExpenseToDate.SelectedDate = DateTime.Now;
        }
        
        private void LoadData()
        {
            try
            {
                // Always use SQL Server - no SQLite support
                LoadIncomes();
                LoadExpenses();
                LoadDebts();
                LoadSupplierDebts();
                UpdateSummary();
                
                LastUpdateText.Text = $"Përditësuar: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LoadIncomes()
        {
            try
            {
                using var context = new POSDbContext();

                var fromDate = IncomeFromDate.SelectedDate ?? new DateTime(DateTime.Now.Year, 1, 1);
                var toDate = (IncomeToDate.SelectedDate ?? DateTime.Now).AddDays(1);

                // Load sales (income) from DitariH - header records
                var salesHeaders = context.DitariH
                    .Where(h => h.Data.HasValue && h.Data >= fromDate && h.Data < toDate)
                    .OrderByDescending(h => h.Data)
                    .Take(5000)
                    .ToList();

                var salesData = salesHeaders.Select(header => new ArkaHyrjeDalje
                    {
                        Id = (int)(header.Id % int.MaxValue),
                        Data = header.Data,
                        Lloji = "Hyrje", // Type: Income
                        Pershkrimi = $"Shitje - FATURA-{header.Id}",
                        Shuma = header.Totali ?? 0
                    }).ToList();

                // Also include ArkaHyrjeDalje entries if any
                var arkaIncomes = context.ArkaHyrjeDalje
                    .Where(a => a.Lloji == "Hyrje" && a.Data >= fromDate && a.Data < toDate)
                    .OrderByDescending(a => a.Data)
                    .ToList();

                // Combine and sort
                _allIncomes = salesData.Concat(arkaIncomes)
                    .OrderByDescending(a => a.Data)
                    .ThenByDescending(a => a.Id)
                    .ToList();

                ApplyIncomeFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të shitjeve:\n\n{ex.Message}\n\n{ex.InnerException?.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ApplyIncomeFilter()
        {
            var searchText = IncomeSearchBox?.Text?.Trim().ToLower() ?? "";
            
            var filtered = string.IsNullOrEmpty(searchText) 
                ? _allIncomes 
                : _allIncomes.Where(i => 
                    (i.DOK?.ToLower().Contains(searchText) ?? false) ||
                    (i.Pershkrimi?.ToLower().Contains(searchText) ?? false)).ToList();
            
            IncomeDataGrid.ItemsSource = filtered;
            IncomeResultText.Text = $"Duke shfaqur {filtered.Count} nga {_allIncomes.Count} shitje";
        }
        
        private void LoadExpenses()
        {
            try
            {
                using var context = new POSDbContext();
                
                var fromDate = ExpenseFromDate.SelectedDate ?? new DateTime(DateTime.Now.Year, 1, 1);
                var toDate = (ExpenseToDate.SelectedDate ?? DateTime.Now).AddDays(1);
                
                // Load purchases (expenses) from DitariH - actual purchase records
                // Note: DitariH doesn't have NrFatures property, using Id instead
                var purchaseHeaders = context.DitariH
                    .Where(h => h.Data.HasValue && h.Data >= fromDate && h.Data < toDate)
                    .OrderByDescending(h => h.Data)
                    .Take(5000)
                    .ToList();
                
                var purchaseData = purchaseHeaders.Select(header => new ArkaHyrjeDalje
                    {
                        Id = (int)(header.Id % int.MaxValue),
                        Data = header.Data,
                        Lloji = "Dalje", // Type: Expense
                        Pershkrimi = $"Blerje - B-{header.Id}",
                        Shuma = header.Totali ?? 0
                    }).ToList();
                
                // Also include ArkaHyrjeDalje expense entries
                var arkaExpenses = context.ArkaHyrjeDalje
                    .Where(a => a.Lloji == "Dalje" && a.Data >= fromDate && a.Data < toDate)
                    .OrderByDescending(a => a.Data)
                    .ToList();
                
                // Combine and sort
                _allExpenses = purchaseData.Concat(arkaExpenses)
                    .OrderByDescending(a => a.Data)
                    .ThenByDescending(a => a.Id)
                    .ToList();
                
                ApplyExpenseFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të blerjeve:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ApplyExpenseFilter()
        {
            var searchText = ExpenseSearchBox?.Text?.Trim().ToLower() ?? "";
            
            var filtered = string.IsNullOrEmpty(searchText) 
                ? _allExpenses 
                : _allExpenses.Where(e => 
                    (e.DOK?.ToLower().Contains(searchText) ?? false) ||
                    (e.Pershkrimi?.ToLower().Contains(searchText) ?? false)).ToList();
            
            ExpenseDataGrid.ItemsSource = filtered;
            ExpenseResultText.Text = $"Duke shfaqur {filtered.Count} nga {_allExpenses.Count} blerje";
        }
        
        private void LoadDebts()
        {
            try
            {
                using var context = new POSDbContext();
                
                // Load debts from Borxhi
                var debts = context.Borxhi.OrderByDescending(b => b.Data).ToList();
                _debts = new ObservableCollection<Borxhi>(debts);
                DebtsDataGrid.ItemsSource = _debts;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të borxheve:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LoadSupplierDebts()
        {
            try
            {
                using var context = new POSDbContext();
                
                // Load unpaid purchases from DitariH - use Mbetur (mapped) not Mbeti (NotMapped)
                var unpaidPurchases = context.DitariH
                    .Where(d => d.Mbetur > 0)
                    .ToList();
                
                
                // Load suppliers for names
                var suppliers = context.FurnitoriNew.ToList();
                
                _supplierDebts = unpaidPurchases
                    .GroupBy(d => new { ReceiptId = d.Id, d.Data?.Date, d.Klienti })
                    .Select(g => new SupplierDebtDisplay
                    {
                        InvoiceNumber = $"B-{g.Key.ReceiptId}",
                        Date = g.Key.Date ?? DateTime.Now,
                        SupplierId = g.Key.Klienti,
                        SupplierName = g.Key.Klienti ?? "E panjohur",
                        TotalAmount = (decimal)g.Sum(x => x.Totali ?? 0),
                        PaidAmount = (decimal)g.Sum(x => x.Paguar ?? 0),
                        RemainingAmount = (decimal)g.Sum(x => x.Mbetur ?? 0)
                    })
                    .Where(d => d.RemainingAmount > 0)
                    .OrderByDescending(d => d.RemainingAmount)
                    .ToList();
                
                SupplierDebtsDataGrid.ItemsSource = _supplierDebts;
                
                var totalSupplierDebt = _supplierDebts.Sum(d => d.RemainingAmount);
                SupplierDebtsTotalText.Text = $" - Total: €{totalSupplierDebt:N2}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të borxheve të furnitorëve:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RefreshSupplierDebts_Click(object sender, RoutedEventArgs e)
        {
            LoadSupplierDebts();
            UpdateSummary();
        }
        
        private void PaySupplierDebt_Click(object sender, RoutedEventArgs e)
        {
            if (SupplierDebtsDataGrid.SelectedItem is not SupplierDebtDisplay selectedDebt)
            {
                MessageBox.Show("Ju lutem zgjidhni një borxh furnitori.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (selectedDebt.RemainingAmount <= 0)
            {
                MessageBox.Show("Ky borxh është paguar plotësisht.", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // Simple payment input dialog
            var inputWindow = new Window
            {
                Title = "Regjistro Pagesë për Furnitorin",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
            
            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock 
            { 
                Text = $"Fatura: {selectedDebt.InvoiceNumber}", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            stack.Children.Add(new TextBlock 
            { 
                Text = $"Furnitori: {selectedDebt.SupplierName}", 
                Margin = new Thickness(0, 0, 0, 5) 
            });
            stack.Children.Add(new TextBlock 
            { 
                Text = $"Shuma e mbetur: €{selectedDebt.RemainingAmount:N2}", 
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.DarkRed,
                Margin = new Thickness(0, 0, 0, 15) 
            });
            stack.Children.Add(new TextBlock { Text = "Shkruani shumën e paguar:" });
            var amountBox = new TextBox { Text = selectedDebt.RemainingAmount.ToString("N2"), Margin = new Thickness(0, 5, 0, 15) };
            stack.Children.Add(amountBox);
            
            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "Konfirmo", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Anulo", Width = 80, IsCancel = true };
            buttonStack.Children.Add(okButton);
            buttonStack.Children.Add(cancelButton);
            stack.Children.Add(buttonStack);
            
            inputWindow.Content = stack;
            
            decimal payment = 0;
            bool confirmed = false;
            
            okButton.Click += (s, args) =>
            {
                if (!decimal.TryParse(amountBox.Text, out payment))
                {
                    MessageBox.Show("Shuma e pavlefshme. Ju lutem shkruani një numër valid.", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (payment <= 0)
                {
                    MessageBox.Show("Shuma duhet të jetë më e madhe se 0.\n\nNëse dëshironi të shënoni si të paguar plotësisht, shkruani shumën e mbetur.", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (payment > selectedDebt.RemainingAmount)
                {
                    MessageBox.Show($"Shuma nuk mund të jetë më e madhe se shuma e mbetur (€{selectedDebt.RemainingAmount:N2}).", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                confirmed = true;
                inputWindow.Close();
            };
            
            inputWindow.ShowDialog();
            
            if (!confirmed) return;
            
            try
            {
                using var context = new POSDbContext();
                
                // Update DitariH items with this invoice number (extract ID from "B-{id}" format)
                var invoiceIdStr = selectedDebt.InvoiceNumber.Replace("B-", "");
                if (!long.TryParse(invoiceIdStr, out long invoiceId))
                {
                    MessageBox.Show("Numri i faturës nuk është valid.", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var items = context.DitariH
                    .Where(d => d.Id == invoiceId && d.Mbetur > 0)
                    .ToList();
                
                decimal remainingPayment = payment;
                
                foreach (var item in items)
                {
                    if (remainingPayment <= 0) break;
                    
                    var itemRemaining = (decimal)(item.Mbeti ?? 0);
                    var payForItem = Math.Min(remainingPayment, itemRemaining);
                    
                    item.Paguar = (item.Paguar ?? 0) + (double)payForItem;
                    item.Mbetur = (item.Mbetur ?? 0) - (double)payForItem;
                    
                    remainingPayment -= payForItem;
                }
                
                context.SaveChanges();
                
                LoadSupplierDebts();
                UpdateSummary();
                
                MessageBox.Show("Pagesa u regjistrua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateSummary()
        {
            // Calculate totals from DitariD (sales) and DitariH (purchases) for accurate financials
            using var context = new POSDbContext();
            
            // Get selected date range for filtering
            var incomeFromDate = IncomeFromDate.SelectedDate ?? new DateTime(DateTime.Now.Year, 1, 1);
            var incomeToDate = (IncomeToDate.SelectedDate ?? DateTime.Now).AddDays(1);
            var expenseFromDate = ExpenseFromDate.SelectedDate ?? new DateTime(DateTime.Now.Year, 1, 1);
            var expenseToDate = (ExpenseToDate.SelectedDate ?? DateTime.Now).AddDays(1);
            
            // Income from DitariH (sales) - filtered by date range
            var totalSalesIncome = context.DitariH
                .Where(h => h.Data.HasValue && h.Data >= incomeFromDate && h.Data < incomeToDate)
                .Sum(h => h.Totali ?? 0);
            
            
            // Expenses from DitariH (purchases) - filtered by date range
            var totalPurchaseExpense = context.DitariH
                .Where(h => h.Data.HasValue && h.Data >= expenseFromDate && h.Data < expenseToDate)
                .Sum(h => h.Totali ?? 0);
            
            // Debts total (all time)
            var totalDebts = _debts.Sum(d => d.Mbeti ?? 0);
            
            // Calculate balance
            var balance = totalSalesIncome - totalPurchaseExpense;
            
            // Count transactions - use DitariH.Id (mapped) instead of NotMapped properties
            var incomeCount = context.DitariH
                .Where(h => h.Data.HasValue && h.Data >= incomeFromDate && h.Data < incomeToDate)
                .Count();
            var expenseCount = context.DitariH
                .Where(h => h.Data.HasValue && h.Data >= expenseFromDate && h.Data < expenseToDate)
                .Select(h => h.Id)
                .Distinct()
                .Count();
            
            // Update UI
            TotalIncomeText.Text = $"€{totalSalesIncome:N2}";
            TotalExpenseText.Text = $"€{totalPurchaseExpense:N2}";
            BalanceText.Text = $"€{balance:N2}";
            TotalDebtsText.Text = $"€{totalDebts:N2}";
            
            IncomeCountText.Text = $"{incomeCount:N0} fatura";
            ExpenseCountText.Text = $"{expenseCount:N0} blerje";
            DebtsCountText.Text = $"{_debts.Count:N0} klientë";
        }
        
        private void IncomeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                LoadIncomes();
                UpdateSummary();
            }
        }
        
        private void IncomeSearch_Changed(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ApplyIncomeFilter();
            }
        }
        
        private void RefreshIncome_Click(object sender, RoutedEventArgs e)
        {
            LoadIncomes();
            UpdateSummary();
        }
        
        private void ExpenseFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                LoadExpenses();
                UpdateSummary();
            }
        }
        
        private void ExpenseSearch_Changed(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ApplyExpenseFilter();
            }
        }
        
        private void RefreshExpense_Click(object sender, RoutedEventArgs e)
        {
            LoadExpenses();
            UpdateSummary();
        }
        
        private void AddDebt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(DebtCustomerBox.Text))
                {
                    MessageBox.Show("Ju lutem shkruani emrin e klientit.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!decimal.TryParse(DebtAmountBox.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("Ju lutem shkruani vlerën e saktë.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                decimal.TryParse(DebtPaidBox.Text, out decimal paid);
                
                using var context = new POSDbContext();
                
                var debt = new Borxhi
                {
                    Klienti = DebtCustomerBox.Text.Trim(),
                    Shuma = (double)amount,
                    Paguar = (double)paid,
                    Mbetur = (double)(amount - paid),
                    Data = DateTime.Today,
                    Pershkrimi = DebtDescriptionBox.Text?.Trim()
                };
                
                context.Borxhi.Add(debt);
                context.SaveChanges();
                
                _debts.Insert(0, debt);
                UpdateSummary();
                
                // Clear inputs
                DebtCustomerBox.Clear();
                DebtAmountBox.Clear();
                DebtPaidBox.Text = "0";
                DebtDescriptionBox.Clear();
                
                MessageBox.Show("Borxhi u shtua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RecordPayment_Click(object sender, RoutedEventArgs e)
        {
            if (DebtsDataGrid.SelectedItem is not Borxhi selectedDebt)
            {
                MessageBox.Show("Ju lutem zgjidhni një borxh.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var remaining = selectedDebt.Mbeti ?? 0;
            if (remaining <= 0)
            {
                MessageBox.Show("Ky borxh është paguar plotësisht.", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // Simple payment input dialog
            var inputWindow = new Window
            {
                Title = "Regjistro Pagesë",
                Width = 350,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
            
            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock 
            { 
                Text = $"Shuma e mbetur: €{remaining:N2}", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10) 
            });
            stack.Children.Add(new TextBlock { Text = "Shkruani shumën e paguar:" });
            var amountBox = new TextBox { Text = remaining.ToString("N2"), Margin = new Thickness(0, 5, 0, 15) };
            stack.Children.Add(amountBox);
            
            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "Konfirmo", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Anulo", Width = 80, IsCancel = true };
            buttonStack.Children.Add(okButton);
            buttonStack.Children.Add(cancelButton);
            stack.Children.Add(buttonStack);
            
            inputWindow.Content = stack;
            
            decimal payment = 0;
            bool confirmed = false;
            
            okButton.Click += (s, args) =>
            {
                if (decimal.TryParse(amountBox.Text, out payment) && payment > 0)
                {
                    confirmed = true;
                    inputWindow.Close();
                }
                else
                {
                    MessageBox.Show("Shuma e pavlefshme.", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            inputWindow.ShowDialog();
            
            if (!confirmed) return;
            
            try
            {
                using var context = new POSDbContext();
                var debt = context.Borxhi.Find(selectedDebt.Id);
                if (debt != null)
                {
                    debt.Paguar = (debt.Paguar ?? 0) + (double)payment;
                    debt.Mbetur = (debt.Shuma ?? 0) - (debt.Paguar ?? 0);
                    context.SaveChanges();
                    
                    // Update local collection
                    selectedDebt.Paguar = debt.Paguar;
                    selectedDebt.Mbetur = debt.Mbetur;
                    DebtsDataGrid.Items.Refresh();
                    UpdateSummary();
                    
                    MessageBox.Show("Pagesa u regjistrua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void DeleteDebt_Click(object sender, RoutedEventArgs e)
        {
            if (DebtsDataGrid.SelectedItem is not Borxhi selectedDebt)
            {
                MessageBox.Show("Ju lutem zgjidhni një borxh.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show($"A jeni i sigurt që doni të fshini borxhin e {selectedDebt.Klienti}?",
                "Konfirmo Fshirjen", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;
            
            try
            {
                using var context = new POSDbContext();
                var debt = context.Borxhi.Find(selectedDebt.Id);
                if (debt != null)
                {
                    context.Borxhi.Remove(debt);
                    context.SaveChanges();
                    
                    _debts.Remove(selectedDebt);
                    UpdateSummary();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void DebtsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/disable context menu items based on selection
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
}
