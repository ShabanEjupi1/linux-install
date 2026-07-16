using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.IO;

namespace KosovaPOS.Windows
{
    /// <summary>
    /// Display model for sales data - works with both SQLite and SQL Server
    /// </summary>
    public class SaleDisplay
    {
        public long Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal VATAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Cashier { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal LeftAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsFiscal { get; set; } = true;
        public string FiscalStatus => IsFiscal ? "🧾 Fiskale" : "📄 Jo-Fiskale";
        
        // For SQL Server, store the NUMRI to get items later
        public long? SqlServerNumri { get; set; }
    }
    
    /// <summary>
    /// Display model for sale items
    /// </summary>
    public class SaleItemDisplay
    {
        public long Id { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string ArticleName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATValue { get; set; }
        public decimal TotalValue { get; set; }
    }
    
    public partial class SalesWindow : Window
    {
        private SaleDisplay? _selectedSale;
        private List<SaleDisplay> _allSales = new List<SaleDisplay>();
        
        public SalesWindow()
        {
            InitializeComponent();
            
            // Check if user has permission to access sales reports
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanViewReports && !userSession.IsAdmin)
            {
                MessageBox.Show("Ju nuk keni leje për të hyrë në raportet e shitjeve.\nJu lutem kontaktoni administratorin.", 
                    "Qasje e refuzuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Loaded += (s, e) => this.Close();
                return;
            }
            
            // Set default date range (from start of previous year to now)
            ToDatePicker.SelectedDate = DateTime.Now;
            FromDatePicker.SelectedDate = new DateTime(DateTime.Now.Year - 1, 1, 1);
            
            LoadSalesData();
        }
        
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }
        
        private void SaleTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ApplyFilters();
            }
        }
        
        private void ApplyFilters()
        {
            var filtered = _allSales.AsEnumerable();
            
            // Filter by fiscal/non-fiscal type
            var saleTypeIndex = SaleTypeFilter?.SelectedIndex ?? 0;
            if (saleTypeIndex == 1) // Fiscal only
            {
                filtered = filtered.Where(s => s.IsFiscal);
            }
            else if (saleTypeIndex == 2) // Non-fiscal only
            {
                filtered = filtered.Where(s => !s.IsFiscal);
            }
            
            // Filter by search text
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? string.Empty;
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(s =>
                    (s.ReceiptNumber?.ToLower().Contains(searchText) ?? false) ||
                    (s.Cashier?.ToLower().Contains(searchText) ?? false) ||
                    (s.CustomerName?.ToLower().Contains(searchText) ?? false) ||
                    s.TotalAmount.ToString("N2").Contains(searchText)
                );
            }
            
            var filteredList = filtered.ToList();
            SalesDataGrid.ItemsSource = filteredList;
            UpdateResultCount(filteredList.Count, _allSales.Count);
            
            // Update summary cards based on filtered data
            UpdateSummaryCards(filteredList);
        }
        
        private void UpdateSummaryCards(List<SaleDisplay> sales)
        {
            var totalSales = sales.Sum(s => s.TotalAmount);
            var totalVAT = sales.Sum(s => s.VATAmount);
            var receiptCount = sales.Count;
            var averageSale = receiptCount > 0 ? totalSales / receiptCount : 0;
            
            TotalSalesText.Text = $"{totalSales:N2} €";
            TotalVATText.Text = $"{totalVAT:N2} €";
            TotalReceiptsText.Text = receiptCount.ToString();
            AverageSaleText.Text = $"{averageSale:N2} €";
        }
        
        private void FilterSalesBySearch()
        {
            ApplyFilters();
        }
        
        private void UpdateResultCount(int shown, int total)
        {
            if (ResultCountText != null)
            {
                ResultCountText.Text = shown == total 
                    ? $"Duke shfaqur {total} fatura" 
                    : $"Duke shfaqur {shown} nga {total} fatura";
            }
        }
        
        private void LoadSalesData()
        {
            try
            {
                using var context = new POSDbContext();

                var fromDate = FromDatePicker.SelectedDate ?? DateTime.Now.AddDays(-30);
                var toDate = ToDatePicker.SelectedDate ?? DateTime.Now;

                var ditariH = context.DitariH
                    .Where(h => h.Data >= fromDate && h.Data <= toDate.AddDays(1))
                    .OrderByDescending(h => h.Data)
                    .ThenByDescending(h => h.Id)
                    .Take(5000)
                    .ToList();

                var headerIds = ditariH.Select(h => h.Id).ToList();
                var ditariD = context.DitariD
                    .Where(d => d.DitariHID.HasValue && headerIds.Contains(d.DitariHID.Value))
                    .ToList();

                var groupedDetails = ditariD.GroupBy(d => d.DitariHID).ToDictionary(g => g.Key, g => g.ToList());

                var grouped = ditariH.Select(header => {
                        var details = groupedDetails.ContainsKey(header.Id) ? groupedDetails[header.Id] : new List<DitariD>();

                        return new SaleDisplay
                        {
                            Id = header.Id,
                            SqlServerNumri = header.Id,
                            ReceiptNumber = header.Id.ToString(),
                            Date = header.Data ?? DateTime.Now,
                            CustomerName = header.Klienti ?? "Qytetar",
                            TotalAmount = (decimal)(header.Totali ?? details.Sum(x => x.Totali ?? 0)),
                            VATAmount = (decimal)(header.Tatimi ?? details.Sum(x => x.Tatimi ?? 0)),
                            PaymentMethod = GetPaymentMethodName(header.TipiPageses),
                            Cashier = header.Punetori ?? string.Empty,
                            ItemCount = details.Count,
                            PaidAmount = (decimal)(header.Paguar ?? 0),
                            LeftAmount = (decimal)(header.Mbetur ?? 0),
                            Notes = header.Verejtje ?? string.Empty,
                            IsFiscal = false
                        };
                    }).ToList();

                _allSales = grouped;
                ApplyFilters();

                var totalSales = _allSales.Sum(s => s.TotalAmount);
                var totalVAT = _allSales.Sum(s => s.VATAmount);
                var receiptCount = _allSales.Count;
                var averageSale = receiptCount > 0 ? totalSales / receiptCount : 0;

                TotalSalesText.Text = $"{totalSales:N2} €";
                TotalVATText.Text = $"{totalVAT:N2} €";
                TotalReceiptsText.Text = receiptCount.ToString();
                AverageSaleText.Text = $"{averageSale:N2} €";

                if (SearchTextBox != null)
                    SearchTextBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private static string GetPaymentMethodName(string? methodId)
        {
            if (int.TryParse(methodId, out int methodNum))
            {
                return methodNum switch
                {
                    1 => "Para në dorë",
                    2 => "Kartë",
                    3 => "Transfer bankar",
                    _ => "Para në dorë"
                };
            }
            return "Para në dorë";
        }
        
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSalesData();
        }
        
        private void SalesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSale = SalesDataGrid.SelectedItem as SaleDisplay;
            
            if (_selectedSale != null)
            {
                try
                {
                    using var context = new POSDbContext();
                    
                    if (POSDbContext.UseSqlServer && _selectedSale.SqlServerNumri.HasValue)
                    {
                        // Load items from DitariD for this receipt header ID
                        var items = context.DitariD
                            .Where(d => d.DitariHID == _selectedSale.SqlServerNumri)
                            .AsEnumerable()
                            .Select(d => new SaleItemDisplay
                            {
                                Id = d.Id,
                                Barcode = d.Barkodi ?? string.Empty,
                                ArticleName = d.Emertimi ?? string.Empty,
                                Quantity = (decimal)(d.Sasia ?? 0),
                                Price = (decimal)(d.Cmimi ?? 0),
                                DiscountPercent = (decimal)(d.Zbritja ?? 0),
                                DiscountValue = (decimal)(d.Zbritja ?? 0),
                                VATRate = (decimal)(d.Vat ?? d.Tatimi ?? 18),
                                VATValue = (decimal)(d.Tatimi ?? 0),
                                TotalValue = (decimal)(d.Totali ?? 0)
                            })
                            .ToList();
                        
                        ReceiptItemsDataGrid.ItemsSource = items;
                    }
                    else
                    {
                        // SQLite mode - load from Receipts
                        var receipt = context.Receipts
                            .Include(r => r.Items)
                            .ThenInclude(i => i.Article)
                            .FirstOrDefault(r => r.Id == _selectedSale.Id);
                        
                        if (receipt != null)
                        {
                            var items = receipt.Items.Select(i => new SaleItemDisplay
                            {
                                Id = i.Id,
                                Barcode = i.Barcode,
                                ArticleName = i.ArticleName,
                                Quantity = i.Quantity,
                                Price = i.Price,
                                DiscountPercent = i.DiscountPercent,
                                DiscountValue = i.DiscountValue,
                                VATRate = i.VATRate,
                                VATValue = i.VATValue,
                                TotalValue = i.TotalValue
                            }).ToList();
                            
                            ReceiptItemsDataGrid.ItemsSource = items;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të artikujve: {ex.Message}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                ReceiptItemsDataGrid.ItemsSource = null;
            }
        }
        
        private void SalesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewReceipt_Click(sender, e);
        }
        
        private void ViewReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSale == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një faturë!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                using var context = new POSDbContext();
                
                // Create detailed receipt view
                var detailsText = new System.Text.StringBuilder();
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine("              FATURA - DETAJE              ");
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine();
                detailsText.AppendLine($"Nr. Faturës:     {_selectedSale.ReceiptNumber}");
                detailsText.AppendLine($"Data:            {_selectedSale.Date:dd/MM/yyyy HH:mm:ss}");
                detailsText.AppendLine($"Klienti:         {_selectedSale.CustomerName}");
                detailsText.AppendLine($"Arkëtari:        {_selectedSale.Cashier}");
                detailsText.AppendLine($"Mënyra e pagesës: {_selectedSale.PaymentMethod}");
                detailsText.AppendLine();
                detailsText.AppendLine("───────────────────────────────────────────");
                detailsText.AppendLine("ARTIKUJT:");
                detailsText.AppendLine("───────────────────────────────────────────");
                detailsText.AppendLine();
                
                List<SaleItemDisplay> items;
                
                if (POSDbContext.UseSqlServer && _selectedSale.SqlServerNumri.HasValue)
                {
                    items = context.DitariD
                        .Where(d => d.DitariHID == _selectedSale.SqlServerNumri)
                        .AsEnumerable()
                        .Select(d => new SaleItemDisplay
                        {
                            Barcode = d.Barkodi ?? string.Empty,
                            ArticleName = d.Emertimi ?? string.Empty,
                            Quantity = (decimal)(d.Sasia ?? 0),
                            Price = (decimal)(d.Cmimi ?? 0),
                            DiscountPercent = (decimal)(d.Zbritja ?? 0),
                            DiscountValue = (decimal)(d.Zbritja ?? 0),
                            VATRate = (decimal)(d.Vat ?? d.Tatimi ?? 18),
                            VATValue = (decimal)(d.Tatimi ?? 0),
                            TotalValue = (decimal)(d.Totali ?? 0)
                        })
                        .ToList();
                }
                else
                {
                    var receipt = context.Receipts
                        .Include(r => r.Items)
                        .FirstOrDefault(r => r.Id == _selectedSale.Id);
                    
                    items = receipt?.Items?.Select(i => new SaleItemDisplay
                    {
                        Barcode = i.Barcode,
                        ArticleName = i.ArticleName,
                        Quantity = i.Quantity,
                        Price = i.Price,
                        DiscountPercent = i.DiscountPercent,
                        DiscountValue = i.DiscountValue,
                        VATRate = i.VATRate,
                        VATValue = i.VATValue,
                        TotalValue = i.TotalValue
                    }).ToList() ?? new List<SaleItemDisplay>();
                }
                
                int itemNo = 1;
                foreach (var item in items)
                {
                    detailsText.AppendLine($"{itemNo}. {item.ArticleName}");
                    detailsText.AppendLine($"   Barkodi:   {item.Barcode}");
                    detailsText.AppendLine($"   Sasia:     {item.Quantity:N2} x {item.Price:N2} € = {item.Quantity * item.Price:N2} €");
                    
                    if (item.DiscountPercent > 0)
                    {
                        detailsText.AppendLine($"   Zbritja:   -{item.DiscountPercent:N2}% (-{item.DiscountValue:N2} €)");
                    }
                    
                    detailsText.AppendLine($"   TVSH {item.VATRate}%:   {item.VATValue:N2} €");
                    detailsText.AppendLine($"   Totali:    {item.TotalValue:N2} €");
                    detailsText.AppendLine();
                    itemNo++;
                }
                
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine($"Totali:          {_selectedSale.TotalAmount:N2} €");
                detailsText.AppendLine($"TVSH:            {_selectedSale.VATAmount:N2} €");
                detailsText.AppendLine($"Paguar:          {_selectedSale.PaidAmount:N2} €");
                
                if (_selectedSale.LeftAmount > 0)
                {
                    detailsText.AppendLine($"Kusur:           {_selectedSale.LeftAmount:N2} €");
                }
                
                detailsText.AppendLine("═══════════════════════════════════════════");
                
                if (!string.IsNullOrWhiteSpace(_selectedSale.Notes))
                {
                    detailsText.AppendLine();
                    detailsText.AppendLine($"Vërejtje: {_selectedSale.Notes}");
                }
                
                MessageBox.Show(detailsText.ToString(), $"Fatura - {_selectedSale.ReceiptNumber}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë shfaqjes së faturës: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSale == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një faturë për printim!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                // Create a Receipt object for printing
                var receipt = new Receipt
                {
                    ReceiptNumber = _selectedSale.ReceiptNumber,
                    Date = _selectedSale.Date,
                    BuyerName = _selectedSale.CustomerName,
                    CashierName = _selectedSale.Cashier,
                    PaymentMethod = _selectedSale.PaymentMethod,
                    TotalAmount = _selectedSale.TotalAmount,
                    TaxAmount = _selectedSale.VATAmount,
                    PaidAmount = _selectedSale.PaidAmount,
                    LeftAmount = _selectedSale.LeftAmount,
                    Remark = _selectedSale.Notes
                };
                
                // Load items
                using var context = new POSDbContext();
                
                if (POSDbContext.UseSqlServer && _selectedSale.SqlServerNumri.HasValue)
                {
                    var items = context.DitariD
                        .Where(d => d.DitariHID == _selectedSale.SqlServerNumri)
                        .ToList();

                    receipt.Items = items.Select(d => new ReceiptItem
                    {
                        Barcode = d.Barkodi ?? string.Empty,
                        ArticleName = d.Emertimi ?? string.Empty,
                        Quantity = (decimal)(d.Sasia ?? 0),
                        Price = (decimal)(d.Cmimi ?? 0),
                        DiscountPercent = (decimal)(d.Zbritja ?? 0),
                        DiscountValue = (decimal)(d.Zbritja ?? 0),
                        VATRate = (decimal)(d.Vat ?? d.Tatimi ?? 18),
                        VATValue = (decimal)(d.Tatimi ?? 0),
                        TotalValue = (decimal)(d.Totali ?? 0)
                    }).ToList();
                }
                else
                {
                    var existingReceipt = context.Receipts
                        .Include(r => r.Items)
                        .FirstOrDefault(r => r.Id == _selectedSale.Id);
                    
                    if (existingReceipt != null)
                    {
                        receipt.Items = existingReceipt.Items;
                    }
                }
                
                var printerService = new Services.ReceiptPrinterService();
                printerService.PrintNonFiscalReceipt(receipt);
                
                MessageBox.Show("Fatura u printua me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Shitjet");
                
                // Headers
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "Nr. Faturës";
                worksheet.Cell(1, 3).Value = "Data";
                worksheet.Cell(1, 4).Value = "Klienti";
                worksheet.Cell(1, 5).Value = "Totali (€)";
                worksheet.Cell(1, 6).Value = "TVSH (€)";
                worksheet.Cell(1, 7).Value = "Mënyra e pagesës";
                worksheet.Cell(1, 8).Value = "Arkëtari";
                
                // Style headers
                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                
                // Data
                var sales = SalesDataGrid.ItemsSource as List<SaleDisplay>;
                if (sales != null)
                {
                    int row = 2;
                    foreach (var sale in sales)
                    {
                        worksheet.Cell(row, 1).Value = sale.Id;
                        worksheet.Cell(row, 2).Value = sale.ReceiptNumber;
                        worksheet.Cell(row, 3).Value = sale.Date.ToString("dd/MM/yyyy HH:mm");
                        worksheet.Cell(row, 4).Value = sale.CustomerName ?? "";
                        worksheet.Cell(row, 5).Value = sale.TotalAmount;
                        worksheet.Cell(row, 6).Value = sale.VATAmount;
                        worksheet.Cell(row, 7).Value = sale.PaymentMethod ?? "";
                        worksheet.Cell(row, 8).Value = sale.Cashier ?? "";
                        row++;
                    }
                }
                
                // Auto-fit columns
                worksheet.Columns().AdjustToContents();
                
                // Save
                var fileName = $"Shitjet_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
                workbook.SaveAs(filePath);
                
                MessageBox.Show($"Të dhënat u eksportuan me sukses në:\n{filePath}", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Open file
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë eksportimit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
