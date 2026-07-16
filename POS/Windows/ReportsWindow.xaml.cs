using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using KosovaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class ReportsWindow : Window
    {
        public ReportsWindow()
        {
            InitializeComponent();
            LoadReportsData();
        }
        
        private void LoadReportsData()
        {
            try
            {
                using var context = new POSDbContext();
                
                var now = DateTime.Now;
                var today = now.Date;
                var weekStart = today.AddDays(-(int)today.DayOfWeek);
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var yearStart = new DateTime(now.Year, 1, 1);
                
                double todaySales = 0, weekSales = 0, monthSales = 0, yearSales = 0;
                double todayPurchases = 0, weekPurchases = 0, monthPurchases = 0, yearPurchases = 0;
                double totalRevenue = 0, totalCost = 0;
                double vatPayable = 0;
                int receiptItemsCount = 0;
                
                // Load sales from DitariD for SQL Server - Include DitariH to access Data property
                var ditariD = context.DitariD
                    .Include(d => d.DitariH)
                    .Where(d => d.DitariH != null && d.DitariH.Data.HasValue)
                    .ToList();

                todaySales = ditariD.Where(d => d.DitariH!.Data!.Value.Date == today).Sum(d => d.VleraMeTvsh ?? 0);
                weekSales = ditariD.Where(d => d.DitariH!.Data!.Value.Date >= weekStart).Sum(d => d.VleraMeTvsh ?? 0);
                monthSales = ditariD.Where(d => d.DitariH!.Data!.Value.Date >= monthStart).Sum(d => d.VleraMeTvsh ?? 0);
                yearSales = ditariD.Where(d => d.DitariH!.Data!.Value.Date >= yearStart).Sum(d => d.VleraMeTvsh ?? 0);
                totalRevenue = ditariD.Sum(d => d.VleraMeTvsh ?? 0);
                vatPayable = ditariD.Sum(d => d.Tvsh ?? 0);

                // Load purchases from DitariH for SQL Server
                var ditariH = context.DitariH
                    .Where(h => h.Data.HasValue)
                    .ToList();

                todayPurchases = ditariH.Where(h => h.Data!.Value.Date == today).Sum(h => h.VleraMeTvsh ?? 0);
                weekPurchases = ditariH.Where(h => h.Data!.Value.Date >= weekStart).Sum(h => h.VleraMeTvsh ?? 0);
                monthPurchases = ditariH.Where(h => h.Data!.Value.Date >= monthStart).Sum(h => h.VleraMeTvsh ?? 0);
                yearPurchases = ditariH.Where(h => h.Data!.Value.Date >= yearStart).Sum(h => h.VleraMeTvsh ?? 0);
                totalCost = ditariH.Sum(h => h.VleraMeTvsh ?? 0);

                // Count unique articles sold
                receiptItemsCount = ditariD.Select(d => d.ArtikullId).Where(a => a.HasValue).Distinct().Count();
                
                TodaySalesText.Text = $"{todaySales:N2} €";
                WeekSalesText.Text = $"{weekSales:N2} €";
                MonthSalesText.Text = $"{monthSales:N2} €";
                YearSalesText.Text = $"{yearSales:N2} €";
                
                TodayPurchasesText.Text = $"{todayPurchases:N2} €";
                WeekPurchasesText.Text = $"{weekPurchases:N2} €";
                MonthPurchasesText.Text = $"{monthPurchases:N2} €";
                YearPurchasesText.Text = $"{yearPurchases:N2} €";
                
                // Profit analysis
                var netProfit = totalRevenue - totalCost;
                TotalRevenueText.Text = $"{totalRevenue:N2} €";
                TotalCostText.Text = $"{totalCost:N2} €";
                NetProfitText.Text = $"{netProfit:N2} €";
                
                // Stock value - Load from SQL Server if available
                double stockValue = 0;
                int lowStockCount = 0;
                if (POSDbContext.UseSqlServer)
                {
                    var artikujt = context.Artikujt.ToList();
                    stockValue = artikujt.Sum(a => (double)((a.Sasia ?? 0) * (a.CFurnizimit ?? 0)));
                    lowStockCount = artikujt.Count(a => (a.Sasia ?? 0) < 10);
                }
                else
                {
                    var articles = context.Articles.Where(a => a.IsActive).ToList();
                    stockValue = articles.Sum(a => (double)(a.StockQuantity * a.PurchasePrice));
                    lowStockCount = articles.Count(a => a.StockQuantity < 10);
                }
                StockValueText.Text = $"{stockValue:N2} €";
                
                // VAT
                VATPayableText.Text = $"{vatPayable:N2} €";
                
                // Top products count
                TopProductCountText.Text = $"{receiptItemsCount} artikuj";
                
                // Low stock
                LowStockCountText.Text = $"{lowStockCount} artikuj kanë stok të ulët";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të raporteve: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SalesDetails_Click(object sender, RoutedEventArgs e)
        {
            var salesWindow = new SalesWindow();
            salesWindow.ShowDialog();
        }
        
        private void PurchasesDetails_Click(object sender, RoutedEventArgs e)
        {
            var purchasesWindow = new PurchasesWindow();
            purchasesWindow.ShowDialog();
        }
        
        private void StockReport_Click(object sender, RoutedEventArgs e)
        {
            var articlesWindow = new ArticlesWindow();
            articlesWindow.ShowDialog();
        }
        
        private void VATReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                string report = "RAPORTI I TVSH-së\n\n";
                report += "Data\t\t\tShitjet\t\tTVSH\n";
                report += new string('-', 60) + "\n";
                
                if (POSDbContext.UseSqlServer)
                {
                    // Use DitariD joined with DitariH for SQL Server - filter on mapped DitariH.Data
                    var vatReport = context.DitariD
                        .Include(d => d.DitariH)
                        .Where(d => d.DitariH.Data.HasValue)
                        .ToList()
                        .GroupBy(d => d.DitariH!.Data!.Value.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            TotalSales = g.Sum(d => d.VleraMeTvsh ?? 0),
                            TotalVAT = g.Sum(d => d.Tvsh ?? 0)
                        })
                        .OrderByDescending(x => x.Date)
                        .ToList();
                    
                    foreach (var item in vatReport.Take(30))
                    {
                        report += $"{item.Date:dd/MM/yyyy}\t\t{item.TotalSales:N2} €\t{item.TotalVAT:N2} €\n";
                    }
                    
                    report += new string('-', 60) + "\n";
                    report += $"TOTALI:\t\t{vatReport.Sum(x => x.TotalSales):N2} €\t{vatReport.Sum(x => x.TotalVAT):N2} €\n";
                }
                else
                {
                    // SQLite mode
                    var vatReport = context.Receipts
                        .ToList()
                        .GroupBy(r => r.Date.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            TotalSales = g.Sum(r => (double)r.TotalAmount),
                            TotalVAT = g.Sum(r => (double)r.VATAmount)
                        })
                        .OrderByDescending(x => x.Date)
                        .ToList();
                    
                    foreach (var item in vatReport.Take(30))
                    {
                        report += $"{item.Date:dd/MM/yyyy}\t\t{item.TotalSales:N2} €\t{item.TotalVAT:N2} €\n";
                    }
                    
                    report += new string('-', 60) + "\n";
                    report += $"TOTALI:\t\t{vatReport.Sum(x => x.TotalSales):N2} €\t{vatReport.Sum(x => x.TotalVAT):N2} €\n";
                }
                
                MessageBox.Show(report, "Raporti i TVSH", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TopProducts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                
                var report = "TOP 20 ARTIKUJT MË TË SHITUR\n\n";
                report += "Emri\t\t\t\tSasia\t\tVlera\n";
                report += new string('-', 70) + "\n";
                
                if (POSDbContext.UseSqlServer)
                {
                    // Use mapped Emertimi column instead of [NotMapped] Artikulli
                    var topProducts = context.DitariD
                        .Where(d => !string.IsNullOrEmpty(d.Emertimi))
                        .ToList()
                        .GroupBy(d => new { d.ArtikujID, Name = d.Emertimi })
                        .Select(g => new
                        {
                            ArticleName = g.Key.Name ?? "Artikull",
                            TotalQuantity = g.Sum(d => d.Sasia ?? 0),
                            TotalValue = g.Sum(d => d.VleraMeTvsh ?? 0)
                        })
                        .OrderByDescending(x => x.TotalValue)
                        .Take(20)
                        .ToList();
                    
                    foreach (var item in topProducts)
                    {
                        var name = item.ArticleName.Length > 30 ? item.ArticleName.Substring(0, 30) : item.ArticleName;
                        report += $"{name}\t\t{item.TotalQuantity:N0}\t\t{item.TotalValue:N2} €\n";
                    }
                }
                else
                {
                    // SQLite mode
                    var topProducts = context.ReceiptItems
                        .Include(ri => ri.Article)
                        .ToList()
                        .Where(ri => ri.Article != null)
                        .GroupBy(ri => ri.ArticleId)
                        .Select(g => new
                        {
                            ArticleName = g.First().Article!.Name,
                            TotalQuantity = g.Sum(ri => (double)ri.Quantity),
                            TotalValue = g.Sum(ri => (double)ri.TotalValue)
                        })
                        .OrderByDescending(x => x.TotalValue)
                        .Take(20)
                        .ToList();
                    
                    foreach (var item in topProducts)
                    {
                        var name = item.ArticleName.Length > 30 ? item.ArticleName.Substring(0, 30) : item.ArticleName;
                        report += $"{name}\t\t{item.TotalQuantity:N0}\t\t{item.TotalValue:N2} €\n";
                    }
                }
                
                MessageBox.Show(report, "Artikujt më të shitur", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void LowStockReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show loading cursor
                this.Cursor = System.Windows.Input.Cursors.Wait;

                if (POSDbContext.UseSqlServer)
                {
                    // SQL Server mode - use ArticleDataService
                    var lowStock = await System.Threading.Tasks.Task.Run(() =>
                    {
                        return ArticleDataService.GetAllArticles()
                            .Where(a => a.IsActive && a.StockQuantity < 10)
                            .OrderBy(a => a.StockQuantity)
                            .ToList();
                    });
                    
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    ShowLowStockWindow(lowStock);
                }
                else
                {
                    // SQLite mode
                    var lowStock = await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var context = new POSDbContext();
                        return context.Articles
                            .Where(a => a.IsActive && a.StockQuantity < 10)
                            .ToList()
                            .OrderBy(a => a.StockQuantity)
                            .ToList();
                    });
                    
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    ShowLowStockWindow(lowStock);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        
        private void ShowLowStockWindow(List<Article> lowStock)
        {
            // Create a window to show the low stock items
            var lowStockWindow = new Window
            {
                Title = "Artikujt me stok të ulët",
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["BackgroundColor"]
            };
            
            var grid = new System.Windows.Controls.Grid
            {
                Margin = new Thickness(20)
            };
            
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            
            // Header
            var headerPanel = new System.Windows.Controls.StackPanel();
            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "\t&#xf071; Artikujt me stok të ulët (< 10 njësi)",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryColor"]
            };
            var subtitleText = new System.Windows.Controls.TextBlock
            {
                Text = $"Gjetur {lowStock.Count} artikuj që kanë nevojë për rifurnizim",
                FontSize = 14,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondary"],
                Margin = new Thickness(0, 5, 0, 0)
            };
            headerPanel.Children.Add(titleText);
            headerPanel.Children.Add(subtitleText);
            System.Windows.Controls.Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);
            
            // DataGrid with scroll
            var dataGrid = new System.Windows.Controls.DataGrid
            {
                Margin = new Thickness(0, 20, 0, 20),
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = System.Windows.Controls.DataGridSelectionMode.Single,
                GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                HeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility.Column,
                AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 0, 0, 0))
            };
            
            // Define columns
            dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Barkodi",
                Binding = new System.Windows.Data.Binding("Barcode"),
                Width = new System.Windows.Controls.DataGridLength(120)
            });
            dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Emri i Artikullit",
                Binding = new System.Windows.Data.Binding("Name"),
                Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
            });
            dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Kategoria",
                Binding = new System.Windows.Data.Binding("Category"),
                Width = new System.Windows.Controls.DataGridLength(150)
            });
            dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Furnizuesi",
                Binding = new System.Windows.Data.Binding("Supplier"),
                Width = new System.Windows.Controls.DataGridLength(150)
            });
            dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Stoku",
                Binding = new System.Windows.Data.Binding("StockQuantity") { StringFormat = "N2" },
                Width = new System.Windows.Controls.DataGridLength(100)
            });
            dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Çmimi Shitës (€)",
                Binding = new System.Windows.Data.Binding("SalesPrice") { StringFormat = "N2" },
                Width = new System.Windows.Controls.DataGridLength(120)
            });
            
            dataGrid.ItemsSource = lowStock;
            System.Windows.Controls.Grid.SetRow(dataGrid, 1);
            grid.Children.Add(dataGrid);
            
            // Close button
            var closeButton = new System.Windows.Controls.Button
            {
                Content = "🚪 Mbyll",
                Width = 120,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)Application.Current.Resources["ModernButton"],
                Background = (System.Windows.Media.Brush)Application.Current.Resources["DangerColor"]
            };
            closeButton.Click += (s, args) => lowStockWindow.Close();
            System.Windows.Controls.Grid.SetRow(closeButton, 2);
            grid.Children.Add(closeButton);
            
            lowStockWindow.Content = grid;
            
            if (lowStock.Count == 0)
            {
                MessageBox.Show("Nuk ka artikuj me stok të ulët!\nTë gjithë artikujt kanë stok të mjaftueshëm.", 
                    "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                lowStockWindow.ShowDialog();
            }
        }
        
        private void PrintDailySalesReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                var today = DateTime.Now.Date;
                
                // Get filter type
                string filterType = GetReceiptFilterType();
                string filterText = GetFilterTypeText(filterType);
                
                if (POSDbContext.UseSqlServer)
                {
                    // Use DitariH.Data (mapped) to filter by date, then get related DitariD
                    var todayHeaderIds = context.DitariH
                        .Where(h => h.Data.HasValue && h.Data.Value.Date == today)
                        .Select(h => h.Id)
                        .ToList();
                    var ditariD = context.DitariD
                        .Include(d => d.DitariH)
                        .Where(d => d.DitariHID.HasValue && todayHeaderIds.Contains(d.DitariHID.Value))
                        .ToList();

                    if (filterType == "fiscal")
                        ditariD = ditariD.Where(d => d.DitariHID.HasValue).ToList();

                    if (ditariD.Count == 0)
                    {
                        MessageBox.Show($"Nuk ka shitje {filterText} për ditën e sotme!", "Informacion", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    var report = GenerateDailySalesReportFromSqlServer(ditariD, today, filterText);
                    SaveAndOpenReport(report, $"Raporti_Ditore_{filterType}_{today:yyyy-MM-dd}.txt");
                }
                else
                {
                    // SQLite mode
                    var receipts = context.Receipts
                        .Where(r => r.Date.Date == today)
                        .ToList();
                    
                    // Filter by receipt type for SQLite
                    if (filterType == "fiscal")
                        receipts = receipts.Where(r => r.ReceiptType == ReceiptType.Fiscal).ToList();
                    else if (filterType == "nonfiscal")
                        receipts = receipts.Where(r => r.ReceiptType != ReceiptType.Fiscal).ToList();
                    
                    if (receipts.Count == 0)
                    {
                        MessageBox.Show($"Nuk ka shitje {filterText} për ditën e sotme!", "Informacion", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    var report = GenerateDailySalesReport(receipts, today, filterText);
                    SaveAndOpenReport(report, $"Raporti_Ditore_{filterType}_{today:yyyy-MM-dd}.txt");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string GetReceiptFilterType()
        {
            if (FiscalOnlyRadio.IsChecked == true) return "fiscal";
            if (NonFiscalOnlyRadio.IsChecked == true) return "nonfiscal";
            return "all";
        }
        
        private string GetFilterTypeText(string filterType)
        {
            return filterType switch
            {
                "fiscal" => "(fiskale)",
                "nonfiscal" => "(jofiskale)",
                _ => ""
            };
        }
        
        private List<Models.BMDData.DitariD> ApplyReceiptTypeFilter(List<Models.BMDData.DitariD> items, string filterType)
        {
            return filterType switch
            {
                "fiscal" => items.Where(d => d.DitariHID.HasValue).ToList(),
                "nonfiscal" => items.Where(d => !d.DitariHID.HasValue).ToList(),
                _ => items
            };
        }
        
        private void PrintMonthlySalesReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                var now = DateTime.Now;
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                
                // Get filter type
                string filterType = GetReceiptFilterType();
                string filterText = GetFilterTypeText(filterType);
                
                if (POSDbContext.UseSqlServer)
                {
                    // Use DitariH.Data (mapped) to filter by date, then get related DitariD
                    var monthHeaderIds = context.DitariH
                        .Where(h => h.Data.HasValue && h.Data >= monthStart && h.Data <= monthEnd.AddDays(1))
                        .Select(h => h.Id)
                        .ToList();
                    var ditariD = context.DitariD
                        .Include(d => d.DitariH)
                        .Where(d => d.DitariHID.HasValue && monthHeaderIds.Contains(d.DitariHID.Value))
                        .ToList();

                    if (filterType == "fiscal")
                        ditariD = ditariD.Where(d => d.DitariHID.HasValue).ToList();

                    if (ditariD.Count == 0)
                    {
                        MessageBox.Show($"Nuk ka shitje {filterText} për këtë muaj!", "Informacion", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    var report = GenerateMonthlySalesReportFromSqlServer(ditariD, monthStart, filterText);
                    SaveAndOpenReport(report, $"Raporti_Mujor_{filterType}_{monthStart:yyyy-MM}.txt");
                }
                else
                {
                    // SQLite mode
                    var receipts = context.Receipts
                        .Where(r => r.Date >= monthStart && r.Date <= monthEnd)
                        .ToList();
                    
                    // Filter by receipt type for SQLite
                    if (filterType == "fiscal")
                        receipts = receipts.Where(r => r.ReceiptType == ReceiptType.Fiscal).ToList();
                    else if (filterType == "nonfiscal")
                        receipts = receipts.Where(r => r.ReceiptType != ReceiptType.Fiscal).ToList();
                    
                    if (receipts.Count == 0)
                    {
                        MessageBox.Show($"Nuk ka shitje {filterText} për këtë muaj!", "Informacion", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    var report = GenerateMonthlySalesReport(receipts, monthStart, filterText);
                    SaveAndOpenReport(report, $"Raporti_Mujor_{filterType}_{monthStart:yyyy-MM}.txt");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string GenerateDailySalesReportFromSqlServer(List<Models.BMDData.DitariD> ditariD, DateTime date, string filterText = "")
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"                    RAPORTI DITORE I SHITJEVE {filterText}".TrimEnd());
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Data: {date:dd/MM/yyyy}");
            sb.AppendLine($"Gjeneruar: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            if (!string.IsNullOrEmpty(filterText))
                sb.AppendLine($"Filtri: {filterText}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            // Group by receipt number
            var receiptGroups = ditariD.GroupBy(d => d.Numri).ToList();
            
            var totalSales = ditariD.Sum(d => d.VleraMeTvsh ?? 0);
            var totalVAT = ditariD.Sum(d => d.Tvsh ?? 0);
            var totalReceipts = receiptGroups.Count;
            
            sb.AppendLine($"Numri i faturave:        {totalReceipts}");
            sb.AppendLine($"Shitjet totale:          {totalSales:N2} €");
            sb.AppendLine($"TVSH totale:             {totalVAT:N2} €");
            sb.AppendLine();
            
            // Hourly breakdown
            var hourlyData = ditariD
                .Where(d => d.Data.HasValue)
                .GroupBy(d => d.Data!.Value.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(d => d.VleraMeTvsh ?? 0), Count = g.Select(d => d.Numri).Distinct().Count() })
                .OrderBy(x => x.Hour)
                .ToList();
            
            sb.AppendLine("SHPËRNDARJA SIPAS ORËVE:");
            sb.AppendLine("  Ora        Fatura        Vlera");
            foreach (var h in hourlyData)
            {
                sb.AppendLine($"  {h.Hour:D2}:00      {h.Count,5}    {h.Total,12:N2} €");
            }
            sb.AppendLine();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        private string GenerateMonthlySalesReportFromSqlServer(List<Models.BMDData.DitariD> ditariD, DateTime month, string filterText = "")
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"                    RAPORTI MUJOR I SHITJEVE {filterText}".TrimEnd());
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Muaji: {month:MMMM yyyy}");
            sb.AppendLine($"Gjeneruar: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            if (!string.IsNullOrEmpty(filterText))
                sb.AppendLine($"Filtri: {filterText}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            var receiptGroups = ditariD.GroupBy(d => d.Numri).ToList();
            
            var totalSales = ditariD.Sum(d => d.VleraMeTvsh ?? 0);
            var totalVAT = ditariD.Sum(d => d.Tvsh ?? 0);
            var totalReceipts = receiptGroups.Count;
            var avgPerReceipt = totalReceipts > 0 ? totalSales / totalReceipts : 0;
            
            sb.AppendLine($"Numri i faturave:        {totalReceipts}");
            sb.AppendLine($"Shitjet totale:          {totalSales:N2} €");
            sb.AppendLine($"TVSH totale:             {totalVAT:N2} €");
            sb.AppendLine($"Mesatarja për faturë:    {avgPerReceipt:N2} €");
            sb.AppendLine();
            
            // Daily breakdown
            var dailyData = ditariD
                .Where(d => d.Data.HasValue)
                .GroupBy(d => d.Data!.Value.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(d => d.VleraMeTvsh ?? 0), Count = g.Select(d => d.Numri).Distinct().Count() })
                .OrderBy(x => x.Date)
                .ToList();
            
            sb.AppendLine("SHPËRNDARJA DITORE:");
            sb.AppendLine("  Data           Fatura        Vlera");
            sb.AppendLine("  ──────────────────────────────────────────");
            
            foreach (var d in dailyData)
            {
                sb.AppendLine($"  {d.Date:dd/MM/yyyy}     {d.Count,5}    {d.Total,12:N2} €");
            }
            sb.AppendLine();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        private string GenerateDailySalesReport(List<Receipt> receipts, DateTime date, string filterText = "")
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"                    RAPORTI DITORE I SHITJEVE {filterText}".TrimEnd());
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Data: {date:dd/MM/yyyy}");
            sb.AppendLine($"Gjeneruar: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            if (!string.IsNullOrEmpty(filterText))
                sb.AppendLine($"Filtri: {filterText}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            // Summary
            var totalSales = receipts.Sum(r => (double)r.TotalAmount);
            var totalVAT = receipts.Sum(r => (double)r.VATAmount);
            var totalReceipts = receipts.Count;
            
            sb.AppendLine($"Numri i faturave:        {totalReceipts}");
            sb.AppendLine($"Shitjet totale:          {totalSales:N2} €");
            sb.AppendLine($"TVSH totale:             {totalVAT:N2} €");
            sb.AppendLine();
            
            // Payment methods
            var paymentMethods = receipts.GroupBy(r => r.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .ToList();
            
            sb.AppendLine("METODAT E PAGESËS:");
            foreach (var pm in paymentMethods)
            {
                sb.AppendLine($"  {pm.Method,-20} {pm.Count,5} fatura    {pm.Total,12:N2} €");
            }
            sb.AppendLine();
            
            // Hourly breakdown
            var hourlyData = receipts.GroupBy(r => r.Date.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .OrderBy(x => x.Hour)
                .ToList();
            
            sb.AppendLine("SHPËRNDARJA SIPAS ORËVE:");
            sb.AppendLine("  Ora        Fatura        Vlera");
            foreach (var h in hourlyData)
            {
                sb.AppendLine($"  {h.Hour:D2}:00      {h.Count,5}    {h.Total,12:N2} €");
            }
            sb.AppendLine();
            
            // Detailed transactions
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine("DETAJET E TRANSAKSIONEVE:");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            sb.AppendLine("Ora    Nr. Faturës        Klienti              Shuma      TVSH   ");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            
            foreach (var receipt in receipts.OrderBy(r => r.Date))
            {
                var time = receipt.Date.ToString("HH:mm");
                var number = receipt.ReceiptNumber.Length > 15 ? 
                    receipt.ReceiptNumber.Substring(0, 15) : receipt.ReceiptNumber;
                var buyer = receipt.BuyerName.Length > 18 ? 
                    receipt.BuyerName.Substring(0, 18) : receipt.BuyerName;
                
                sb.AppendLine($"{time}  {number,-15}  {buyer,-18}  {receipt.TotalAmount,8:N2}  {receipt.VATAmount,6:N2}");
            }
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        private string GenerateMonthlySalesReport(List<Receipt> receipts, DateTime month, string filterText = "")
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"                    RAPORTI MUJOR I SHITJEVE {filterText}".TrimEnd());
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Muaji: {month:MMMM yyyy}");
            sb.AppendLine($"Gjeneruar: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            if (!string.IsNullOrEmpty(filterText))
                sb.AppendLine($"Filtri: {filterText}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            // Summary
            var totalSales = receipts.Sum(r => (double)r.TotalAmount);
            var totalVAT = receipts.Sum(r => (double)r.VATAmount);
            var totalReceipts = receipts.Count;
            var avgPerReceipt = totalReceipts > 0 ? totalSales / totalReceipts : 0;
            
            sb.AppendLine($"Numri i faturave:        {totalReceipts}");
            sb.AppendLine($"Shitjet totale:          {totalSales:N2} €");
            sb.AppendLine($"TVSH totale:             {totalVAT:N2} €");
            sb.AppendLine($"Mesatarja për faturë:    {avgPerReceipt:N2} €");
            sb.AppendLine();
            
            // Daily breakdown
            var dailyData = receipts.GroupBy(r => r.Date.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();
            
            sb.AppendLine("SHPËRNDARJA DITORE:");
            sb.AppendLine("  Data           Fatura        Vlera");
            sb.AppendLine("  ──────────────────────────────────────────");
            
            foreach (var d in dailyData)
            {
                sb.AppendLine($"  {d.Date:dd/MM/yyyy}     {d.Count,5}    {d.Total,12:N2} €");
            }
            sb.AppendLine();
            
            // Payment methods
            var paymentMethods = receipts.GroupBy(r => r.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .OrderByDescending(x => x.Total)
                .ToList();
            
            sb.AppendLine("METODAT E PAGESËS:");
            foreach (var pm in paymentMethods)
            {
                var percentage = (pm.Total / totalSales) * 100;
                sb.AppendLine($"  {pm.Method,-20} {pm.Count,5} ({percentage,5:F1}%)   {pm.Total,12:N2} €");
            }
            sb.AppendLine();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        private void SaveAndOpenReport(string content, string filename)
        {
            try
            {
                var reportsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "KosovaPOS", 
                    "Raportet"
                );
                
                if (!Directory.Exists(reportsDir))
                {
                    Directory.CreateDirectory(reportsDir);
                }
                
                var filePath = Path.Combine(reportsDir, filename);
                System.IO.File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);
                
                var result = MessageBox.Show(
                    $"Raporti u ruajt në:\n{filePath}\n\nDëshironi ta hapni?", 
                    "Raporti u gjenerua", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Information
                );
                
                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes së raportit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
