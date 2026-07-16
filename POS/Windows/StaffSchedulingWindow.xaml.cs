using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KosovaPOS.Database;
using KosovaPOS.Models;

namespace KosovaPOS.Windows
{
    public partial class StaffSchedulingWindow : Window
    {
        private DataGrid _staffDataGrid = new DataGrid();
        private DataGrid _timeCardDataGrid = new DataGrid();
        private DataGrid _scheduleDataGrid = new DataGrid();
        private DatePicker _tcFromDate = new DatePicker();
        private DatePicker _tcToDate = new DatePicker();

        public StaffSchedulingWindow()
        {
            InitializeComponent();
            LoadStaffSchedules();
        }

        private void InitializeComponent()
        {
            Title = "\U0001F465 Menaxhimi i stafit dhe orarit";
            Width = 1400;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowState = WindowState.Maximized;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerBorder = new Border { Padding = new Thickness(24, 18, 24, 18) };
            headerBorder.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(3, 105, 161), 0);
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock { Text = "\U0001F465 Menaxhimi i stafit dhe orarit", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            headerStack.Children.Add(new TextBlock { Text = "Orari i punonjësve, kartela e kohës dhe shtimi i stafit", FontSize = 14, Foreground = Brushes.White, Opacity = 0.9, Margin = new Thickness(0, 4, 0, 0) });
            headerBorder.Child = headerStack;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            var tabControl = new TabControl { Margin = new Thickness(20, 16, 20, 8), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            Grid.SetRow(tabControl, 1);

            tabControl.Items.Add(CreateScheduleTab());
            tabControl.Items.Add(CreateStaffListTab());
            tabControl.Items.Add(CreateTimeCardTab());
            mainGrid.Children.Add(tabControl);

            var footerPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(20, 0, 20, 14) };
            Grid.SetRow(footerPanel, 2);
            var btnClose = MakeBtn("\u2716 Mbyll", "#64748B", 120);
            btnClose.Click += (s, e) => Close();
            footerPanel.Children.Add(btnClose);
            mainGrid.Children.Add(footerPanel);

            Content = mainGrid;
        }

        private TabItem CreateScheduleTab()
        {
            var tab = new TabItem { Header = "\U0001F4C5 Orari Javor", Padding = new Thickness(14, 8, 14, 8) };
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 10) };
            var btnAddShift = MakeBtn("+ Shto Turno", "#0284C7", 140);
            btnAddShift.Click += AddShift_Click;
            var btnRefresh = MakeBtn("\U0001F504 Rifresko", "#0891B2", 120, 10);
            btnRefresh.Click += (s, e) => RefreshSchedule();
            toolbar.Children.Add(btnAddShift);
            toolbar.Children.Add(btnRefresh);
            Grid.SetRow(toolbar, 0);
            panel.Children.Add(toolbar);

            var weekLabel = new TextBlock
            {
                Text = $"Java: {GetWeekStart():dd/MM/yyyy} - {GetWeekEnd():dd/MM/yyyy}",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(weekLabel, 1);
            panel.Children.Add(weekLabel);

            _scheduleDataGrid = MakeGrid();
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Punonjësi", Binding = new System.Windows.Data.Binding("EmployeeName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding("ShiftDateDisplay"), Width = 100 });
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Turni", Binding = new System.Windows.Data.Binding("ShiftType"), Width = 90 });
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Fillimi", Binding = new System.Windows.Data.Binding("StartTimeDisplay"), Width = 80 });
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Mbarimi", Binding = new System.Windows.Data.Binding("EndTimeDisplay"), Width = 80 });
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Pozita", Binding = new System.Windows.Data.Binding("Position"), Width = 100 });
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Statusi", Binding = new System.Windows.Data.Binding("Status"), Width = 90 });
            _scheduleDataGrid.Columns.Add(new DataGridTextColumn { Header = "Orët", Binding = new System.Windows.Data.Binding("HoursDisplay"), Width = 80 });
            Grid.SetRow(_scheduleDataGrid, 2);
            panel.Children.Add(_scheduleDataGrid);

            tab.Content = new Border { Child = panel };
            return tab;
        }

        private TabItem CreateStaffListTab()
        {
            var tab = new TabItem { Header = "\U0001F465 Stafi", Padding = new Thickness(14, 8, 14, 8) };
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 10) };
            var btnAdd = MakeBtn("+ Shto Punonjës", "#22C55E", 155);
            btnAdd.Click += AddStaff_Click;
            var btnEdit = MakeBtn("\u270F Ndrysho", "#F59E0B", 110, 10);
            btnEdit.Click += EditStaff_Click;
            var btnToggle = MakeBtn("\U0001F504 Aktivizo/\u00C7aktivizo", "#64748B", 170, 10);
            btnToggle.Click += DeactivateStaff_Click;
            var btnRefresh = MakeBtn("\U0001F504 Rifresko", "#0891B2", 110, 10);
            btnRefresh.Click += (s, e) => RefreshStaffList();
            toolbar.Children.Add(btnAdd);
            toolbar.Children.Add(btnEdit);
            toolbar.Children.Add(btnToggle);
            toolbar.Children.Add(btnRefresh);
            Grid.SetRow(toolbar, 0);
            panel.Children.Add(toolbar);

            _staffDataGrid = MakeGrid();
            _staffDataGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding("Id"), Width = 50 });
            _staffDataGrid.Columns.Add(new DataGridTextColumn { Header = "Emri", Binding = new System.Windows.Data.Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _staffDataGrid.Columns.Add(new DataGridTextColumn { Header = "Pozita", Binding = new System.Windows.Data.Binding("Position"), Width = 140 });
            _staffDataGrid.Columns.Add(new DataGridTextColumn { Header = "Telefoni", Binding = new System.Windows.Data.Binding("Phone"), Width = 120 });
            _staffDataGrid.Columns.Add(new DataGridTextColumn { Header = "Email", Binding = new System.Windows.Data.Binding("Email"), Width = 180 });
            _staffDataGrid.Columns.Add(new DataGridTextColumn { Header = "Tarifa/Orë", Binding = new System.Windows.Data.Binding("HourlyRateDisplay"), Width = 110 });
            _staffDataGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Aktiv", Binding = new System.Windows.Data.Binding("IsActive") { Mode = System.Windows.Data.BindingMode.OneWay }, Width = 60 });
            _staffDataGrid.Columns.Add(new DataGridTextColumn { Header = "Punësuar", Binding = new System.Windows.Data.Binding("HireDateDisplay"), Width = 110 });
            Grid.SetRow(_staffDataGrid, 1);
            panel.Children.Add(_staffDataGrid);

            tab.Content = new Border { Child = panel };
            return tab;
        }

        private TabItem CreateTimeCardTab()
        {
            var tab = new TabItem { Header = "\u23F1 Kartela e Kohës", Padding = new Thickness(14, 8, 14, 8) };
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            ComboBox empCombo = null!;

            var clockBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 10, 0, 14)
            };
            var clockGrid = new Grid();
            clockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            clockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            clockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            clockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var empStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            empStack.Children.Add(new TextBlock { Text = "Punonjësi: ", FontWeight = FontWeights.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            empCombo = new ComboBox { Width = 200, Height = 36, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center };
            empStack.Children.Add(empCombo);
            Grid.SetColumn(empStack, 0);
            clockGrid.Children.Add(empStack);

            var btnIn = MakeBtn("\u25B6 Hyrje", "#22C55E", 110);
            btnIn.Height = 36;
            btnIn.Click += (s, e) => ClockIn(empCombo);
            Grid.SetColumn(btnIn, 1);
            clockGrid.Children.Add(btnIn);

            var btnOut = MakeBtn("\u25A0 Dalje", "#D97706", 110, 10);
            btnOut.Height = 36;
            btnOut.Click += (s, e) => ClockOut(empCombo);
            Grid.SetColumn(btnOut, 2);
            clockGrid.Children.Add(btnOut);

            var btnRefTC = MakeBtn("\U0001F504 Rifresko", "#0891B2", 110, 10);
            btnRefTC.Height = 36;
            btnRefTC.Click += (s, e) => RefreshTimeCards(empCombo);
            Grid.SetColumn(btnRefTC, 3);
            clockGrid.Children.Add(btnRefTC);

            clockBorder.Child = clockGrid;
            Grid.SetRow(clockBorder, 0);
            panel.Children.Add(clockBorder);

            // Date range filter bar
            _tcFromDate = new DatePicker { Height = 34, Width = 140, SelectedDate = DateTime.Today.AddMonths(-1), VerticalContentAlignment = VerticalAlignment.Center };
            _tcToDate   = new DatePicker { Height = 34, Width = 140, SelectedDate = DateTime.Today,               VerticalContentAlignment = VerticalAlignment.Center };

            var filterBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 8)
            };
            var filterGrid = new Grid();
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblFrom = new TextBlock { Text = "Nga:", FontWeight = FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(lblFrom, 0);
            filterGrid.Children.Add(lblFrom);

            Grid.SetColumn(_tcFromDate, 1);
            filterGrid.Children.Add(_tcFromDate);

            var lblTo = new TextBlock { Text = "Deri:", FontWeight = FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) };
            Grid.SetColumn(lblTo, 2);
            filterGrid.Children.Add(lblTo);

            Grid.SetColumn(_tcToDate, 3);
            filterGrid.Children.Add(_tcToDate);

            var btnFilterTC = MakeBtn("\U0001F50D Filtro", "#0284C7", 100, 10);
            btnFilterTC.Height = 34;
            btnFilterTC.Click += (s, e) => RefreshTimeCards(empCombo);
            Grid.SetColumn(btnFilterTC, 4);
            filterGrid.Children.Add(btnFilterTC);

            filterBorder.Child = filterGrid;
            Grid.SetRow(filterBorder, 1);
            panel.Children.Add(filterBorder);

            var rangeLabel = new TextBlock
            {
                Text = "Regjistrimet e hyrjeve dhe daljeve",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(rangeLabel, 2);
            panel.Children.Add(rangeLabel);

            _timeCardDataGrid = MakeGrid();
            _timeCardDataGrid.Columns.Add(new DataGridTextColumn { Header = "Punonjësi", Binding = new System.Windows.Data.Binding("EmployeeName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _timeCardDataGrid.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding("DateDisplay"), Width = 100 });
            _timeCardDataGrid.Columns.Add(new DataGridTextColumn { Header = "Hyrja", Binding = new System.Windows.Data.Binding("ClockInDisplay"), Width = 110 });
            _timeCardDataGrid.Columns.Add(new DataGridTextColumn { Header = "Dalja", Binding = new System.Windows.Data.Binding("ClockOutDisplay"), Width = 110 });
            _timeCardDataGrid.Columns.Add(new DataGridTextColumn { Header = "Orë Totale", Binding = new System.Windows.Data.Binding("TotalHoursDisplay"), Width = 100 });
            _timeCardDataGrid.Columns.Add(new DataGridTextColumn { Header = "Shënime", Binding = new System.Windows.Data.Binding("Notes"), Width = 180 });
            Grid.SetRow(_timeCardDataGrid, 3);
            panel.Children.Add(_timeCardDataGrid);

            tab.Content = new Border { Child = panel };
            Loaded += (s, e) => { PopulateEmpCombo(empCombo); RefreshTimeCards(empCombo); };
            return tab;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────────

        private static DataGrid MakeGrid() => new DataGrid
        {
            AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
            Background = Brushes.White, AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)), BorderThickness = new Thickness(1),
            SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow
        };

        private static Button MakeBtn(string text, string hex, double width, double leftMargin = 0) => new Button
        {
            Content = text, Width = width, Height = 38, Margin = new Thickness(leftMargin, 0, 0, 0),
            Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!,
            Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol"),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
        };

        private static DateTime GetWeekStart() => DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
        private static DateTime GetWeekEnd() => GetWeekStart().AddDays(6);

        private void PopulateEmpCombo(ComboBox combo)
        {
            try
            {
                using var ctx = new POSDbContext();
                var staff = ctx.StaffMembers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                combo.Items.Clear();
                foreach (var s in staff)
                    combo.Items.Add(new ComboBoxItem { Content = s.Name, Tag = s.Id });
                if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            }
            catch { }
        }

        // ─── Schedule ────────────────────────────────────────────────────────────────

        private void RefreshSchedule()
        {
            try
            {
                using var ctx = new POSDbContext();
                var ws = GetWeekStart();
                var we = GetWeekEnd().AddDays(1);
                var shifts = ctx.WorkShifts.Where(s => s.ShiftDate >= ws && s.ShiftDate < we)
                    .OrderBy(s => s.ShiftDate).ThenBy(s => s.StartTime).ToList();
                _scheduleDataGrid.ItemsSource = shifts.Select(s => new
                {
                    s.EmployeeName,
                    ShiftDateDisplay = s.ShiftDate.ToString("dd/MM ddd"),
                    s.ShiftType,
                    StartTimeDisplay = s.StartTime.ToString(@"hh\:mm"),
                    EndTimeDisplay = s.EndTime.ToString(@"hh\:mm"),
                    Position = s.Position ?? "-",
                    s.Status,
                    HoursDisplay = s.HoursWorked > 0 ? $"{s.HoursWorked:N1}h" : "-"
                }).ToList();
            }
            catch (Exception ex) { ShowErr($"Gabim orari:\n{ex.Message}"); }
        }

        private void AddShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var ctx = new POSDbContext();
                var staff = ctx.StaffMembers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                if (!staff.Any())
                {
                    MessageBox.Show("Nuk ka punonjës aktivë. Shto staf fillimisht.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var dlg = new Window { Title = "Shto Turno", Width = 440, Height = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
                var sp = new StackPanel { Margin = new Thickness(20) };

                sp.Children.Add(MakeLabel("Punonjësi:"));
                var empC = new ComboBox { Height = 34, FontSize = 13 };
                foreach (var s in staff) empC.Items.Add(new ComboBoxItem { Content = s.Name, Tag = s.Id });
                empC.SelectedIndex = 0;
                sp.Children.Add(empC);

                sp.Children.Add(MakeLabel("Data:", 10));
                var dp = new DatePicker { Height = 34, SelectedDate = DateTime.Today };
                sp.Children.Add(dp);

                sp.Children.Add(MakeLabel("Lloji:", 10));
                var typeC = new ComboBox { Height = 34, FontSize = 13 };
                foreach (var t in new[] { "Morning", "Afternoon", "Evening", "Night", "Custom" }) typeC.Items.Add(t);
                typeC.SelectedIndex = 0;
                sp.Children.Add(typeC);

                var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var startSp = new StackPanel();
                startSp.Children.Add(MakeLabel("Fillimi (HH:mm):"));
                var startB = new TextBox { Height = 34, FontSize = 13, Text = "08:00", Padding = new Thickness(8, 0, 8, 0) };
                startSp.Children.Add(startB);
                Grid.SetColumn(startSp, 0);
                var endSp = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
                endSp.Children.Add(MakeLabel("Mbarimi (HH:mm):"));
                var endB = new TextBox { Height = 34, FontSize = 13, Text = "16:00", Padding = new Thickness(8, 0, 8, 0) };
                endSp.Children.Add(endB);
                Grid.SetColumn(endSp, 1);
                row.Children.Add(startSp);
                row.Children.Add(endSp);
                sp.Children.Add(row);

                sp.Children.Add(MakeLabel("Pozita:", 10));
                var posB = new TextBox { Height = 34, FontSize = 13, Text = "Arkatare", Padding = new Thickness(8, 0, 8, 0) };
                sp.Children.Add(posB);

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
                var bSave = MakeBtn("\U0001F4BE Ruaj", "#22C55E", 110);
                var bCancel = MakeBtn("\u2716 Anulo", "#6B7280", 100, 10);
                bCancel.Click += (_, __) => dlg.Close();
                bSave.Click += (_, __) =>
                {
                    var sel = empC.SelectedItem as ComboBoxItem;
                    if (sel == null) return;
                    if (!TimeSpan.TryParse(startB.Text, out var st) || !TimeSpan.TryParse(endB.Text, out var et))
                    { MessageBox.Show("Formati i orës duhet të jetë HH:mm", "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                    try
                    {
                        using var ctx2 = new POSDbContext();
                        ctx2.WorkShifts.Add(new WorkShift
                        {
                            EmployeeId = (int)sel.Tag, EmployeeName = sel.Content.ToString()!,
                            ShiftDate = dp.SelectedDate ?? DateTime.Today,
                            ShiftType = typeC.SelectedItem?.ToString() ?? "Morning",
                            StartTime = st, EndTime = et, Position = posB.Text.Trim(),
                            Status = "Scheduled", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
                        });
                        ctx2.SaveChanges();
                        dlg.Close();
                        RefreshSchedule();
                        MessageBox.Show("Turni u shtua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex2) { ShowErr($"Gabim: {ex2.Message}"); }
                };
                btnRow.Children.Add(bSave);
                btnRow.Children.Add(bCancel);
                sp.Children.Add(btnRow);
                dlg.Content = new ScrollViewer { Content = sp };
                dlg.ShowDialog();
            }
            catch (Exception ex) { ShowErr($"Gabim: {ex.Message}"); }
        }

        // ─── Staff List ───────────────────────────────────────────────────────────────

        private void RefreshStaffList()
        {
            try
            {
                using var ctx = new POSDbContext();
                _staffDataGrid.ItemsSource = ctx.StaffMembers.OrderBy(s => s.Name).ToList().Select(s => new
                {
                    s.Id, s.Name, s.Position, s.Phone, s.Email,
                    HourlyRateDisplay = $"€{s.HourlyRate:N2}", s.IsActive,
                    HireDateDisplay = s.HireDate.ToString("dd/MM/yyyy")
                }).ToList();
            }
            catch (Exception ex) { ShowErr($"Gabim stafi:\n{ex.Message}"); }
        }

        private void AddStaff_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Window { Title = "Shto Punonjës të Ri", Width = 450, Height = 490, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
            var sp = new StackPanel { Margin = new Thickness(20) };

            sp.Children.Add(MakeLabel("Emri i Plotë *:"));
            var txtName = MakeTb();
            sp.Children.Add(txtName);

            sp.Children.Add(MakeLabel("Pozita:", 10));
            var cPos = new ComboBox { Height = 34, FontSize = 13, IsEditable = true };
            foreach (var p in new[] { "Arkatare", "Kuzhinier", "Kamarier", "Shofer", "Menaxher", "Sigurimi", "Pastrues" }) cPos.Items.Add(p);
            cPos.SelectedIndex = 0;
            sp.Children.Add(cPos);

            sp.Children.Add(MakeLabel("Telefoni:", 10));
            var txtPhone = MakeTb();
            sp.Children.Add(txtPhone);

            sp.Children.Add(MakeLabel("Email:", 10));
            var txtEmail = MakeTb();
            sp.Children.Add(txtEmail);

            sp.Children.Add(MakeLabel("Tarifa Orare (€):", 10));
            var txtRate = MakeTb("0.00");
            sp.Children.Add(txtRate);

            sp.Children.Add(MakeLabel("Data e Punësimit:", 10));
            var dpHire = new DatePicker { Height = 34, SelectedDate = DateTime.Today };
            sp.Children.Add(dpHire);

            sp.Children.Add(MakeLabel("Shënime:", 10));
            var txtNotes = new TextBox { Height = 58, FontSize = 13, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Padding = new Thickness(8, 6, 8, 6), BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)), BorderThickness = new Thickness(1) };
            sp.Children.Add(txtNotes);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            var bSave = MakeBtn("\U0001F4BE Ruaj", "#22C55E", 110);
            var bCancel = MakeBtn("\u2716 Anulo", "#6B7280", 100, 10);
            bCancel.Click += (_, __) => dlg.Close();
            bSave.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("Emri është i detyrueshëm!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning); txtName.Focus(); return; }
                try
                {
                    decimal.TryParse(txtRate.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate);
                    using var ctx = new POSDbContext();
                    ctx.StaffMembers.Add(new StaffMember
                    {
                        Name = txtName.Text.Trim(), Position = cPos.Text?.Trim() ?? "",
                        Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text.Trim(),
                        Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim(),
                        HourlyRate = rate, HireDate = dpHire.SelectedDate ?? DateTime.Today,
                        Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim(),
                        IsActive = true, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
                    });
                    ctx.SaveChanges();
                    dlg.Close();
                    RefreshStaffList();
                    MessageBox.Show("Punonjësi u shtua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { ShowErr($"Gabim:\n{ex.Message}\n\n{ex.InnerException?.Message}"); }
            };
            btnRow.Children.Add(bSave);
            btnRow.Children.Add(bCancel);
            sp.Children.Add(btnRow);
            dlg.Content = new ScrollViewer { Content = sp };
            dlg.ShowDialog();
        }

        private void EditStaff_Click(object sender, RoutedEventArgs e)
        {
            if (_staffDataGrid.SelectedItem == null) { MessageBox.Show("Zgjidhni punonjësin nga lista.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            try
            {
                dynamic sel = _staffDataGrid.SelectedItem;
                int empId = sel.Id;
                using var ctx = new POSDbContext();
                var emp = ctx.StaffMembers.Find(empId);
                if (emp == null) return;

                var dlg = new Window { Title = $"Ndrysho: {emp.Name}", Width = 420, Height = 380, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
                var sp = new StackPanel { Margin = new Thickness(20) };

                sp.Children.Add(MakeLabel("Emri *:"));
                var tName = MakeTb(emp.Name);
                sp.Children.Add(tName);
                sp.Children.Add(MakeLabel("Pozita:", 10));
                var tPos = MakeTb(emp.Position);
                sp.Children.Add(tPos);
                sp.Children.Add(MakeLabel("Telefoni:", 10));
                var tPhone = MakeTb(emp.Phone ?? "");
                sp.Children.Add(tPhone);
                sp.Children.Add(MakeLabel("Tarifa Orare (€):", 10));
                var tRate = MakeTb(emp.HourlyRate.ToString("N2"));
                sp.Children.Add(tRate);
                var chkActive = new CheckBox { Content = "Aktiv", IsChecked = emp.IsActive, FontSize = 14, Margin = new Thickness(0, 12, 0, 0) };
                sp.Children.Add(chkActive);

                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
                var bSave = MakeBtn("\U0001F4BE Ruaj", "#22C55E", 110);
                var bCancel = MakeBtn("\u2716 Anulo", "#6B7280", 100, 10);
                bCancel.Click += (_, __) => dlg.Close();
                bSave.Click += (_, __) =>
                {
                    if (string.IsNullOrWhiteSpace(tName.Text)) return;
                    try
                    {
                        decimal.TryParse(tRate.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate);
                        using var ctx2 = new POSDbContext();
                        var e2 = ctx2.StaffMembers.Find(empId);
                        if (e2 == null) return;
                        e2.Name = tName.Text.Trim();
                        e2.Position = tPos.Text.Trim();
                        e2.Phone = string.IsNullOrWhiteSpace(tPhone.Text) ? null : tPhone.Text.Trim();
                        e2.HourlyRate = rate;
                        e2.IsActive = chkActive.IsChecked == true;
                        e2.UpdatedAt = DateTime.Now;
                        ctx2.SaveChanges();
                        dlg.Close();
                        RefreshStaffList();
                        MessageBox.Show("Punonjësi u përditësua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex3) { ShowErr($"Gabim: {ex3.Message}"); }
                };
                btnRow.Children.Add(bSave);
                btnRow.Children.Add(bCancel);
                sp.Children.Add(btnRow);
                dlg.Content = new ScrollViewer { Content = sp };
                dlg.ShowDialog();
            }
            catch (Exception ex) { ShowErr($"Gabim: {ex.Message}"); }
        }

        private void DeactivateStaff_Click(object sender, RoutedEventArgs e)
        {
            if (_staffDataGrid.SelectedItem == null) { MessageBox.Show("Zgjidhni punonjësin nga lista.", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            try
            {
                dynamic sel = _staffDataGrid.SelectedItem;
                int empId = sel.Id;
                bool isActive = sel.IsActive;
                var action = isActive ? "çaktivizoni" : "aktivizoni";
                if (MessageBox.Show($"A dëshironi të {action} këtë punonjës?", "Konfirmim", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                using var ctx = new POSDbContext();
                var emp = ctx.StaffMembers.Find(empId);
                if (emp == null) return;
                emp.IsActive = !emp.IsActive;
                emp.UpdatedAt = DateTime.Now;
                if (!emp.IsActive) emp.TerminationDate = DateTime.Now;
                ctx.SaveChanges();
                RefreshStaffList();
            }
            catch (Exception ex) { ShowErr($"Gabim: {ex.Message}"); }
        }

        // ─── Time Cards ───────────────────────────────────────────────────────────────

        private void ClockIn(ComboBox empCombo)
        {
            var sel = empCombo.SelectedItem as ComboBoxItem;
            if (sel == null) { ShowErr("Zgjidhni punonjësin."); return; }
            try
            {
                using var ctx = new POSDbContext();
                var existing = ctx.TimeCards.Where(tc => tc.EmployeeId == (int)sel.Tag && tc.ClockOutTime == null).FirstOrDefault();
                if (existing != null) { MessageBox.Show("Ky punonjës tashmë është i hyrë.", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                ctx.TimeCards.Add(new TimeCard
                {
                    EmployeeId = (int)sel.Tag, EmployeeName = sel.Content.ToString()!,
                    Date = DateTime.Today, ClockInTime = DateTime.Now, CreatedAt = DateTime.Now
                });
                ctx.SaveChanges();
                RefreshTimeCards(empCombo);
                MessageBox.Show($"Hyrja u regjistrua: {DateTime.Now:HH:mm:ss}", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { ShowErr($"Gabim: {ex.Message}"); }
        }

        private void ClockOut(ComboBox empCombo)
        {
            var sel = empCombo.SelectedItem as ComboBoxItem;
            if (sel == null) { ShowErr("Zgjidhni punonjësin."); return; }
            try
            {
                using var ctx = new POSDbContext();
                var card = ctx.TimeCards.Where(tc => tc.EmployeeId == (int)sel.Tag && tc.ClockOutTime == null).OrderByDescending(tc => tc.ClockInTime).FirstOrDefault();
                if (card == null) { MessageBox.Show("Nuk u gjet regjistrimi i hyrjes.", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                card.ClockOutTime = DateTime.Now;
                card.TotalHours = (decimal)(DateTime.Now - card.ClockInTime).TotalHours;
                ctx.SaveChanges();
                RefreshTimeCards(empCombo);
                MessageBox.Show($"Dalja u regjistrua: {DateTime.Now:HH:mm:ss}\nOrë totale: {card.TotalHours:N2}", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { ShowErr($"Gabim: {ex.Message}"); }
        }

        private void RefreshTimeCards(ComboBox empCombo)
        {
            try
            {
                using var ctx = new POSDbContext();
                var from = (_tcFromDate.SelectedDate ?? DateTime.Today.AddMonths(-1)).Date;
                var to   = (_tcToDate.SelectedDate   ?? DateTime.Today).Date.AddDays(1);

                var query = ctx.TimeCards.Where(tc => tc.Date >= from && tc.Date < to);

                // If a specific employee is selected, filter to that employee
                var sel = empCombo?.SelectedItem as ComboBoxItem;
                if (sel != null)
                    query = query.Where(tc => tc.EmployeeId == (int)sel.Tag);

                var cards = query
                    .OrderByDescending(tc => tc.Date)
                    .ThenByDescending(tc => tc.ClockInTime)
                    .ToList();

                _timeCardDataGrid.ItemsSource = cards.Select(tc => new
                {
                    tc.EmployeeName,
                    DateDisplay = tc.Date.ToString("dd/MM/yyyy"),
                    ClockInDisplay = tc.ClockInTime.ToString("HH:mm:ss"),
                    ClockOutDisplay = tc.ClockOutTime?.ToString("HH:mm:ss") ?? "-",
                    TotalHoursDisplay = tc.TotalHours > 0 ? $"{tc.TotalHours:N2}h" : "-",
                    tc.Notes
                }).ToList();
            }
            catch (Exception ex) { ShowErr($"Gabim kartela:\n{ex.Message}"); }
        }

        private void LoadStaffSchedules()
        {
            Loaded += (s, e) => { RefreshStaffList(); RefreshSchedule(); };
        }

        private static TextBlock MakeLabel(string text, double topMargin = 0) =>
            new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, topMargin, 0, 4) };

        private static TextBox MakeTb(string text = "") =>
            new TextBox { Height = 34, FontSize = 13, Text = text, Padding = new Thickness(8, 0, 8, 0), BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)), BorderThickness = new Thickness(1) };

        private void ShowErr(string msg) => MessageBox.Show(msg, "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
