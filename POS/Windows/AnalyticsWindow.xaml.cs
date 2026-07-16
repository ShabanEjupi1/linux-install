using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using KosovaPOS.Database;
using Microsoft.EntityFrameworkCore;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace KosovaPOS.Windows
{
    public partial class AnalyticsWindow : Window
    {
        private DateTime _startDate;
        private DateTime _endDate;

        // Named class so WPF DataGrid reflection can resolve properties
        private class LowStockItemVm
        {
            public string Barcode { get; set; } = "-";
            public string Name { get; set; } = string.Empty;
            public decimal StockQuantity { get; set; }
            public string Category { get; set; } = string.Empty;
        }

        public AnalyticsWindow()
        {
            InitializeComponent();
            SetPeriod(0); // Default to today
            LoadAllData();
        }

        private void SetPeriod(int index)
        {
            var now = DateTime.Now;
            _endDate = now;

            switch (index)
            {
                case 0: // Sot
                    _startDate = now.Date;
                    break;
                case 1: // Kjo javë
                    _startDate = now.Date.AddDays(-(int)now.DayOfWeek);
                    break;
                case 2: // Ky muaj
                    _startDate = new DateTime(now.Year, now.Month, 1);
                    break;
                case 3: // 3 muaj
                    _startDate = now.AddMonths(-3);
                    break;
                case 4: // 6 muaj
                    _startDate = now.AddMonths(-6);
                    break;
                case 5: // Ky vit
                    _startDate = new DateTime(now.Year, 1, 1);
                    break;
                case 6: // Të gjitha
                    _startDate = DateTime.MinValue;
                    break;
            }
        }

        private async void LoadAllData()
        {
            try
            {
                this.Cursor = System.Windows.Input.Cursors.Wait;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var context = new POSDbContext();
                    bool sqlServerDataLoaded = false;

                    // Primary: SQL Server DitariD / DitariH
                    try
                    {
                        var ditariD = context.DitariD
                            .Include(d => d.DitariH)
                            .AsEnumerable()
                            .Where(d => d.DitariH != null && d.DitariH.Data.HasValue &&
                                        d.DitariH.Data.Value >= _startDate && d.DitariH.Data.Value <= _endDate)
                            .ToList();

                        var ditariH = context.DitariH
                            .Where(h => h.Data.HasValue && h.Data.Value >= _startDate && h.Data.Value <= _endDate)
                            .ToList();

                        if (ditariD.Any() || ditariH.Any())
                        {
                            sqlServerDataLoaded = true;
                            Dispatcher.Invoke(() =>
                            {
                                try { LoadSalesAnalysisFromSqlServer(ditariD); }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadSalesAnalysis error: {ex.Message}"); }
                                try { LoadProductAnalysisFromSqlServer(ditariD); }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadProductAnalysis error: {ex.Message}"); }
                                try { LoadFinancialAnalysisFromSqlServer(ditariD, ditariH); }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadFinancialAnalysis error: {ex.Message}"); }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"DitariD/DitariH load error: {ex.Message}");
                    }

                    // Fallback: KosovaPOS Receipts / ReceiptItems tables
                    if (!sqlServerDataLoaded)
                    {
                        try
                        {
                            var receipts = context.Receipts
                                .Where(r => r.Date >= _startDate && r.Date <= _endDate)
                                .ToList();

                            var receiptIds = receipts.Select(r => r.Id).ToHashSet();
                            var receiptItems = context.ReceiptItems
                                .Include(ri => ri.Article)
                                .Where(ri => receiptIds.Contains(ri.ReceiptId))
                                .ToList();

                            // Link receipt navigation property in memory
                            var receiptDict = receipts.ToDictionary(r => r.Id);
                            foreach (var item in receiptItems)
                                if (receiptDict.TryGetValue(item.ReceiptId, out var r))
                                    item.Receipt = r;

                            if (receipts.Any() || receiptItems.Any())
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    try { LoadSalesAnalysis(receipts, receiptItems); }
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadSalesAnalysis fallback error: {ex.Message}"); }
                                    try { LoadProductAnalysis(receiptItems); }
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadProductAnalysis fallback error: {ex.Message}"); }
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    TotalSalesKPI.Text = "0.00 €";
                                    TotalTransactionsKPI.Text = "0";
                                    AvgTransactionKPI.Text = "Mesatare: 0.00 €";
                                    TotalItemsSoldKPI.Text = "0";
                                    UniqueProductsKPI.Text = "0 produkte unike";
                                    PeakHoursText.Text = "Nuk ka të dhëna për periudhën e zgjedhur";
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Receipts fallback error: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllData outer error: {ex.Message}");
            }
            finally
            {
                _inventoryLoaded = true;
                await LoadInventoryAnalysisAsync();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void LoadSalesAnalysis(List<Models.Receipt> receipts, List<Models.ReceiptItem> receiptItems)
        {
            // KPIs
            var totalSales = receipts.Sum(r => (double)r.TotalAmount);
            var totalTransactions = receipts.Count;
            var avgTransaction = totalTransactions > 0 ? totalSales / totalTransactions : 0;
            var totalItemsSold = receiptItems.Sum(ri => (double)ri.Quantity);
            var uniqueProducts = receiptItems.Select(ri => ri.ArticleId).Distinct().Count();

            TotalSalesKPI.Text = $"{totalSales:N2} €";
            TotalTransactionsKPI.Text = totalTransactions.ToString("N0");
            AvgTransactionKPI.Text = $"Mesatare: {avgTransaction:N2} €";
            TotalItemsSoldKPI.Text = totalItemsSold.ToString("N0");
            UniqueProductsKPI.Text = $"{uniqueProducts} produkte unike";

            // Sales Trend Chart
            var salesByDay = receipts
                .GroupBy(r => r.Date.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(r => (double)r.TotalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            if (salesByDay.Any())
            {
                SalesTrendChart.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = salesByDay.Select(x => x.Total).ToArray(),
                        Name = "Shitjet (€)",
                        Fill = null,
                        GeometrySize = 8,
                        Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 3 }
                    }
                };

                SalesTrendChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = salesByDay.Select(x => x.Date.ToString("dd/MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };
            }

            // Hourly Sales Pattern
            var hourlyPattern = receipts
                .GroupBy(r => r.Date.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(r => (double)r.TotalAmount) })
                .OrderBy(x => x.Hour)
                .ToList();

            if (hourlyPattern.Any())
            {
                var peakHour = hourlyPattern.OrderByDescending(x => x.Total).First();
                PeakHoursText.Text = $"Orët më të ngarkuara: {peakHour.Hour}:00 - {peakHour.Hour + 1}:00 ({peakHour.Total:N2} €)";
                
                HourlySalesChart.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = Enumerable.Range(0, 24).Select(hour =>
                            hourlyPattern.FirstOrDefault(x => x.Hour == hour)?.Total ?? 0).ToArray(),
                        Name = "Shitjet (€)",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue)
                    }
                };

                HourlySalesChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToArray(),
                        LabelsRotation = 45
                    }
                };
            }
            else
            {
                PeakHoursText.Text = "Orët më të ngarkuara: Nuk ka të dhëna";
            }
        }

        // SQL Server specific methods for loading from DitariD and DitariH
        private void LoadSalesAnalysisFromSqlServer(List<Models.BMDData.DitariD> ditariD)
        {
            // Group by receipt (NUMRI) to get unique transactions
            var receiptGroups = ditariD.GroupBy(d => d.Numri).ToList();
            
            var totalSales = ditariD.Sum(d => d.VleraMeTvsh ?? 0);
            var totalTransactions = receiptGroups.Count;
            var avgTransaction = totalTransactions > 0 ? totalSales / totalTransactions : 0;
            var totalItemsSold = ditariD.Sum(d => d.Sasia ?? 0);
            var uniqueProducts = ditariD.Select(d => d.ArtikullId).Where(a => a.HasValue).Distinct().Count();

            TotalSalesKPI.Text = $"{totalSales:N2} €";
            TotalTransactionsKPI.Text = totalTransactions.ToString("N0");
            AvgTransactionKPI.Text = $"Mesatare: {avgTransaction:N2} €";
            TotalItemsSoldKPI.Text = totalItemsSold.ToString("N0");
            UniqueProductsKPI.Text = $"{uniqueProducts} produkte unike";

            // Sales Trend Chart
            var salesByDay = ditariD
                .Where(d => d.Data.HasValue)
                .GroupBy(d => d.Data!.Value.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(d => d.VleraMeTvsh ?? 0) })
                .OrderBy(x => x.Date)
                .ToList();

            if (salesByDay.Any())
            {
                SalesTrendChart.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = salesByDay.Select(x => x.Total).ToArray(),
                        Name = "Shitjet (€)",
                        Fill = null,
                        GeometrySize = 8,
                        Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 3 }
                    }
                };

                SalesTrendChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = salesByDay.Select(x => x.Date.ToString("dd/MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };
            }

            // Hourly Sales Pattern
            var hourlyPattern = ditariD
                .Where(d => d.Data.HasValue)
                .GroupBy(d => d.Data!.Value.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(d => d.VleraMeTvsh ?? 0) })
                .OrderBy(x => x.Hour)
                .ToList();

            if (hourlyPattern.Any())
            {
                var peakHour = hourlyPattern.OrderByDescending(x => x.Total).First();
                PeakHoursText.Text = $"Orët më të ngarkuara: {peakHour.Hour}:00 - {peakHour.Hour + 1}:00 ({peakHour.Total:N2} €)";
                
                HourlySalesChart.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = Enumerable.Range(0, 24).Select(hour =>
                            hourlyPattern.FirstOrDefault(x => x.Hour == hour)?.Total ?? 0).ToArray(),
                        Name = "Shitjet (€)",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue)
                    }
                };

                HourlySalesChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToArray(),
                        LabelsRotation = 45
                    }
                };
            }
            else
            {
                PeakHoursText.Text = "Orët më të ngarkuara: Nuk ka të dhëna";
            }
        }

        private void LoadProductAnalysisFromSqlServer(List<Models.BMDData.DitariD> ditariD)
        {
            // Top 10 Products by article code
            var topProducts = ditariD
                .Where(d => d.ArtikullId.HasValue)
                .GroupBy(d => new { d.ArtikullId, Name = d.Artikulli ?? "Artikull" })
                .Select(g => new
                {
                    ProductName = g.Key.Name,
                    Quantity = g.Sum(d => d.Sasia ?? 0),
                    Revenue = g.Sum(d => d.VleraMeTvsh ?? 0)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            if (topProducts.Any())
            {
                TopProductsChart.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = topProducts.Select(x => x.Revenue).ToArray(),
                        Name = "Të ardhurat (€)",
                        Fill = new SolidColorPaint(new SKColor(34, 197, 94))
                    }
                };

                TopProductsChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = topProducts.Select(x => 
                            x.ProductName != null && x.ProductName.Length > 20 
                                ? x.ProductName.Substring(0, 20) + "..." 
                                : x.ProductName ?? "").ToArray(),
                        LabelsRotation = 45
                    }
                };
            }

            // Category analysis - get categories from Artikujt table since DitariD.Kategoria may be empty
            using var context = new POSDbContext();
            var artikujtCategories = context.Artikujt
                .Where(a => !string.IsNullOrEmpty(a.Kategoria))
                .ToDictionary(a => a.Id, a => a.Kategoria ?? "Pa kategori");
            
            // Join sales data with article categories
            var categoryData = ditariD
                .Where(d => d.ArtikullId.HasValue)
                .Select(d => new {
                    Category = d.ArtikullId.HasValue && artikujtCategories.TryGetValue(d.ArtikullId.Value, out var cat) 
                        ? cat 
                        : (!string.IsNullOrEmpty(d.Kategoria) ? d.Kategoria : "Pa kategori"),
                    Revenue = d.VleraMeTvsh ?? 0
                })
                .GroupBy(d => d.Category.Length >= 20 ? d.Category.Substring(0, 20) : d.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Revenue = g.Sum(d => d.Revenue)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(8)
                .ToList();

            // Professional masculine color palette for pie chart
            var professionalColors = new[]
            {
                new SKColor(30, 58, 138),   // Deep Navy Blue
                new SKColor(22, 101, 52),   // Forest Green
                new SKColor(107, 114, 128), // Steel Gray
                new SKColor(55, 65, 81),    // Charcoal
                new SKColor(30, 64, 175),   // Royal Blue
                new SKColor(21, 128, 61),   // Emerald
                new SKColor(75, 85, 99),    // Slate
                new SKColor(17, 24, 39),    // Dark Navy
            };

            if (categoryData.Any())
            {
                CategoryPieChart.Series = categoryData.Select((c, index) => 
                    new PieSeries<double>
                    {
                        Values = new double[] { c.Revenue },
                        Name = c.Category,
                        Fill = new SolidColorPaint(professionalColors[index % professionalColors.Length])
                    }).ToArray();
            }
            else
            {
                // If no category data, show a placeholder
                CategoryPieChart.Series = new ISeries[]
                {
                    new PieSeries<double>
                    {
                        Values = new double[] { 1 },
                        Name = "Nuk ka të dhëna",
                        Fill = new SolidColorPaint(new SKColor(107, 114, 128))
                    }
                };
            }
            
            // Product Performance Grid for SQL Server mode
            // Reuse artikujtCategories and load full articles for purchase price
            var articles = context.Artikujt.ToDictionary(a => a.Id, a => a);
            
            var productPerformance = ditariD
                .Where(d => d.ArtikullId.HasValue && !string.IsNullOrEmpty(d.Artikulli))
                .GroupBy(d => new { d.ArtikullId, d.Artikulli })
                .Select(g =>
                {
                    var quantity = g.Sum(d => d.Sasia ?? 0);
                    var revenue = g.Sum(d => d.VleraMeTvsh ?? 0);
                    
                    // Get purchase price from article if available
                    double purchasePrice = 0;
                    if (g.Key.ArtikullId.HasValue && articles.TryGetValue(g.Key.ArtikullId.Value, out var article))
                    {
                        purchasePrice = article.CFurnizimit ?? 0;
                    }
                    // Alternatively, get from QmimiF column in DitariD
                    if (purchasePrice == 0)
                    {
                        purchasePrice = g.First().QmimiF ?? 0;
                    }
                    
                    var cost = quantity * purchasePrice;
                    var profit = revenue - cost;
                    var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

                    return new ProductPerformance
                    {
                        ProductName = g.Key.Artikulli ?? "Artikull",
                        Quantity = quantity,
                        Revenue = revenue,
                        Cost = cost,
                        Profit = profit,
                        Margin = margin
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .Take(20)
                .ToList();

            ProductPerformanceGrid.ItemsSource = productPerformance;
        }

        private void LoadFinancialAnalysisFromSqlServer(List<Models.BMDData.DitariD> ditariD, List<Models.BMDData.DitariH> ditariH)
        {
            var totalRevenue = ditariD.Sum(d => d.VleraMeTvsh ?? 0);
            var totalCost = ditariH.Sum(h => h.VleraMeTvsh ?? 0);
            var grossProfit = totalRevenue - totalCost;
            var profitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;

            // Use existing UI controls
            NetProfitKPI.Text = $"{grossProfit:N2} €";
            ProfitMarginKPI.Text = $"Marzha: {profitMargin:N1}%";

            // Monthly revenue vs costs comparison
            var monthlyRevenue = ditariD
                .Where(d => d.Data.HasValue)
                .GroupBy(d => new { d.Data!.Value.Year, d.Data.Value.Month })
                .Select(g => new { YearMonth = $"{g.Key.Year}-{g.Key.Month:00}", Revenue = g.Sum(d => d.VleraMeTvsh ?? 0) })
                .OrderBy(x => x.YearMonth)
                .ToList();

            var monthlyCosts = ditariH
                .Where(h => h.Data.HasValue)
                .GroupBy(h => new { h.Data!.Value.Year, h.Data.Value.Month })
                .Select(g => new { YearMonth = $"{g.Key.Year}-{g.Key.Month:00}", Costs = g.Sum(h => h.VleraMeTvsh ?? 0) })
                .OrderBy(x => x.YearMonth)
                .ToList();

            var allMonths = monthlyRevenue.Select(r => r.YearMonth)
                .Union(monthlyCosts.Select(c => c.YearMonth))
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            if (allMonths.Any())
            {
                RevenueVsCostChart.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = allMonths.Select(m => monthlyRevenue.FirstOrDefault(r => r.YearMonth == m)?.Revenue ?? 0).ToArray(),
                        Name = "Të ardhurat",
                        Fill = new SolidColorPaint(new SKColor(34, 197, 94))
                    },
                    new ColumnSeries<double>
                    {
                        Values = allMonths.Select(m => monthlyCosts.FirstOrDefault(c => c.YearMonth == m)?.Costs ?? 0).ToArray(),
                        Name = "Kostot",
                        Fill = new SolidColorPaint(new SKColor(239, 68, 68))
                    }
                };

                RevenueVsCostChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = allMonths.ToArray(),
                        LabelsRotation = 45
                    }
                };
                
                // Profit Trend Chart - calculate daily profit
                var dailyRevenue = ditariD
                    .Where(d => d.Data.HasValue)
                    .GroupBy(d => d.Data!.Value.Date)
                    .Select(g => new { Date = g.Key, Revenue = g.Sum(d => d.VleraMeTvsh ?? 0) })
                    .OrderBy(x => x.Date)
                    .ToList();

                var dailyCosts = ditariH
                    .Where(h => h.Data.HasValue)
                    .GroupBy(h => h.Data!.Value.Date)
                    .Select(g => new { Date = g.Key, Costs = g.Sum(h => h.VleraMeTvsh ?? 0) })
                    .OrderBy(x => x.Date)
                    .ToList();

                var allDates = dailyRevenue.Select(r => r.Date)
                    .Union(dailyCosts.Select(c => c.Date))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                if (allDates.Any())
                {
                    var profitByDay = allDates.Select(date => new
                    {
                        Date = date,
                        Profit = (dailyRevenue.FirstOrDefault(r => r.Date == date)?.Revenue ?? 0) -
                                (dailyCosts.FirstOrDefault(c => c.Date == date)?.Costs ?? 0)
                    }).ToList();

                    ProfitTrendChart.Series = new ISeries[]
                    {
                        new LineSeries<double>
                        {
                            Values = profitByDay.Select(x => x.Profit).ToArray(),
                            Name = "Fitimi (€)",
                            Fill = null,
                            GeometrySize = 8,
                            Stroke = new SolidColorPaint(SKColors.SteelBlue) { StrokeThickness = 3 }
                        }
                    };

                    ProfitTrendChart.XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = profitByDay.Select(x => x.Date.ToString("dd/MM")).ToArray(),
                            LabelsRotation = 45
                        }
                    };
                }
            }
        }

        private void LoadProductAnalysis(List<Models.ReceiptItem> receiptItems)
        {
            // Top 10 Products
            var topProducts = receiptItems
                .Where(ri => ri.Article != null && ri.Article.Name != null)
                .GroupBy(ri => new { ri.ArticleId, ri.Article!.Name })
                .Select(g => new
                {
                    ProductName = g.Key.Name,
                    Quantity = g.Sum(ri => (double)ri.Quantity),
                    Revenue = g.Sum(ri => (double)ri.TotalValue)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            if (topProducts.Any())
            {
                TopProductsChart.Series = new ISeries[]
                {
                    new RowSeries<double>
                    {
                        Values = topProducts.Select(x => x.Revenue).ToArray(),
                        Name = "Të ardhurat (€)",
                        Fill = new SolidColorPaint(SKColors.Orange),
                        DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                        DataLabelsSize = 12,
                        DataLabelsFormatter = point => $"{point.Model:N0}€"
                    }
                };

                TopProductsChart.YAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = topProducts.Select(x =>
                            x.ProductName.Length > 20 ? x.ProductName.Substring(0, 17) + "..." : x.ProductName).ToArray()
                    }
                };
            }

            // Product Performance Grid - Use a proper class instead of anonymous type
            var productPerformance = receiptItems
                .Where(ri => ri.Article != null && ri.Article.Name != null)
                .GroupBy(ri => new { ri.ArticleId, ri.Article!.Name, ri.Article.PurchasePrice })
                .Select(g =>
                {
                    var quantity = g.Sum(ri => (double)ri.Quantity);
                    var revenue = g.Sum(ri => (double)ri.TotalValue);
                    var cost = quantity * (double)g.Key.PurchasePrice;
                    var profit = revenue - cost;
                    var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

                    return new ProductPerformance
                    {
                        ProductName = g.Key.Name,
                        Quantity = quantity,
                        Revenue = revenue,
                        Cost = cost,
                        Profit = profit,
                        Margin = margin
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .Take(20)
                .ToList();

            ProductPerformanceGrid.ItemsSource = productPerformance;
        }

        // Helper class for product performance
        private class ProductPerformance
        {
            public string ProductName { get; set; } = string.Empty;
            public double Quantity { get; set; }
            public double Revenue { get; set; }
            public double Cost { get; set; }
            public double Profit { get; set; }
            public double Margin { get; set; }
        }

        private async System.Threading.Tasks.Task LoadInventoryAnalysisAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var context = new POSDbContext();
                    
                    // SQL Server mode - use Artikujt table
                    var artikujt = context.Artikujt.ToList();

                    // KPIs - use Sasia directly from Artikujt
                    var totalStockValue = artikujt.Sum(a => (double)(a.Sasia ?? 0) * (double)(a.CShitjes ?? 0));
                    var lowStockItems = artikujt.Count(a => 
                    {
                        var qty = a.Sasia ?? 0;
                        var minQty = a.StoguMinimal ?? 0;
                        // Consider low stock if: below explicit minimum, or below 10 if no minimum set
                        return qty >= 0 && (minQty > 0 ? qty <= minQty : qty < 10);
                    });
                    var outOfStockItems = artikujt.Count(a => (a.Sasia ?? 0) <= 0);

                    Dispatcher.Invoke(() =>
                    {
                        TotalStockValueKPI.Text = $"{totalStockValue:N2} €";
                        LowStockItemsKPI.Text = $"{lowStockItems} artikuj";
                        SlowMovingItemsKPI.Text = $"{outOfStockItems} artikuj";
                    });

                    // Stock Value by Category
                    var stockByCategory = artikujt
                        .Where(a => (a.Sasia ?? 0) > 0)
                        .GroupBy(a => a.Kategoria ?? "Pa kategori")
                        .Select(g => new
                        {
                            Category = g.Key,
                            Value = g.Sum(a => (double)(a.Sasia ?? 0) * (double)(a.CShitjes ?? 0))
                            })
                            .OrderByDescending(x => x.Value)
                            .Take(10)
                            .ToList();

                        if (stockByCategory.Any())
                        {
                            Dispatcher.Invoke(() =>
                            {
                                StockValueChart.Series = new ISeries[]
                                {
                                    new ColumnSeries<double>
                                    {
                                        Values = stockByCategory.Select(x => x.Value).ToArray(),
                                        Name = "Vlera (€)",
                                        Fill = new SolidColorPaint(SKColors.Teal),
                                        DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                                        DataLabelsSize = 11,
                                        DataLabelsFormatter = point => $"{point.Model:N0}€"
                                    }
                                };

                                StockValueChart.XAxes = new Axis[]
                                {
                                    new Axis
                                    {
                                        Labels = stockByCategory.Select(x => 
                                            x.Category != null && x.Category.Length > 15 ? x.Category.Substring(0, 12) + "..." : x.Category ?? "").ToArray(),
                                        LabelsRotation = 45
                                    }
                                };
                            });
                        }

                        // Low Stock Grid - use named class so WPF binding works via reflection
                        List<LowStockItemVm> lowStockArticles;
                        try
                        {
                            var invStocks = context.InventoryStocks
                                .Where(s => s.CurrentQuantity <= s.MinimumQuantity)
                                .OrderBy(s => s.CurrentQuantity)
                                .Take(50)
                                .ToList();

                            if (invStocks.Any())
                            {
                                lowStockArticles = invStocks.Select(s => new LowStockItemVm
                                {
                                    Barcode = "-",
                                    Name = s.ArticleName,
                                    StockQuantity = s.CurrentQuantity,
                                    Category = $"Min: {s.MinimumQuantity:N0} {s.Unit}"
                                }).ToList();
                            }
                            else
                            {
                                // Show articles with stock below minimum, or below 20 if no minimum set
                                lowStockArticles = artikujt
                                    .Where(a =>
                                    {
                                        var qty = a.Sasia ?? 0;
                                        var minQty = a.StoguMinimal ?? 0;
                                        return minQty > 0 ? qty <= minQty : qty < 20;
                                    })
                                    .OrderBy(a => a.Sasia)
                                    .Take(50)
                                    .Select(a => new LowStockItemVm
                                    {
                                        Barcode = a.Barkodi ?? "-",
                                        Name = a.Emertimi ?? "Pa emër",
                                        StockQuantity = (decimal)(a.Sasia ?? 0),
                                        Category = a.Kategoria ?? "Pa kategori"
                                    })
                                    .ToList();

                                // If none found, show the 30 articles with lowest stock regardless of threshold
                                if (!lowStockArticles.Any())
                                {
                                    lowStockArticles = artikujt
                                        .OrderBy(a => a.Sasia)
                                        .Take(30)
                                        .Select(a => new LowStockItemVm
                                        {
                                            Barcode = a.Barkodi ?? "-",
                                            Name = a.Emertimi ?? "Pa emër",
                                            StockQuantity = (decimal)(a.Sasia ?? 0),
                                            Category = a.Kategoria ?? "Pa kategori"
                                        })
                                        .ToList();
                                }
                            }
                        }
                        catch
                        {
                            // On any error, show lowest-stock articles
                            lowStockArticles = artikujt
                                .OrderBy(a => a.Sasia)
                                .Take(30)
                                .Select(a => new LowStockItemVm
                                {
                                    Barcode = a.Barkodi ?? "-",
                                    Name = a.Emertimi ?? "Pa emër",
                                    StockQuantity = (decimal)(a.Sasia ?? 0),
                                    Category = a.Kategoria ?? "Pa kategori"
                                })
                                .ToList();
                        }

                        // Fallback: try Articles table if Artikujt was empty
                        if (!lowStockArticles.Any())
                        {
                            try
                            {
                                var articles = context.Articles
                                    .OrderBy(a => a.StockQuantity)
                                    .Take(30)
                                    .ToList();
                                lowStockArticles = articles.Select(a => new LowStockItemVm
                                {
                                    Barcode = a.Barcode ?? "-",
                                    Name = a.Name,
                                    StockQuantity = a.StockQuantity,
                                    Category = a.Category ?? "Pa kategori"
                                }).ToList();
                            }
                            catch { /* Articles table unavailable */ }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            LowStockGrid.ItemsSource = lowStockArticles;

                            // Populate the LowStockBarChart with top 10 articles by sales volume this month
                            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                            var monthEnd = monthStart.AddMonths(1);
                            try
                            {
                                var topSales = context.DitariD
                                    .Where(d => d.Data.HasValue && d.Data.Value >= monthStart && d.Data.Value < monthEnd
                                             && !string.IsNullOrEmpty(d.Emertimi))
                                    .GroupBy(d => d.Emertimi)
                                    .Select(g => new { Name = g.Key!, Qty = g.Sum(x => x.Sasia ?? 0) })
                                    .OrderByDescending(x => x.Qty)
                                    .Take(10)
                                    .ToList();

                                if (topSales.Any())
                                {
                                    LowStockBarChart.Series = new ISeries[]
                                    {
                                        new ColumnSeries<double>
                                        {
                                            Values = topSales.Select(x => x.Qty).ToArray(),
                                            Name = "Sasia e shitur (muaji)",
                                            Fill = new SolidColorPaint(new SKColor(16, 185, 129)),
                                            DataLabelsPaint = new SolidColorPaint(SKColors.DarkSlateGray),
                                            DataLabelsSize = 10,
                                            DataLabelsFormatter = p => $"{p.Model:N0}"
                                        }
                                    };
                                    LowStockBarChart.XAxes = new Axis[]
                                    {
                                        new Axis
                                        {
                                            Labels = topSales.Select(x =>
                                                x.Name.Length > 16 ? x.Name.Substring(0, 13) + "..." : x.Name).ToArray(),
                                            LabelsRotation = 45,
                                            TextSize = 11
                                        }
                                    };
                                }
                                else if (lowStockArticles.Any())
                                {
                                    var top10 = lowStockArticles.Take(10).ToList();
                                    LowStockBarChart.Series = new ISeries[]
                                    {
                                        new ColumnSeries<double>
                                        {
                                            Values = top10.Select(x => (double)x.StockQuantity).ToArray(),
                                            Name = "Sasia në stok",
                                            Fill = new SolidColorPaint(new SKColor(217, 119, 6)),
                                            DataLabelsSize = 10
                                        }
                                    };
                                    LowStockBarChart.XAxes = new Axis[]
                                    {
                                        new Axis
                                        {
                                            Labels = top10.Select(x =>
                                                x.Name.Length > 16 ? x.Name.Substring(0, 13) + "..." : x.Name).ToArray(),
                                            LabelsRotation = 45,
                                            TextSize = 11
                                        }
                                    };
                                }
                            }
                            catch
                            {
                                // DitariD unavailable, fallback to low-stock chart
                            }
                        });
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të analizës së stoqit: {ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadFinancialAnalysis(List<Models.Receipt> receipts, List<Models.Purchase> purchases)
        {
            // Calculate profit (simplified - actual profit calculation would need cost basis)
            var totalRevenue = receipts.Sum(r => (double)r.TotalAmount);
            var totalCost = purchases.Sum(p => (double)p.TotalAmount);
            var netProfit = totalRevenue - totalCost;
            var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;

            NetProfitKPI.Text = $"{netProfit:N2} €";
            ProfitMarginKPI.Text = $"Marzha: {profitMargin:N1}%";

            // Revenue vs Cost by Day
            var revenueByDay = receipts
                .GroupBy(r => r.Date.Date)
                .Select(g => new { Date = g.Key, Amount = g.Sum(r => (double)r.TotalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            var costByDay = purchases
                .GroupBy(p => p.Date.Date)
                .Select(g => new { Date = g.Key, Amount = g.Sum(p => (double)p.TotalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            var allDates = revenueByDay.Select(x => x.Date)
                .Union(costByDay.Select(x => x.Date))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (allDates.Any())
            {
                RevenueVsCostChart.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = allDates.Select(date =>
                            revenueByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0).ToArray(),
                        Name = "Të ardhurat",
                        Fill = new SolidColorPaint(SKColors.Green)
                    },
                    new ColumnSeries<double>
                    {
                        Values = allDates.Select(date =>
                            costByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0).ToArray(),
                        Name = "Kostot",
                        Fill = new SolidColorPaint(SKColors.Red)
                    }
                };

                RevenueVsCostChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = allDates.Select(d => d.ToString("dd/MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };

                // Profit Trend
                var profitByDay = allDates.Select(date => new
                {
                    Date = date,
                    Profit = (revenueByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0) -
                            (costByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0)
                }).ToList();

                ProfitTrendChart.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = profitByDay.Select(x => x.Profit).ToArray(),
                        Name = "Fitimi (€)",
                        Fill = null,
                        GeometrySize = 8,
                        Stroke = new SolidColorPaint(SKColors.SteelBlue) { StrokeThickness = 3 }
                    }
                };

                ProfitTrendChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = profitByDay.Select(x => x.Date.ToString("dd/MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };
            }
        }

        private void PeriodComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PeriodComboBox.SelectedIndex >= 0)
            {
                SetPeriod(PeriodComboBox.SelectedIndex);
                LoadAllData();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAllData();
            MessageBox.Show("Të dhënat u rifreskuan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Eksporto Analytics në Excel",
                    FileName = $"Analytics_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using var workbook = new XLWorkbook();
                    
                    // Create sheets for different analyses
                    var summarySheet = workbook.Worksheets.Add("Përmbledhje");
                    summarySheet.Cell(1, 1).Value = "Raporti analitik";
                    summarySheet.Cell(2, 1).Value = $"Periudha: {_startDate:dd/MM/yyyy} - {_endDate:dd/MM/yyyy}";
                    summarySheet.Cell(4, 1).Value = "Shitjet totale:";
                    summarySheet.Cell(4, 2).Value = TotalSalesKPI.Text;
                    summarySheet.Cell(5, 1).Value = "Transaksionet:";
                    summarySheet.Cell(5, 2).Value = TotalTransactionsKPI.Text;
                    summarySheet.Cell(6, 1).Value = "Fitimi neto:";
                    summarySheet.Cell(6, 2).Value = NetProfitKPI.Text;

                    // Add product performance data
                    if (ProductPerformanceGrid.ItemsSource != null)
                    {
                        var perfSheet = workbook.Worksheets.Add("Performanca e produkteve");
                        perfSheet.Cell(1, 1).Value = "Produkti";
                        perfSheet.Cell(1, 2).Value = "Sasia";
                        perfSheet.Cell(1, 3).Value = "Vlera";
                        perfSheet.Cell(1, 4).Value = "Fitimi";
                        perfSheet.Cell(1, 5).Value = "Marzha %";

                        int row = 2;
                        foreach (dynamic item in ProductPerformanceGrid.ItemsSource)
                        {
                            perfSheet.Cell(row, 1).Value = item.ProductName;
                            perfSheet.Cell(row, 2).Value = item.Quantity;
                            perfSheet.Cell(row, 3).Value = item.Revenue;
                            perfSheet.Cell(row, 4).Value = item.Profit;
                            perfSheet.Cell(row, 5).Value = item.Margin;
                            row++;
                        }
                    }

                    workbook.SaveAs(saveFileDialog.FileName);
                    MessageBox.Show("Raporti u eksportua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë eksportimit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                
                if (printDialog.ShowDialog() == true)
                {
                    // Create a FlowDocument for printing
                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PagePadding = new Thickness(50);
                    doc.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

                    // Title
                    var titlePara = new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("📊 RAPORTI ANALITIK")
                        {
                            FontSize = 24,
                            FontWeight = FontWeights.Bold
                        });
                    titlePara.TextAlignment = TextAlignment.Center;
                    doc.Blocks.Add(titlePara);

                    // Period
                    var periodPara = new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"Periudha: {_startDate:dd/MM/yyyy} - {_endDate:dd/MM/yyyy}")
                        {
                            FontSize = 14
                        });
                    periodPara.TextAlignment = TextAlignment.Center;
                    periodPara.Margin = new Thickness(0, 0, 0, 20);
                    doc.Blocks.Add(periodPara);

                    // Sales Summary
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("PËRMBLEDHJE E SHITJEVE")
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Shitjet totale: {TotalSalesKPI.Text}")
                        {
                            FontSize = 14
                        }));
                    
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Transaksionet: {TotalTransactionsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• {AvgTransactionKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Artikujt e shitur: {TotalItemsSoldKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• {UniqueProductsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    // Financial Summary
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("\nPËRMBLEDHJE FINANCIARE")
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Fitimi neto: {NetProfitKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• {ProfitMarginKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    // Inventory Summary
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("\nPËRMBLEDHJE E STOKUT")
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Vlera totale: {TotalStockValueKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Stok i ulët: {LowStockItemsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Pa lëvizje: {SlowMovingItemsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    // Footer
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"\n\nRaport i gjeneruar më: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        {
                            FontSize = 10,
                            FontStyle = FontStyles.Italic
                        })
                    {
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    });

                    // Print the document
                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Raporti analitik");

                    MessageBox.Show("Raporti u printua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit të raportit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool _inventoryLoaded = false;

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TabControl tc) return;
            // Index 2 = Inventory tab
            if (tc.SelectedIndex == 2 && !_inventoryLoaded)
            {
                _inventoryLoaded = true;
                _ = LoadInventoryAnalysisAsync();
            }
        }
    }
}
