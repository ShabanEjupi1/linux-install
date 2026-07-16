using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KosovaPOS.Database;
using KosovaPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class AdvancedReportingWindow : Window
    {
        private DataGrid _salesByCategoryGrid = new DataGrid();
        private DataGrid _hourlyGrid = new DataGrid();
        private DataGrid _driverGrid = new DataGrid();
        private DataGrid _topCustomersGrid = new DataGrid();
        private DataGrid _tierGrid = new DataGrid();
        private DataGrid _campaignGrid = new DataGrid();
        private DataGrid _inventoryGrid = new DataGrid();
        private DataGrid _staffGrid = new DataGrid();
        private readonly TextBlock _lblTotalRevenue = new TextBlock();
        private readonly TextBlock _lblTotalOrders = new TextBlock();
        private readonly TextBlock _lblAvgOrder = new TextBlock();
        private readonly TextBlock _lblNetProfit = new TextBlock();
        private readonly TextBlock _lblTopCategory = new TextBlock();
        private readonly TextBlock _lblTopProduct = new TextBlock();
        private readonly TextBlock _lblActiveCustomers = new TextBlock();
        private readonly TextBlock _lblTotalDrivers = new TextBlock();
        private ComboBox _periodCombo = new ComboBox();

        public AdvancedReportingWindow()
        {
            Title = "\U0001F4CA Raporte te Avancuara";
            Width = 1400; Height = 900;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowState = WindowState.Maximized;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol");
            BuildLayout();
            Loaded += (s, e) => LoadAllData();
        }

        private void BuildLayout()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var hdr = new Border { Padding = new Thickness(28, 18, 28, 18) };
            hdr.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(3, 105, 161), 0);
            var hdrGrid = new Grid();
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var hdrText = new StackPanel();
            hdrText.Children.Add(new TextBlock { Text = "\U0001F4CA Raporte te Avancuara", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            hdrText.Children.Add(new TextBlock { Text = "Analiza e detajuar e shitjeve, shofereve, klienteve, inventarit dhe stafit", FontSize = 14, Foreground = Brushes.White, Opacity = 0.9, Margin = new Thickness(0, 4, 0, 0) });
            Grid.SetColumn(hdrText, 0);
            hdrGrid.Children.Add(hdrText);
            var acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            acts.Children.Add(new TextBlock { Text = "Periudha: ", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), FontSize = 14 });
            _periodCombo = new ComboBox { Width = 140, Height = 34, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center };
            foreach (var p in new[] { "Sot", "Kjo jave", "Ky muaj", "3 muaj", "6 muaj", "Ky vit", "Te gjitha" })
                _periodCombo.Items.Add(p);
            _periodCombo.SelectedIndex = 2;
            _periodCombo.SelectionChanged += (s, e) => LoadAllData();
            acts.Children.Add(_periodCombo);
            var btnR = Btn("\U0001F504 Rifresko", "#0891B2", 120, 10); btnR.Click += (s, e) => LoadAllData(); acts.Children.Add(btnR);
            var btnC = Btn("\u2716 Mbyll", "#475569", 100, 10); btnC.Click += (s, e) => Close(); acts.Children.Add(btnC);
            Grid.SetColumn(acts, 1); hdrGrid.Children.Add(acts);
            hdr.Child = hdrGrid; Grid.SetRow(hdr, 0); mainGrid.Children.Add(hdr);

            // Tab control
            var tabs = new TabControl { Margin = new Thickness(16, 12, 16, 8), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            tabs.Items.Add(BuildOverviewTab());
            tabs.Items.Add(BuildSalesTab());
            tabs.Items.Add(BuildDeliveryTab());
            tabs.Items.Add(BuildCustomerTab());
            tabs.Items.Add(BuildInventoryTab());
            tabs.Items.Add(BuildStaffTab());
            Grid.SetRow(tabs, 1); mainGrid.Children.Add(tabs);

            // Footer
            var ftr = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 10, 16, 10) };
            ftr.Child = new TextBlock { Text = "KosovaPOS \u00b7 Raporte te Avancuara \u00b7 Te dhenat nga baza e te dhenave", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetRow(ftr, 2); mainGrid.Children.Add(ftr);
            Content = mainGrid;
        }

        private TabItem BuildOverviewTab()
        {
            var tab = new TabItem { Header = "\U0001F4CA Pasqyra", Padding = new Thickness(14, 8, 14, 8) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            var row1 = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
            row1.Children.Add(KpiCard("\U0001F4B0 Te ardhura totale", _lblTotalRevenue, "#0284C7"));
            row1.Children.Add(KpiCard("\U0001F6D2 Numri i porosive", _lblTotalOrders, "#059669"));
            row1.Children.Add(KpiCard("\U0001F4CA Mesatare / porosi", _lblAvgOrder, "#D97706"));
            row1.Children.Add(KpiCard("\U0001F48E Fitimi neto (est.)", _lblNetProfit, "#1E3A5F"));
            panel.Children.Add(row1);
            var row2 = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
            row2.Children.Add(KpiCard("\U0001F3C6 Kategoria kryesore", _lblTopCategory, "#0369A1"));
            row2.Children.Add(KpiCard("\u2B50 Produkti kryesor", _lblTopProduct, "#065F46"));
            row2.Children.Add(KpiCard("\U0001F465 Kliente aktive", _lblActiveCustomers, "#374151"));
            row2.Children.Add(KpiCard("\U0001F697 Shofera aktive", _lblTotalDrivers, "#0891B2"));
            panel.Children.Add(row2);
            scroll.Content = panel; tab.Content = scroll;
            return tab;
        }

        private TabItem BuildSalesTab()
        {
            var tab = new TabItem { Header = "\U0001F4C8 Shitjet", Padding = new Thickness(14, 8, 14, 8) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lc = Card("\U0001F3AF TOP Artikujt sipas shitjeve");
            _salesByCategoryGrid = Dg();
            _salesByCategoryGrid.Columns.Add(new DataGridTextColumn { Header = "Artikulli", Binding = Bind("Category"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _salesByCategoryGrid.Columns.Add(new DataGridTextColumn { Header = "Porosi", Binding = Bind("OrderCount"), Width = 80 });
            _salesByCategoryGrid.Columns.Add(new DataGridTextColumn { Header = "Artikuj", Binding = Bind("ItemCount"), Width = 80 });
            _salesByCategoryGrid.Columns.Add(new DataGridTextColumn { Header = "EUR", Binding = Bind("Revenue"), Width = 130 });
            lc.Children.Add(_salesByCategoryGrid);
            var lbdr = lc.Parent as Border ?? WrapInBorder(lc); Grid.SetColumn(lbdr, 0); g.Children.Add(lbdr);
            var rc = Card("\U0001F550 Shitjet sipas ores");
            _hourlyGrid = Dg();
            _hourlyGrid.Columns.Add(new DataGridTextColumn { Header = "Ora", Binding = Bind("Hour"), Width = 80 });
            _hourlyGrid.Columns.Add(new DataGridTextColumn { Header = "Porosi", Binding = Bind("OrderCount"), Width = 80 });
            _hourlyGrid.Columns.Add(new DataGridTextColumn { Header = "EUR", Binding = Bind("Revenue"), Width = 130 });
            _hourlyGrid.Columns.Add(new DataGridTextColumn { Header = "Aktiviteti", Binding = Bind("Bar"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            rc.Children.Add(_hourlyGrid);
            var rbdr = rc.Parent as Border ?? WrapInBorder(rc); Grid.SetColumn(rbdr, 1); g.Children.Add(rbdr);
            tab.Content = g; return tab;
        }

        private TabItem BuildDeliveryTab()
        {
            var tab = new TabItem { Header = "\U0001F697 Shoferet", Padding = new Thickness(14, 8, 14, 8) };
            var c = Card("\U0001F697 Statistikat e shofereve");
            _driverGrid = Dg();
            _driverGrid.Columns.Add(new DataGridTextColumn { Header = "Shoferi", Binding = Bind("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _driverGrid.Columns.Add(new DataGridTextColumn { Header = "Dergesa totale", Binding = Bind("TotalDeliveries"), Width = 130 });
            _driverGrid.Columns.Add(new DataGridTextColumn { Header = "Vleresimi", Binding = Bind("Rating"), Width = 110 });
            _driverGrid.Columns.Add(new DataGridTextColumn { Header = "Statusi", Binding = Bind("Status"), Width = 110 });
            _driverGrid.Columns.Add(new DataGridTextColumn { Header = "Telefoni", Binding = Bind("Phone"), Width = 130 });
            _driverGrid.Columns.Add(new DataGridTextColumn { Header = "Automjeti", Binding = Bind("VehicleType"), Width = 120 });
            c.Children.Add(_driverGrid);
            tab.Content = c.Parent as Border ?? (UIElement)WrapInBorder(c); return tab;
        }

        private TabItem BuildCustomerTab()
        {
            var tab = new TabItem { Header = "\U0001F465 Klientet", Padding = new Thickness(14, 8, 14, 8) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            var lc = Card("\U0001F3C6 TOP 20 Klientet");
            _topCustomersGrid = Dg();
            _topCustomersGrid.Columns.Add(new DataGridTextColumn { Header = "#", Binding = Bind("Rank"), Width = 40 });
            _topCustomersGrid.Columns.Add(new DataGridTextColumn { Header = "Emri", Binding = Bind("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _topCustomersGrid.Columns.Add(new DataGridTextColumn { Header = "Porosi", Binding = Bind("OrderCount"), Width = 80 });
            _topCustomersGrid.Columns.Add(new DataGridTextColumn { Header = "Shpenzuar (EUR)", Binding = Bind("TotalSpent"), Width = 140 });
            _topCustomersGrid.Columns.Add(new DataGridTextColumn { Header = "Niveli", Binding = Bind("Tier"), Width = 90 });
            _topCustomersGrid.Columns.Add(new DataGridTextColumn { Header = "Pika", Binding = Bind("Points"), Width = 70 });
            lc.Children.Add(_topCustomersGrid);
            var lb = lc.Parent as Border ?? WrapInBorder(lc); Grid.SetColumn(lb, 0); g.Children.Add(lb);
            var rc = Card("\U0001F3C5 Shperndarje sipas nivelit");
            _tierGrid = Dg();
            _tierGrid.Columns.Add(new DataGridTextColumn { Header = "Niveli", Binding = Bind("Tier"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _tierGrid.Columns.Add(new DataGridTextColumn { Header = "Kliente", Binding = Bind("Count"), Width = 90 });
            _tierGrid.Columns.Add(new DataGridTextColumn { Header = "Pika mesatare", Binding = Bind("AvgPoints"), Width = 120 });
            _tierGrid.Columns.Add(new DataGridTextColumn { Header = "Shpenz. mesatare", Binding = Bind("AvgSpent"), Width = 150 });
            rc.Children.Add(_tierGrid);
            var rb = rc.Parent as Border ?? WrapInBorder(rc); Grid.SetColumn(rb, 1); g.Children.Add(rb);
            tab.Content = g; return tab;
        }

        private TabItem BuildInventoryTab()
        {
            var tab = new TabItem { Header = "\U0001F4E6 Inventari & Fushata", Padding = new Thickness(14, 8, 14, 8) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            var lc = Card("\u26A0 Produkte me stok te ulet");
            _inventoryGrid = Dg();
            _inventoryGrid.Columns.Add(new DataGridTextColumn { Header = "Produkti", Binding = Bind("ArticleName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _inventoryGrid.Columns.Add(new DataGridTextColumn { Header = "Stoku aktual", Binding = Bind("CurrentQuantity"), Width = 120 });
            _inventoryGrid.Columns.Add(new DataGridTextColumn { Header = "Minimum", Binding = Bind("MinimumQuantity"), Width = 110 });
            _inventoryGrid.Columns.Add(new DataGridTextColumn { Header = "Njesija", Binding = Bind("Unit"), Width = 80 });
            _inventoryGrid.Columns.Add(new DataGridTextColumn { Header = "Statusi", Binding = Bind("StockStatus"), Width = 100 });
            lc.Children.Add(_inventoryGrid);
            var lb = lc.Parent as Border ?? WrapInBorder(lc); Grid.SetColumn(lb, 0); g.Children.Add(lb);
            var rc = Card("\U0001F381 Performanca e fushatave");
            _campaignGrid = Dg();
            _campaignGrid.Columns.Add(new DataGridTextColumn { Header = "Fushata", Binding = Bind("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _campaignGrid.Columns.Add(new DataGridTextColumn { Header = "Te ardhura", Binding = Bind("TotalRevenue"), Width = 120 });
            _campaignGrid.Columns.Add(new DataGridTextColumn { Header = "Perdorur", Binding = Bind("UsageCount"), Width = 90 });
            _campaignGrid.Columns.Add(new DataGridTextColumn { Header = "Statusi", Binding = Bind("Status"), Width = 90 });
            rc.Children.Add(_campaignGrid);
            var rb = rc.Parent as Border ?? WrapInBorder(rc); Grid.SetColumn(rb, 1); g.Children.Add(rb);
            tab.Content = g; return tab;
        }

        private TabItem BuildStaffTab()
        {
            var tab = new TabItem { Header = "\U0001F465 Stafi", Padding = new Thickness(14, 8, 14, 8) };
            var c = Card("\U0001F465 Punonjesit");
            _staffGrid = Dg();
            _staffGrid.Columns.Add(new DataGridTextColumn { Header = "Punonjesi", Binding = Bind("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _staffGrid.Columns.Add(new DataGridTextColumn { Header = "Pozita", Binding = Bind("Position"), Width = 130 });
            _staffGrid.Columns.Add(new DataGridTextColumn { Header = "Tarifa/ore (EUR)", Binding = Bind("HourlyRate"), Width = 140 });
            _staffGrid.Columns.Add(new DataGridTextColumn { Header = "Aktiv", Binding = Bind("IsActive"), Width = 80 });
            _staffGrid.Columns.Add(new DataGridTextColumn { Header = "Punesuar", Binding = Bind("HireDate"), Width = 110 });
            _staffGrid.Columns.Add(new DataGridTextColumn { Header = "Ore sot", Binding = Bind("HoursToday"), Width = 90 });
            c.Children.Add(_staffGrid);
            tab.Content = c.Parent as Border ?? (UIElement)WrapInBorder(c); return tab;
        }

        // ── Data ──────────────────────────────────────────────────────────────────
        private void LoadAllData()
        {
            var (from, to) = GetDateRange();
            try { LoadOverview(from, to); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadOverview error: {ex}"); }
            try { LoadSalesData(from, to); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadSalesData error: {ex}"); }
            try { LoadDriverData(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadDriverData error: {ex}"); }
            try { LoadCustomerData(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadCustomerData error: {ex}"); }
            try { LoadInventoryData(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadInventoryData error: {ex}"); }
            try { LoadStaffData(from, to); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadStaffData error: {ex}"); }
        }

        private (DateTime from, DateTime to) GetDateRange()
        {
            var idx = _periodCombo.SelectedIndex;
            var now = DateTime.Now;
            return idx switch
            {
                0 => (now.Date, now.Date.AddDays(1)),
                1 => (now.Date.AddDays(-(int)now.DayOfWeek + 1), now.Date.AddDays(-(int)now.DayOfWeek + 8)),
                2 => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
                3 => (now.AddMonths(-3), now),
                4 => (now.AddMonths(-6), now),
                5 => (new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1)),
                _ => (DateTime.MinValue, now.AddDays(1))
            };
        }

        private string _lastDataError = string.Empty;

        private void LoadOverview(DateTime from, DateTime to)
        {
            try
            {
                using var ctx = new POSDbContext();
                bool loaded = false;

                // Primary: DitariH (SQL Server BMDData)
                try
                {
                    var ditariH = ctx.DitariH
                        .Where(h => h.Data.HasValue && h.Data.Value >= from && h.Data.Value < to)
                        .ToList();
                    if (ditariH.Any())
                    {
                        var totalRev = (decimal)ditariH.Sum(h => h.Totali ?? 0);
                        var totalOrders = ditariH.Count;
                        var avgOrder = totalOrders > 0 ? totalRev / totalOrders : 0m;
                        _lblTotalRevenue.Text = $"EUR {totalRev:N2}";
                        _lblTotalOrders.Text = totalOrders.ToString("N0");
                        _lblAvgOrder.Text = $"EUR {avgOrder:N2}";
                        _lblNetProfit.Text = $"EUR {totalRev * 0.35m:N2}";

                        var ditariD = ctx.DitariD
                            .Include(d => d.DitariH)
                            .AsEnumerable()
                            .Where(d => d.DitariH != null && d.DitariH.Data.HasValue &&
                                        d.DitariH.Data.Value >= from && d.DitariH.Data.Value < to)
                            .ToList();

                        var topProd = ditariD
                            .Where(d => !string.IsNullOrEmpty(d.Emertimi))
                            .GroupBy(d => d.Emertimi)
                            .Select(g => new { Name = g.Key, Rev = g.Sum(x => x.Totali ?? 0) })
                            .OrderByDescending(x => x.Rev)
                            .FirstOrDefault();

                        _lblTopCategory.Text = topProd?.Name ?? "--";
                        _lblTopProduct.Text = topProd?.Name ?? "--";
                        loaded = true;
                    }
                }
                catch { /* fall through to Receipts */ }

                // Fallback: Receipts table (KosovaPOS)
                if (!loaded)
                {
                    try
                    {
                        var receipts = ctx.Receipts
                            .Where(r => r.Date >= from && r.Date < to)
                            .ToList();
                        var totalRev = receipts.Sum(r => r.TotalAmount);
                        var totalOrders = receipts.Count;
                        var avgOrder = totalOrders > 0 ? totalRev / totalOrders : 0m;
                        _lblTotalRevenue.Text = $"EUR {totalRev:N2}";
                        _lblTotalOrders.Text = totalOrders.ToString("N0");
                        _lblAvgOrder.Text = $"EUR {avgOrder:N2}";
                        _lblNetProfit.Text = $"EUR {totalRev * 0.35m:N2}";

                        var topItemGroup = ctx.ReceiptItems
                            .Include(ri => ri.Receipt)
                            .AsEnumerable()
                            .Where(ri => ri.Receipt != null && ri.Receipt.Date >= from && ri.Receipt.Date < to)
                            .GroupBy(ri => ri.ArticleName)
                            .Select(g => new { Name = g.Key, Rev = g.Sum(x => x.TotalValue) })
                            .OrderByDescending(x => x.Rev)
                            .FirstOrDefault();
                        _lblTopCategory.Text = topItemGroup?.Name ?? "--";
                        _lblTopProduct.Text = topItemGroup?.Name ?? "--";
                    }
                    catch
                    {
                        _lblTotalRevenue.Text = "0.00";
                        _lblTotalOrders.Text = "0";
                        _lblAvgOrder.Text = "0.00";
                        _lblNetProfit.Text = "0.00";
                        _lblTopCategory.Text = "--";
                        _lblTopProduct.Text = "--";
                    }
                }

                // Always try to get counts for customers and drivers
                try { _lblActiveCustomers.Text = ctx.Customers.Count(c => c.IsActive).ToString(); }
                catch { _lblActiveCustomers.Text = "0"; }
                try { _lblTotalDrivers.Text = ctx.DeliveryDrivers.Count(d => d.IsActive).ToString(); }
                catch { _lblTotalDrivers.Text = "0"; }
            }
            catch (Exception ex)
            {
                _lblTotalRevenue.Text = "0.00";
                _lblTotalOrders.Text = "--";
                _lastDataError = $"Pasqyra: {ex.Message}";
            }
        }

        private void LoadSalesData(DateTime from, DateTime to)
        {
            try
            {
                using var ctx = new POSDbContext();
                bool loaded = false;

                // Primary: DitariD (SQL Server)
                try
                {
                    var ditariD = ctx.DitariD
                        .Include(d => d.DitariH)
                        .AsEnumerable()
                        .Where(d => d.DitariH != null && d.DitariH.Data.HasValue &&
                                   d.DitariH.Data.Value >= from && d.DitariH.Data.Value < to)
                        .ToList();

                    if (ditariD.Any())
                    {
                        var byArticle = ditariD
                            .Where(d => !string.IsNullOrEmpty(d.Emertimi))
                            .GroupBy(d => d.Emertimi)
                            .Select(g => new
                            {
                                Category = g.Key,
                                OrderCount = g.Select(x => x.DitariHID).Distinct().Count(),
                                ItemCount = (int)g.Sum(x => x.Sasia ?? 0),
                                Revenue = ((decimal)g.Sum(x => x.Totali ?? 0)).ToString("N2")
                            })
                            .OrderByDescending(x => x.OrderCount)
                            .Take(50)
                            .ToList();
                        _salesByCategoryGrid.ItemsSource = byArticle;

                        var hourly = ditariD
                            .Where(d => d.Data.HasValue)
                            .GroupBy(d => d.Data!.Value.Hour)
                            .Select(g => new
                            {
                                Hour = $"{g.Key:D2}:00",
                                OrderCount = g.Select(x => x.DitariHID).Distinct().Count(),
                                Revenue = ((decimal)g.Sum(x => x.Totali ?? 0)).ToString("N2"),
                                Bar = new string('█', Math.Min(20, Math.Max(1, g.Select(x => x.DitariHID).Distinct().Count())))
                            })
                            .OrderBy(x => x.Hour)
                            .ToList();
                        _hourlyGrid.ItemsSource = hourly;
                        loaded = true;
                    }
                }
                catch { /* fall through */ }

                // Fallback: ReceiptItems table
                if (!loaded)
                {
                    try
                    {
                        var items = ctx.ReceiptItems
                            .Include(ri => ri.Receipt)
                            .Where(ri => ri.Receipt!.Date >= from && ri.Receipt.Date < to)
                            .ToList();

                        var byArticle = items
                            .GroupBy(ri => ri.ArticleName)
                            .Select(g => new
                            {
                                Category = g.Key,
                                OrderCount = g.Select(x => x.ReceiptId).Distinct().Count(),
                                ItemCount = (int)g.Sum(x => x.Quantity),
                                Revenue = g.Sum(x => x.TotalValue).ToString("N2")
                            })
                            .OrderByDescending(x => x.OrderCount)
                            .Take(50)
                            .ToList();
                        _salesByCategoryGrid.ItemsSource = byArticle;

                        var hourly = items
                            .Where(ri => ri.Receipt != null)
                            .GroupBy(ri => ri.Receipt!.Date.Hour)
                            .Select(g => new
                            {
                                Hour = $"{g.Key:D2}:00",
                                OrderCount = g.Select(x => x.ReceiptId).Distinct().Count(),
                                Revenue = g.Sum(x => x.TotalValue).ToString("N2"),
                                Bar = new string('█', Math.Min(20, Math.Max(1, g.Select(x => x.ReceiptId).Distinct().Count())))
                            })
                            .OrderBy(x => x.Hour)
                            .ToList();
                        _hourlyGrid.ItemsSource = hourly;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"LoadSalesData fallback error: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                _lastDataError = $"Shitjet: {ex.Message}\n{ex.InnerException?.Message}";
                System.Diagnostics.Debug.WriteLine($"LoadSalesData error: {ex}");
            }
        }

        private void LoadDriverData()
        {
            try
            {
                using var ctx = new POSDbContext();
                var drivers = ctx.DeliveryDrivers.OrderByDescending(d => d.TotalDeliveries).ToList()
                    .Select(d => new { d.Name, d.TotalDeliveries, Rating = string.Format("\u2605 {0:N1}", d.AverageRating), d.Status, d.Phone, VehicleType = d.VehicleType ?? "--" }).ToList();
                _driverGrid.ItemsSource = drivers;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadDriverData error: {ex}");
            }
        }

        private void LoadCustomerData()
        {
            try
            {
                using var ctx = new POSDbContext();
                var top20 = ctx.Customers.OrderByDescending(c => c.TotalSpent).Take(20).ToList()
                    .Select((c, i) => new { Rank = i + 1, c.Name, Phone = c.Phone ?? "--", c.OrderCount, TotalSpent = c.TotalSpent.ToString("N2"), c.Tier, Points = c.LoyaltyPoints }).ToList();
                _topCustomersGrid.ItemsSource = top20;
                var tiers = ctx.Customers.GroupBy(c => c.Tier)
                    .Select(g => new { Tier = g.Key, Count = g.Count(), AvgPoints = ((int)g.Average(c => c.LoyaltyPoints)).ToString("N0"), AvgSpent = g.Average(c => c.TotalSpent).ToString("N2") })
                    .OrderByDescending(x => x.Count).ToList();
                _tierGrid.ItemsSource = tiers;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCustomerData error: {ex}");
            }
        }

        private void LoadInventoryData()
        {
            try
            {
                using var ctx = new POSDbContext();

                // Try InventoryStocks; fall back to Artikujt when table is empty or missing
                bool usedInventoryStocks = false;
                try
                {
                    var invStocks = ctx.InventoryStocks
                        .Where(s => s.CurrentQuantity <= s.MinimumQuantity)
                        .OrderBy(s => s.CurrentQuantity)
                        .ToList();

                    if (invStocks.Any())
                    {
                        _inventoryGrid.ItemsSource = invStocks
                            .Select(s => new
                            {
                                s.ArticleName,
                                CurrentQuantity = s.CurrentQuantity.ToString("N2"),
                                MinimumQuantity = s.MinimumQuantity.ToString("N2"),
                                s.Unit,
                                StockStatus = s.CurrentQuantity == 0 ? "Pa stok" : "Stok i ul\u00ebt"
                            }).ToList();
                        usedInventoryStocks = true;
                    }
                }
                catch
                {
                    // InventoryStocks table missing – will fall through to Artikujt
                }

                if (!usedInventoryStocks)
                {
                    // Fall back to Artikujt table – show lowest-stock items
                    var artikujt = ctx.Artikujt
                        .Where(a => (a.Sasia ?? 0) < 20)
                        .OrderBy(a => a.Sasia)
                        .Take(50)
                        .ToList();

                    if (!artikujt.Any())
                    {
                        artikujt = ctx.Artikujt
                            .OrderBy(a => a.Sasia)
                            .Take(30)
                            .ToList();
                    }

                    if (artikujt.Any())
                    {
                        _inventoryGrid.ItemsSource = artikujt
                            .Select(a => new
                            {
                                ArticleName = a.Emertimi ?? "Pa em\u00ebr",
                                CurrentQuantity = ((decimal)(a.Sasia ?? 0)).ToString("N2"),
                                MinimumQuantity = ((decimal)(a.StoguMinimal ?? 0)).ToString("N2"),
                                Unit = a.Unit ?? "cop\u00eb",
                                StockStatus = (a.Sasia ?? 0) <= 0 ? "\u26d4 Pa stok" : (a.Sasia ?? 0) < 5 ? "\ud83d\udd34 Stok kritik" : "\u26a0 Stok i ul\u00ebt"
                            }).ToList();
                    }
                    else
                    {
                        // Final fallback: KosovaPOS Articles table
                        try
                        {
                            var articles = ctx.Articles
                                .Where(a => a.StockQuantity < 20)
                                .OrderBy(a => a.StockQuantity)
                                .Take(50)
                                .ToList();

                            if (!articles.Any())
                                articles = ctx.Articles.OrderBy(a => a.StockQuantity).Take(30).ToList();

                            _inventoryGrid.ItemsSource = articles.Select(a => new
                            {
                                ArticleName = a.Name,
                                CurrentQuantity = a.StockQuantity.ToString("N2"),
                                MinimumQuantity = "10",
                                Unit = a.Unit,
                                StockStatus = a.StockQuantity <= 0 ? "\u26d4 Pa stok" : a.StockQuantity < 5 ? "\ud83d\udd34 Stok kritik" : "\u26a0 Stok i ul\u00ebt"
                            }).ToList();
                        }
                        catch { /* Articles table also unavailable */ }
                    }
                }

                // Campaigns
                try
                {
                    var camp = ctx.Campaigns
                        .OrderByDescending(c => c.UsageCount)
                        .Take(20)
                        .ToList()
                        .Select(c => new
                        {
                            c.Name,
                            TotalRevenue = c.TotalRevenue.ToString("N2"),
                            c.UsageCount,
                            Status = c.IsActive ? "Aktive" : "Joaktive"
                        }).ToList();
                    _campaignGrid.ItemsSource = camp;
                }
                catch
                {
                    // Campaigns table may be missing
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadInventoryData error: {ex}");
            }
        }

        private void LoadStaffData(DateTime from, DateTime to)
        {
            try
            {
                using var ctx = new POSDbContext();
                var staff = ctx.StaffMembers.OrderBy(s => s.Name).ToList();
                var cards = ctx.TimeCards.Where(tc => tc.Date >= from.Date && tc.Date < to.Date).ToList();
                var result = staff.Select(s => new
                {
                    s.Name,
                    Position = string.IsNullOrEmpty(s.Position) ? "--" : s.Position,
                    HourlyRate = s.HourlyRate.ToString("N2"),
                    IsActive = s.IsActive ? "Po" : "Jo",
                    HireDate = s.HireDate.ToString("dd/MM/yyyy"),
                    HoursToday = cards.Where(tc => tc.EmployeeId == s.Id && tc.TotalHours > 0).Sum(tc => tc.TotalHours).ToString("N1") + "h"
                }).ToList();
                _staffGrid.ItemsSource = result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadStaffData error: {ex}");
            }
        }

        // ── UI Helpers ────────────────────────────────────────────────────────────
        private Border KpiCard(string title, TextBlock lbl, string hex)
        {
            var b = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
                CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(6)
            };
            b.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Opacity = 0.12, BlurRadius = 16, ShadowDepth = 4, Direction = 270 };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            lbl.Text = "--"; lbl.FontSize = 28; lbl.FontWeight = FontWeights.Bold;
            lbl.Foreground = Brushes.White; lbl.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(lbl); b.Child = sp; return b;
        }

        private StackPanel Card(string title)
        {
            var b = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(6) };
            b.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Opacity = 0.08, BlurRadius = 12, ShadowDepth = 3, Direction = 270 };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30, 58, 95)), Margin = new Thickness(0, 0, 0, 12) });
            b.Child = sp; return sp;
        }

        private Border WrapInBorder(StackPanel sp)
        {
            var b = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(6) };
            b.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Opacity = 0.08, BlurRadius = 12, ShadowDepth = 3, Direction = 270 };
            b.Child = sp; return b;
        }

        private static DataGrid Dg() => new DataGrid
        {
            AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
            Background = Brushes.White, AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(243, 244, 246)),
            BorderThickness = new Thickness(0), SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow, RowHeight = 42, ColumnHeaderHeight = 40, FontSize = 13
        };

        private static System.Windows.Data.Binding Bind(string path) => new System.Windows.Data.Binding(path);

        private static Button Btn(string text, string hex, double w, double lm = 0) => new Button
        {
            Content = text, Width = w, Height = 36, Margin = new Thickness(lm, 0, 0, 0),
            Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!,
            Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol"),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
        };
    }
}
