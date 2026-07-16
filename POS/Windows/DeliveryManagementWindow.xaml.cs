using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;

namespace KosovaPOS.Windows
{
    public partial class DeliveryManagementWindow : Window
    {
        private ObservableCollection<DeliveryOrder> _activeDeliveries = new ObservableCollection<DeliveryOrder>();
        private ObservableCollection<DeliveryDriver> _drivers = new ObservableCollection<DeliveryDriver>();
        private ObservableCollection<DeliveryOrder> _history = new ObservableCollection<DeliveryOrder>();

        public DeliveryManagementWindow()
        {
            InitializeComponent();
            InitializeDates();
            LoadData();
            
            // Auto-refresh every 30 seconds
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(30);
            timer.Tick += (s, e) => LoadData();
            timer.Start();
        }

        private void InitializeDates()
        {
            FromDate.SelectedDate = DateTime.Today;
            ToDate.SelectedDate = DateTime.Today;
        }

        private void LoadData()
        {
            try
            {
                using var context = new POSDbContext();

                // Load active deliveries (not completed or cancelled)
                var activeStatuses = new[] { "Pending", "Preparing", "Ready", "OnTheWay" };
                _activeDeliveries = new ObservableCollection<DeliveryOrder>(
                    context.DeliveryOrders
                        .Where(d => activeStatuses.Contains(d.Status))
                        .OrderBy(d => d.CreatedAt)
                        .ToList()
                );
                ActiveDeliveriesGrid.ItemsSource = _activeDeliveries;

                // Load drivers
                _drivers = new ObservableCollection<DeliveryDriver>(
                    context.DeliveryDrivers
                        .Where(d => d.IsActive)
                        .OrderBy(d => d.Name)
                        .ToList()
                );
                DriversGrid.ItemsSource = _drivers;

                // Update statistics
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Invalid object name"))
                {
                    if (ex.Message.Contains("DeliveryOrders"))
                        DatabaseHelper.ShowTableMissingError("DeliveryOrders", "Menaxhimi i Dërgesave");
                    else if (ex.Message.Contains("DeliveryDrivers"))
                        DatabaseHelper.ShowTableMissingError("DeliveryDrivers", "Menaxhimi i Shoferëve");
                }
                else
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave:\n\n{ex.Message}", 
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateStatistics()
        {
            txtPendingOrders.Text = _activeDeliveries.Count(d => d.Status == "Pending").ToString();
            txtPreparingOrders.Text = _activeDeliveries.Count(d => d.Status == "Preparing").ToString();
            txtOnTheWayOrders.Text = _activeDeliveries.Count(d => d.Status == "OnTheWay").ToString();

            using var context = new POSDbContext();
            var completedToday = context.DeliveryOrders
                .Count(d => d.Status == "Delivered" && d.DeliveredAt.HasValue && 
                           d.DeliveredAt.Value.Date == DateTime.Today);
            txtCompletedToday.Text = completedToday.ToString();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            MessageBox.Show("Të dhënat u rifreshuan!", "Sukses", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddDeliveryOrder_Click(object sender, RoutedEventArgs e)
        {
            ShowDeliveryOrderDialog(null);
        }

        private void EditDeliveryOrder_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveDeliveriesGrid.SelectedItem is not DeliveryOrder selected)
            {
                MessageBox.Show("Zgjidhni një porosi nga lista për të modifikuar.",
                    "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ShowDeliveryOrderDialog(selected);
        }

        private void DeleteDeliveryOrder_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveDeliveriesGrid.SelectedItem is not DeliveryOrder selected)
            {
                MessageBox.Show("Zgjidhni një porosi nga lista për të fshirë.",
                    "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show($"A jeni të sigurt që dëshironi të fshini porosinë #{selected.Id} ({selected.CustomerName})?",
                "Konfirmim", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                using var ctx = new Database.POSDbContext();
                var order = ctx.DeliveryOrders.Find(selected.Id);
                if (order != null)
                {
                    ctx.DeliveryOrders.Remove(order);
                    ctx.SaveChanges();
                }
                LoadData();
                MessageBox.Show("Porosia u fshi me sukses.", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë fshirjes:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDeliveryStatus_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveDeliveriesGrid.SelectedItem is not DeliveryOrder selected)
            {
                MessageBox.Show("Zgjidhni një porosi nga lista për të ndryshuar statusin.",
                    "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new Window
            {
                Title = $"📋 Ndrysho statusin – Porosia #{selected.Id}",
                Width = 360, Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };
            var sp = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24) };
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "Statusi i ri:", FontWeight = System.Windows.FontWeights.SemiBold, FontSize = 13, Margin = new System.Windows.Thickness(0, 0, 0, 8) });
            var cmb = new System.Windows.Controls.ComboBox { Height = 36, FontSize = 13, Margin = new System.Windows.Thickness(0, 0, 0, 16) };
            foreach (var s in new[] { "Pending", "Preparing", "Ready", "OnTheWay", "Delivered", "Cancelled" })
                cmb.Items.Add(s);
            cmb.SelectedItem = selected.Status;
            sp.Children.Add(cmb);

            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "Vërejtje (opsionale):", FontWeight = System.Windows.FontWeights.SemiBold, FontSize = 13, Margin = new System.Windows.Thickness(0, 0, 0, 6) });
            var txtNote = new System.Windows.Controls.TextBox { Height = 36, FontSize = 13, Padding = new System.Windows.Thickness(8, 0, 8, 0), VerticalContentAlignment = System.Windows.VerticalAlignment.Center, BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)), BorderThickness = new System.Windows.Thickness(1), Margin = new System.Windows.Thickness(0, 0, 0, 20) };
            sp.Children.Add(txtNote);

            var btnRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            var btnSave = MakeDialogButton("✓ Ruaj", "#0284C7", 100);
            var btnCancel = MakeDialogButton("✗ Anulo", "#64748B", 90, 10);
            btnCancel.Click += (_, __) => dlg.Close();
            btnSave.Click += (_, __) =>
            {
                try
                {
                    using var ctx = new Database.POSDbContext();
                    var order = ctx.DeliveryOrders.Find(selected.Id);
                    if (order == null) return;
                    order.Status = cmb.SelectedItem?.ToString() ?? order.Status;
                    if (order.Status == "Delivered") order.DeliveredAt = DateTime.Now;
                    order.UpdatedAt = DateTime.Now;
                    ctx.SaveChanges();
                    dlg.Close();
                    LoadData();
                    MessageBox.Show("Statusi u përditësua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Gabim:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error); }
            };
            btnRow.Children.Add(btnSave);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        private void AssignDriver_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveDeliveriesGrid.SelectedItem is not DeliveryOrder selected)
            {
                MessageBox.Show("Zgjidhni një porosi nga lista për të caktuar shoferin.",
                    "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var ctx = new Database.POSDbContext();
                var drivers = ctx.DeliveryDrivers.Where(d => d.IsActive && d.Status == "Available").OrderBy(d => d.Name).ToList();
                if (!drivers.Any())
                {
                    MessageBox.Show("Nuk ka shoferë të disponueshëm.", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dlg = new Window
                {
                    Title = $"🚗 Cakto Shoferin – Porosia #{selected.Id}",
                    Width = 360, Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this, ResizeMode = ResizeMode.NoResize,
                    Background = System.Windows.Media.Brushes.White
                };
                var sp = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24) };
                sp.Children.Add(new System.Windows.Controls.TextBlock { Text = "Zgjidh Shoferin:", FontWeight = System.Windows.FontWeights.SemiBold, FontSize = 13, Margin = new System.Windows.Thickness(0, 0, 0, 8) });
                var cmb = new System.Windows.Controls.ComboBox { Height = 36, FontSize = 13, Margin = new System.Windows.Thickness(0, 0, 0, 20) };
                foreach (var d in drivers)
                    cmb.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = $"{d.Name} ({d.VehicleType ?? "Makinë"})", Tag = d.Id });
                cmb.SelectedIndex = 0;
                sp.Children.Add(cmb);

                var btnRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
                var btnSave = MakeDialogButton("✓ Cakto", "#0284C7", 100);
                var btnCancel = MakeDialogButton("✗ Anulo", "#64748B", 90, 10);
                btnCancel.Click += (_, __) => dlg.Close();
                btnSave.Click += (_, __) =>
                {
                    if (cmb.SelectedItem is System.Windows.Controls.ComboBoxItem ci && ci.Tag is int driverId)
                    {
                        try
                        {
                            using var ctx2 = new Database.POSDbContext();
                            var order = ctx2.DeliveryOrders.Find(selected.Id);
                            var driver = ctx2.DeliveryDrivers.Find(driverId);
                            if (order != null && driver != null)
                            {
                                order.DriverId = driver.Id;
                                order.DriverName = driver.Name;
                                order.Status = order.Status == "Pending" ? "Preparing" : order.Status;
                                order.UpdatedAt = DateTime.Now;
                                driver.Status = "Busy";
                                driver.ActiveDeliveries++;
                                ctx2.SaveChanges();
                                dlg.Close();
                                LoadData();
                                MessageBox.Show($"Shoferi {driver.Name} u caktua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        catch (Exception ex) { MessageBox.Show($"Gabim:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error); }
                    }
                };
                btnRow.Children.Add(btnSave);
                btnRow.Children.Add(btnCancel);
                sp.Children.Add(btnRow);
                dlg.Content = sp;
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të shoferëve:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDeliveryOrderDialog(DeliveryOrder? existing)
        {
            bool isNew = existing == null;
            var dlg = new Window
            {
                Title = isNew ? "➕ Shto Porosi të Re Dërgese" : $"✏ Modifiko: Porosia #{existing!.Id}",
                Width = 500, Height = 580,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var scroll = new System.Windows.Controls.ScrollViewer { VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
            var sp = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24) };

            sp.Children.Add(MakeDialogLabel("Emri i Klientit *:"));
            var txtName = MakeDialogTextBox(existing?.CustomerName ?? "");
            sp.Children.Add(txtName);

            sp.Children.Add(MakeDialogLabel("Telefoni *:", 10));
            var txtPhone = MakeDialogTextBox(existing?.CustomerPhone ?? "");
            sp.Children.Add(txtPhone);

            sp.Children.Add(MakeDialogLabel("Adresa e Dërgesës *:", 10));
            var txtAddress = MakeDialogTextBox(existing?.DeliveryAddress ?? "");
            sp.Children.Add(txtAddress);

            sp.Children.Add(MakeDialogLabel("Shuma Totale (€) *:", 10));
            var txtAmount = MakeDialogTextBox(existing?.TotalAmount.ToString("F2") ?? "0.00");
            sp.Children.Add(txtAmount);

            sp.Children.Add(MakeDialogLabel("Taksa e Dërgesës (€):", 10));
            var txtFee = MakeDialogTextBox(existing?.DeliveryFee.ToString("F2") ?? "0.00");
            sp.Children.Add(txtFee);

            sp.Children.Add(MakeDialogLabel("Mënyra e Pagesës:", 10));
            var cmbPayment = new System.Windows.Controls.ComboBox { Height = 34, FontSize = 13, Margin = new System.Windows.Thickness(0, 4, 0, 0) };
            foreach (var p in new[] { "Cash", "Kartë", "Online", "PaKontakt" })
                cmbPayment.Items.Add(p);
            cmbPayment.SelectedIndex = 0;
            sp.Children.Add(cmbPayment);

            sp.Children.Add(MakeDialogLabel("Vërejtje:", 10));
            var txtNotes = MakeDialogTextBox(existing?.Notes ?? "");
            sp.Children.Add(txtNotes);

            var btnRow = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(0, 20, 0, 0)
            };
            var btnSave = MakeDialogButton("💾 Ruaj", "#22C55E", 110);
            var btnCancel = MakeDialogButton("✖ Anulo", "#64748B", 100, 10);
            btnCancel.Click += (_, __) => dlg.Close();
            btnSave.Click += (_, __) =>
            {
                foreach (var tb in new[] { txtName, txtPhone, txtAddress, txtAmount })
                {
                    tb.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                    tb.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                }
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    txtName.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtName.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Emri i Klientit' është e detyrueshme.", "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtName.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtPhone.Text))
                {
                    txtPhone.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtPhone.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Telefoni' është e detyrueshme.", "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPhone.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtAddress.Text))
                {
                    txtAddress.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtAddress.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Adresa e Dërgesës' është e detyrueshme.", "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtAddress.Focus();
                    return;
                }
                if (!decimal.TryParse(txtAmount.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0)
                {
                    txtAmount.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtAmount.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Shuma Totale' duhet të jetë numër pozitiv.", "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtAmount.Focus();
                    return;
                }
                decimal.TryParse(txtFee.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee);
                try
                {
                    using var ctx = new Database.POSDbContext();
                    if (isNew)
                    {
                        ctx.DeliveryOrders.Add(new DeliveryOrder
                        {
                            OrderNumber = $"DEL-{DateTime.Now:yyyyMMdd-HHmmss}",
                            ReceiptId = 0,
                            CustomerName = txtName.Text.Trim(),
                            CustomerPhone = txtPhone.Text.Trim(),
                            DeliveryAddress = txtAddress.Text.Trim(),
                            TotalAmount = amount,
                            DeliveryFee = fee,
                            Status = "Pending",
                            Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim(),
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            EstimatedDeliveryTime = 30
                        });
                    }
                    else
                    {
                        var order = ctx.DeliveryOrders.Find(existing!.Id);
                        if (order != null)
                        {
                            order.CustomerName = txtName.Text.Trim();
                            order.CustomerPhone = txtPhone.Text.Trim();
                            order.DeliveryAddress = txtAddress.Text.Trim();
                            order.TotalAmount = amount;
                            order.DeliveryFee = fee;
                            order.Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim();
                            order.UpdatedAt = DateTime.Now;
                        }
                    }
                    ctx.SaveChanges();

                    // Also send to kitchen if this is a new order
                    if (isNew)
                    {
                        try
                        {
                            using var kCtx = new Database.POSDbContext();
                            int nextNum = kCtx.KitchenOrders.Any() ? kCtx.KitchenOrders.Max(o => o.OrderNumber) + 1 : 1;
                            kCtx.KitchenOrders.Add(new Models.KitchenOrder
                            {
                                ReceiptId = 0,
                                OrderNumber = nextNum,
                                OrderType = "Delivery",
                                CustomerName = txtName.Text.Trim(),
                                OrderItems = string.IsNullOrWhiteSpace(txtNotes.Text) ? "Porosi Dërgese" : txtNotes.Text.Trim(),
                                Status = "New",
                                Priority = "Normal",
                                Station = "Kitchen",
                                ReceivedAt = DateTime.Now,
                                TargetTime = 30,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            });
                            kCtx.SaveChanges();
                        }
                        catch { /* Kitchen sync is non-fatal */ }
                    }

                    dlg.Close();
                    LoadData();
                    MessageBox.Show(isNew ? "Porosia u shtua me sukses dhe u dërgua te kuzhina!" : "Porosia u përditësua me sukses!",
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Gabim:\n{ex.Message}\n{ex.InnerException?.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error); }
            };
            btnRow.Children.Add(btnSave);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);
            scroll.Content = sp;
            dlg.Content = scroll;
            dlg.ShowDialog();
        }

        private void AddDriver_Click(object sender, RoutedEventArgs e)
        {
            ShowDriverDialog(null);
        }
        private void EditDriver_Click(object sender, RoutedEventArgs e)
        {
            if (DriversGrid.SelectedItem is DeliveryDriver selected)
            {
                ShowDriverDialog(selected);
            }
            else
            {
                MessageBox.Show("Zgjidhni shofer nga lista për të modifikuar.",
                    "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowDriverDialog(DeliveryDriver? existing)
        {
            bool isNew = existing == null;
            var dlg = new Window
            {
                Title = isNew ? "➕ Shto Shofer të Ri" : $"✏ Modifiko: {existing!.Name}",
                Width = 420, Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var sp = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24) };

            sp.Children.Add(MakeDialogLabel("Emri i Plotë *:"));
            var txtName = MakeDialogTextBox(existing?.Name ?? "");
            sp.Children.Add(txtName);

            sp.Children.Add(MakeDialogLabel("Telefoni *:", 10));
            var txtPhone = MakeDialogTextBox(existing?.Phone ?? "");
            sp.Children.Add(txtPhone);

            sp.Children.Add(MakeDialogLabel("Lloji i Automjetit:", 10));
            var cmbVehicle = new System.Windows.Controls.ComboBox { Height = 34, FontSize = 13, IsEditable = true, Margin = new System.Windows.Thickness(0, 4, 0, 0) };
            foreach (var v in new[] { "Makinë", "Motocikletë", "Biçikletë", "Furgon", "Kamion i vogël" })
                cmbVehicle.Items.Add(v);
            cmbVehicle.Text = existing?.VehicleType ?? "Makinë";
            sp.Children.Add(cmbVehicle);

            sp.Children.Add(MakeDialogLabel("Targa:", 10));
            var txtPlate = MakeDialogTextBox(existing?.VehiclePlate ?? "");
            sp.Children.Add(txtPlate);

            if (!isNew)
            {
                sp.Children.Add(MakeDialogLabel("Statusi:", 10));
                var cmbStatus = new System.Windows.Controls.ComboBox { Height = 34, FontSize = 13, Margin = new System.Windows.Thickness(0, 4, 0, 0) };
                foreach (var s in new[] { "Available", "Busy", "Offline" })
                    cmbStatus.Items.Add(s);
                cmbStatus.SelectedItem = existing!.Status;
                sp.Children.Add(cmbStatus);
            }

            var btnRow = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(0, 20, 0, 0)
            };
            var btnSave = MakeDialogButton("💾 Ruaj", "#0284C7", 110);
            var btnCancel = MakeDialogButton("✖ Anulo", "#64748B", 100, 10);
            btnCancel.Click += (_, __) => dlg.Close();
            btnSave.Click += (_, __) =>
            {
                txtName.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtName.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                txtPhone.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtPhone.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    txtName.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtName.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Emri i Plotë' është e detyrueshme.", "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtName.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtPhone.Text))
                {
                    txtPhone.BorderBrush = System.Windows.Media.Brushes.Red;
                    txtPhone.BorderThickness = new System.Windows.Thickness(2);
                    MessageBox.Show("Fusha 'Telefoni' është e detyrueshme.", "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPhone.Focus();
                    return;
                }
                try
                {
                    using var ctx = new Database.POSDbContext();
                    if (isNew)
                    {
                        ctx.DeliveryDrivers.Add(new DeliveryDriver
                        {
                            Name = txtName.Text.Trim(),
                            Phone = txtPhone.Text.Trim(),
                            VehicleType = cmbVehicle.Text?.Trim(),
                            VehiclePlate = string.IsNullOrWhiteSpace(txtPlate.Text) ? null : txtPlate.Text.Trim(),
                            Status = "Available",
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                    }
                    else
                    {
                        var driver = ctx.DeliveryDrivers.Find(existing!.Id);
                        if (driver != null)
                        {
                            driver.Name = txtName.Text.Trim();
                            driver.Phone = txtPhone.Text.Trim();
                            driver.VehicleType = cmbVehicle.Text?.Trim();
                            driver.VehiclePlate = string.IsNullOrWhiteSpace(txtPlate.Text) ? null : txtPlate.Text.Trim();
                            driver.UpdatedAt = DateTime.Now;
                        }
                    }
                    ctx.SaveChanges();
                    dlg.Close();
                    LoadData();
                    MessageBox.Show(isNew ? "Shoferi u shtua me sukses!" : "Shoferi u përditësua me sukses!",
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim:\n{ex.Message}\n{ex.InnerException?.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            btnRow.Children.Add(btnSave);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);
            dlg.Content = new System.Windows.Controls.ScrollViewer { Content = sp };
            dlg.ShowDialog();
        }

        private static System.Windows.Controls.TextBlock MakeDialogLabel(string text, double topMargin = 0) =>
            new System.Windows.Controls.TextBlock
            {
                Text = text, FontWeight = System.Windows.FontWeights.SemiBold,
                FontSize = 14, Margin = new System.Windows.Thickness(0, topMargin, 0, 4)
            };

        private static System.Windows.Controls.TextBox MakeDialogTextBox(string text = "") =>
            new System.Windows.Controls.TextBox
            {
                Height = 36, FontSize = 14, Text = text,
                Padding = new System.Windows.Thickness(10, 0, 10, 0),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                BorderThickness = new System.Windows.Thickness(1),
                Margin = new System.Windows.Thickness(0, 0, 0, 2)
            };

        private static System.Windows.Controls.Button MakeDialogButton(string text, string hex, double width, double leftMargin = 0)
        {
            var bg = (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
            var btn = new System.Windows.Controls.Button
            {
                Content = text, Width = width, Height = 36,
                Margin = new System.Windows.Thickness(leftMargin, 0, 0, 0),
                Foreground = System.Windows.Media.Brushes.White, FontSize = 13,
                FontWeight = System.Windows.FontWeights.SemiBold,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol"),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = bg
            };
            var borderFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new System.Windows.CornerRadius(8));
            borderFactory.SetBinding(System.Windows.Controls.Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var cpFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            cpFactory.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            cpFactory.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            borderFactory.AppendChild(cpFactory);
            var template = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button));
            template.VisualTree = borderFactory;
            btn.Template = template;
            return btn;
        }

        private void SearchHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fromDate = FromDate.SelectedDate ?? DateTime.Today;
                var toDate = (ToDate.SelectedDate ?? DateTime.Today).AddDays(1);

                using var context = new POSDbContext();
                _history = new ObservableCollection<DeliveryOrder>(
                    context.DeliveryOrders
                        .Where(d => d.CreatedAt >= fromDate && d.CreatedAt < toDate)
                        .OrderByDescending(d => d.CreatedAt)
                        .ToList()
                );
                HistoryGrid.ItemsSource = _history;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë kërkimit:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarkOutForDelivery_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is int id)
            {
                UpdateRowStatus(id, "OutForDelivery");
            }
        }

        private void MarkDelivered_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is int id)
            {
                UpdateRowStatus(id, "Delivered");
            }
        }

        private void UpdateRowStatus(int orderId, string newStatus)
        {
            try
            {
                using var ctx = new Database.POSDbContext();
                var order = ctx.DeliveryOrders.Find(orderId);
                if (order == null) return;
                order.Status = newStatus;
                if (newStatus == "Delivered") order.DeliveredAt = DateTime.Now;
                order.UpdatedAt = DateTime.Now;
                ctx.SaveChanges();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ndryshimit të statusit:\n{ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
