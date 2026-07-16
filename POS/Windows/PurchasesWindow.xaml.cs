using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Services;
using ClosedXML.Excel;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class PurchasesWindow : Window
    {
        private ObservableCollection<PurchaseDisplay> _purchases = new ObservableCollection<PurchaseDisplay>();
        private List<PurchaseDisplay> _allPurchases = new List<PurchaseDisplay>();
        
        // Simple display model for purchases
        public class PurchaseDisplay
        {
            public long Id { get; set; }
            public string DocumentNumber { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string Supplier { get; set; } = string.Empty;
            public string SupplierName { get; set; } = string.Empty;
            public string PurchaseType { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public decimal VATAmount { get; set; }
            public bool IsPaid { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }
        
        public PurchasesWindow()
        {
            InitializeComponent();
            
            // Check if user has permission to access purchases
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanManagePurchases && !userSession.IsAdmin)
            {
                MessageBox.Show("Ju nuk keni leje për të hyrë në menaxhimin e blerjeve.\nJu lutem kontaktoni administratorin.", 
                    "Qasje e refuzuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Loaded += (s, e) => this.Close();
                return;
            }
            
            InitializeDates();
            LoadPurchasesData();
        }
        
        private void InitializeDates()
        {
            // Set default date range to include previous year's data
            FromDatePicker.SelectedDate = new DateTime(DateTime.Now.Year - 1, 1, 1);
            ToDatePicker.SelectedDate = DateTime.Now;
        }
        
        private void LoadPurchasesData()
        {
            try
            {
                using var context = new POSDbContext();
                
                var fromDate = FromDatePicker.SelectedDate ?? DateTime.Now.AddMonths(-1);
                var toDate = ToDatePicker.SelectedDate ?? DateTime.Now;
                
                // Load from local Purchase table (SQLite) - purchases are stored locally, not in BMD
                var purchases = context.Purchases
                    .Include(p => p.Items)
                    .Include(p => p.Supplier)
                    .Where(p => p.Date >= fromDate && p.Date <= toDate.AddDays(1))
                    .OrderByDescending(p => p.Date)
                    .Take(2000)
                    .Select(p => new PurchaseDisplay
                    {
                        Id = p.Id,
                        Date = p.Date.Date,
                        DocumentNumber = p.DocumentNumber,
                        SupplierName = p.Supplier != null ? p.Supplier.Name : "Unknown",
                        TotalAmount = p.TotalAmount,
                        Status = p.Status
                    })
                    .ToList();
                
                _allPurchases = purchases.Cast<PurchaseDisplay>().ToList();
                _purchases = new ObservableCollection<PurchaseDisplay>(_allPurchases);
                PurchasesDataGrid.ItemsSource = _purchases;
                
                UpdateSummary();
                UpdateResultCount(_allPurchases.Count, _allPurchases.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _allPurchases = new List<PurchaseDisplay>();
                _purchases = new ObservableCollection<PurchaseDisplay>();
                PurchasesDataGrid.ItemsSource = _purchases;
            }
        }
        
        private void UpdateSummary()
        {
            var total = _purchases.Sum(p => p.TotalAmount);
            var paid = _purchases.Where(p => p.IsPaid).Sum(p => p.TotalAmount);
            var unpaid = total - paid;
            
            TotalPurchasesText.Text = $"{total:N2} €";
            PaidAmountText.Text = $"{paid:N2} €";
            UnpaidAmountText.Text = $"{unpaid:N2} €";
            PurchaseCountText.Text = _purchases.Count.ToString();
        }
        
        private void UpdateResultCount(int shown, int total)
        {
            if (ResultCountText != null)
            {
                ResultCountText.Text = shown == total 
                    ? $"Duke shfaqur {total} blerje" 
                    : $"Duke shfaqur {shown} nga {total} blerje";
            }
        }
        
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPurchasesData();
        }
        
        private void NewPurchase_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new PurchaseEditWindow(null);
            if (editWindow.ShowDialog() == true)
            {
                LoadPurchasesData();
            }
        }
        
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? string.Empty;
            
            if (string.IsNullOrEmpty(searchText))
            {
                _purchases = new ObservableCollection<PurchaseDisplay>(_allPurchases);
            }
            else
            {
                var filtered = _allPurchases.Where(p =>
                    (p.DocumentNumber?.ToLower().Contains(searchText) ?? false) ||
                    (p.Supplier?.ToLower().Contains(searchText) ?? false) ||
                    (p.Notes?.ToLower().Contains(searchText) ?? false)
                ).ToList();
                _purchases = new ObservableCollection<PurchaseDisplay>(filtered);
            }
            
            PurchasesDataGrid.ItemsSource = _purchases;
            UpdateSummary();
            UpdateResultCount(_purchases.Count, _allPurchases.Count);
        }
        
        private void PurchasesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PurchasesDataGrid.SelectedItem is PurchaseDisplay selectedPurchase)
            {
                LoadPurchaseItems(selectedPurchase.DocumentNumber);
            }
        }
        
        private void LoadPurchaseItems(string documentNumber)
        {
            try
            {
                using var context = new POSDbContext();
                
                if (POSDbContext.UseSqlServer)
                {
                    // Load items from DitariH for SQL Server
                    // DocumentNumber is in format "B-{id}" or just a number
                    long headerId = 0;
                    if (documentNumber.StartsWith("B-"))
                    {
                        long.TryParse(documentNumber.Replace("B-", ""), out headerId);
                    }
                    else
                    {
                        long.TryParse(documentNumber, out headerId);
                    }
                    
                    if (headerId == 0) return;
                    
                    var items = context.DitariH
                        .Where(d => d.Id == headerId)
                        .ToList();
                    
                    PurchaseItemsDataGrid.ItemsSource = items.Select(i => new
                    {
                        ArticleName = i.Artikulli ?? string.Empty,
                        Barcode = i.Barkodi ?? string.Empty,
                        Quantity = i.Sasia ?? 0,
                        PurchasePrice = i.CmimiFurn ?? 0,
                        VATRate = i.TvshPer ?? 18,
                        TotalValue = (i.Sasia ?? 0) * (i.CmimiFurn ?? 0)
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të artikujve: {ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void PurchasesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditPurchase_Click(sender, new RoutedEventArgs());
        }
        
        private void ViewPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (PurchasesDataGrid.SelectedItem is PurchaseDisplay selectedPurchase)
            {
                // Show purchase details in a message box for now
                MessageBox.Show(
                    $"Dokumenti: {selectedPurchase.DocumentNumber}\n" +
                    $"Data: {selectedPurchase.Date:dd/MM/yyyy}\n" +
                    $"Lloji: {selectedPurchase.PurchaseType}\n" +
                    $"Totali: {selectedPurchase.TotalAmount:N2} €\n" +
                    $"TVSH: {selectedPurchase.VATAmount:N2} €\n" +
                    $"Paguar: {(selectedPurchase.IsPaid ? "Po" : "Jo")}",
                    "Detajet e blerjes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Zgjidhni një blerje për ta parë!", "Paralajmërim", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void EditPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (PurchasesDataGrid.SelectedItem is PurchaseDisplay selectedPurchase)
            {
                try
                {
                    // Create a Purchase object from the display model to pass to edit window
                    var purchase = new Purchase
                    {
                        Id = (int)selectedPurchase.Id,
                        DocumentNumber = selectedPurchase.DocumentNumber,
                        Date = selectedPurchase.Date,
                        PurchaseType = selectedPurchase.PurchaseType,
                        TotalAmount = selectedPurchase.TotalAmount,
                        VATAmount = selectedPurchase.VATAmount,
                        IsPaid = selectedPurchase.IsPaid
                    };
                    
                    var editWindow = new PurchaseEditWindow(purchase);
                    if (editWindow.ShowDialog() == true)
                    {
                        LoadPurchasesData();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë hapjes së blerjes për ndryshim: {ex.Message}", 
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një blerje për ta ndryshuar!", "Paralajmërim", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void MarkAsPaid_Click(object sender, RoutedEventArgs e)
        {
            if (PurchasesDataGrid.SelectedItem is PurchaseDisplay selectedPurchase)
            {
                if (selectedPurchase.IsPaid)
                {
                    MessageBox.Show("Kjo blerje është shënuar tashmë si e paguar!", "Info", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                try
                {
                    using var context = new POSDbContext();
                    
                    // Parse document number to get ID
                    long headerId = 0;
                    if (selectedPurchase.DocumentNumber.StartsWith("B-"))
                    {
                        long.TryParse(selectedPurchase.DocumentNumber.Replace("B-", ""), out headerId);
                    }
                    else
                    {
                        long.TryParse(selectedPurchase.DocumentNumber, out headerId);
                    }
                    
                    if (headerId == 0)
                    {
                        MessageBox.Show("Numri i dokumentit nuk është valid!", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    // First, calculate the total remaining amount
                    var items = context.DitariH
                        .Where(d => d.Id == headerId)
                        .ToList();
                    
                    if (!items.Any())
                    {
                        MessageBox.Show("Nuk u gjetën artikujt për këtë blerje!", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    var totalRemaining = (decimal)items.Sum(i => i.Mbetur ?? 0);
                    
                    // Show payment dialog
                    var inputWindow = new Window
                    {
                        Title = "Regjistro Pagesë për Blerje",
                        Width = 420,
                        Height = 280,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        ResizeMode = ResizeMode.NoResize
                    };
                    
                    var stack = new StackPanel { Margin = new Thickness(20) };
                    stack.Children.Add(new TextBlock 
                    { 
                        Text = $"Fatura: {selectedPurchase.DocumentNumber}", 
                        FontWeight = FontWeights.Bold,
                            FontSize = 14,
                            Margin = new Thickness(0, 0, 0, 5) 
                        });
                        stack.Children.Add(new TextBlock 
                        { 
                            Text = $"Furnitori: {selectedPurchase.Supplier}", 
                            Margin = new Thickness(0, 0, 0, 5) 
                        });
                        stack.Children.Add(new TextBlock 
                        { 
                            Text = $"Shuma totale: €{selectedPurchase.TotalAmount:N2}", 
                            Margin = new Thickness(0, 0, 0, 5) 
                        });
                        stack.Children.Add(new TextBlock 
                        { 
                            Text = $"Shuma e mbetur: €{totalRemaining:N2}", 
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.DarkRed,
                            FontSize = 16,
                            Margin = new Thickness(0, 0, 0, 15) 
                        });
                        stack.Children.Add(new TextBlock { Text = "Shkruani shumën e paguar:" });
                        var amountBox = new TextBox 
                        { 
                            Text = totalRemaining.ToString("N2"), 
                            Margin = new Thickness(0, 5, 0, 15),
                            FontSize = 14,
                            Height = 30
                        };
                        stack.Children.Add(amountBox);
                        
                        var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                        var okButton = new Button 
                        { 
                            Content = "💾 Konfirmo", 
                            Width = 100, 
                            Height = 35,
                            IsDefault = true, 
                            Margin = new Thickness(0, 0, 10, 0) 
                        };
                        var cancelButton = new Button 
                        { 
                            Content = "❌ Anulo", 
                            Width = 100, 
                            Height = 35,
                            IsCancel = true 
                        };
                        buttonStack.Children.Add(okButton);
                        buttonStack.Children.Add(cancelButton);
                        stack.Children.Add(buttonStack);
                        
                        inputWindow.Content = stack;
                        
                        decimal payment = 0;
                        bool confirmed = false;
                        
                        okButton.Click += (s, args) =>
                        {
                            // Validate payment: must be positive and not exceed remaining amount
                            if (!decimal.TryParse(amountBox.Text, out payment))
                            {
                                MessageBox.Show("Shuma e pavlefshme. Ju lutem shkruani një numër valid.", "Gabim", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            
                            if (payment <= 0)
                            {
                                MessageBox.Show("Shuma duhet të jetë më e madhe se 0.\n\nNëse dëshironi të shënoni si të paguar plotësisht, shkruani shumën e mbetur.", "Gabim", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            
                            if (payment > totalRemaining)
                            {
                                MessageBox.Show($"Shuma nuk mund të jetë më e madhe se shuma e mbetur (€{totalRemaining:N2}).", "Gabim", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            
                            confirmed = true;
                            inputWindow.Close();
                        };
                        
                        inputWindow.ShowDialog();
                        
                        if (!confirmed) return;
                        
                        // Apply payment proportionally to all items
                        decimal remainingPayment = payment;
                        foreach (var item in items)
                        {
                            if (remainingPayment <= 0) break;
                            
                            var itemRemaining = (decimal)(item.Mbeti ?? 0);
                            if (itemRemaining <= 0) continue;
                            
                            var payForItem = Math.Min(remainingPayment, itemRemaining);
                            
                            item.Paguar = (item.Paguar ?? 0) + (double)payForItem;
                            var newRemaining = itemRemaining - payForItem;
                            // Round to 0 if remaining is very small (less than 0.01)
                            item.Mbetur = newRemaining < 0.01m ? 0 : (double)newRemaining;
                            
                            remainingPayment -= payForItem;
                        }
                        
                        context.SaveChanges();
                        
                        LoadPurchasesData();
                        MessageBox.Show($"Pagesa prej €{payment:N2} u regjistrua me sukses!", "Sukses", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim: {ex.Message}", "Gabim", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një blerje!", "Paralajmërim", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void ClearAllDebts_Click(object sender, RoutedEventArgs e)
        {
            var unpaidAmount = _purchases.Where(p => !p.IsPaid).Sum(p => p.TotalAmount);
            var unpaidCount = _purchases.Count(p => !p.IsPaid);
            
            if (unpaidCount == 0)
            {
                MessageBox.Show("Nuk ka borxhe për të pastruar!", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var result = MessageBox.Show(
                $"A jeni i sigurt që doni t'i pastroni të gjitha borxhet?\n\n" +
                $"Blerje të papaguara: {unpaidCount}\n" +
                $"Shuma totale: €{unpaidAmount:N2}\n\n" +
                "Kjo do të shënojë të gjitha blerjet si të paguara.",
                "Konfirmimi i Pastrimit të Borxheve",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
                return;
            
            try
            {
                using var context = new POSDbContext();
                
                // Get all DitariH items where Mbeti > 0
                var unpaidItems = context.DitariH
                    .Where(d => d.Mbeti > 0.01)
                    .ToList();
                
                foreach (var item in unpaidItems)
                {
                    // Mark as fully paid
                    item.Paguar = (item.Paguar ?? 0) + (item.Mbetur ?? 0);
                    item.Mbetur = 0;
                }
                
                context.SaveChanges();
                
                LoadPurchasesData();
                MessageBox.Show(
                    $"Të gjitha borxhet u pastruan!\n\n" +
                    $"Blerje të përditësuara: {unpaidItems.Count}",
                    "Sukses",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë pastrimit të borxheve: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Eksporto blerjet si Excel",
                    FileName = $"Blerjet_{DateTime.Now:yyyyMMdd}.xlsx"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Blerjet");
                    
                    // Headers
                    worksheet.Cell(1, 1).Value = "ID";
                    worksheet.Cell(1, 2).Value = "Nr. Dokumentit";
                    worksheet.Cell(1, 3).Value = "Data";
                    worksheet.Cell(1, 4).Value = "Lloji";
                    worksheet.Cell(1, 5).Value = "Totali (€)";
                    worksheet.Cell(1, 6).Value = "TVSH (€)";
                    worksheet.Cell(1, 7).Value = "Paguar";
                    worksheet.Cell(1, 8).Value = "Shënime";
                    
                    var headerRange = worksheet.Range(1, 1, 1, 8);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    
                    int row = 2;
                    foreach (var purchase in _purchases)
                    {
                        worksheet.Cell(row, 1).Value = purchase.Id;
                        worksheet.Cell(row, 2).Value = purchase.DocumentNumber;
                        worksheet.Cell(row, 3).Value = purchase.Date.ToString("dd/MM/yyyy");
                        worksheet.Cell(row, 4).Value = purchase.PurchaseType;
                        worksheet.Cell(row, 5).Value = purchase.TotalAmount;
                        worksheet.Cell(row, 6).Value = purchase.VATAmount;
                        worksheet.Cell(row, 7).Value = purchase.IsPaid ? "Po" : "Jo";
                        worksheet.Cell(row, 8).Value = purchase.Notes;
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
}
