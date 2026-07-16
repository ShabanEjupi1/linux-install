using System;
using System.Linq;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;

namespace KosovaPOS.Windows
{
    public partial class BusinessPartnerEditWindow : Window
    {
        private BusinessPartner? _partner;
        private System.Collections.Generic.List<Qytetet>? _cities;
        
        public BusinessPartnerEditWindow(BusinessPartner? partner = null)
        {
            InitializeComponent();
            _partner = partner;
            
            PartnerTypeComboBox.Items.Add("Klient");
            PartnerTypeComboBox.Items.Add("Furnizues");
            PartnerTypeComboBox.Items.Add("Të dy");
            PartnerTypeComboBox.SelectedIndex = 0;
            
            LoadCities();
            
            if (_partner != null)
            {
                Title = "Ndrysho partnerin";
                LoadPartnerData();
            }
        }
        
        private void LoadCities()
        {
            try
            {
                using var context = new POSDbContext();
                _cities = context.Qytetet.OrderBy(q => q.Emertimi).ToList();
                CityComboBox.ItemsSource = _cities;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cities: {ex.Message}");
            }
        }
        
        private void LoadPartnerData()
        {
            if (_partner == null) return;
            
            NameTextBox.Text = _partner.Name;
            NRFTextBox.Text = _partner.NRF;
            NUITextBox.Text = _partner.NUI;
            AddressTextBox.Text = _partner.Address;
            
            // Set city by ID if it's a number, otherwise by text
            if (int.TryParse(_partner.City, out int cityId))
            {
                CityComboBox.SelectedValue = cityId;
            }
            else if (!string.IsNullOrEmpty(_partner.City))
            {
                // Try to find city by name
                var city = _cities?.FirstOrDefault(c => c.Emertimi?.Equals(_partner.City, StringComparison.OrdinalIgnoreCase) == true);
                if (city != null)
                {
                    CityComboBox.SelectedItem = city;
                }
                else
                {
                    CityComboBox.Text = _partner.City;
                }
            }
            
            PhoneTextBox.Text = _partner.Phone;
            EmailTextBox.Text = _partner.Email;
            PartnerTypeComboBox.Text = _partner.PartnerType;
            IsActiveCheckBox.IsChecked = _partner.IsActive;
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            NameTextBox.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
            NameTextBox.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
            PartnerTypeComboBox.ClearValue(System.Windows.Controls.ComboBox.BorderBrushProperty);
            PartnerTypeComboBox.ClearValue(System.Windows.Controls.ComboBox.BorderThicknessProperty);

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.BorderBrush = System.Windows.Media.Brushes.Red;
                NameTextBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Emri' është e detyrueshme.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(PartnerTypeComboBox.Text))
            {
                PartnerTypeComboBox.BorderBrush = System.Windows.Media.Brushes.Red;
                PartnerTypeComboBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Lloji i partnerit' është e detyrueshme.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PartnerTypeComboBox.Focus();
                return;
            }
            
            try
            {
                using var context = new POSDbContext();

                FurnitoriNew furnitoriRecord;

                if (_partner != null && _partner.Id > 0)
                {
                    furnitoriRecord = context.FurnitoriNew.Find((long)_partner.Id);
                    if (furnitoriRecord == null)
                    {
                        MessageBox.Show("Partneri nuk u gjet!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    furnitoriRecord = new FurnitoriNew();
                    context.FurnitoriNew.Add(furnitoriRecord);
                }

                furnitoriRecord.Emri = NameTextBox.Text;
                furnitoriRecord.NRF = NRFTextBox.Text;
                furnitoriRecord.NIT = NUITextBox.Text;
                furnitoriRecord.Adresa = AddressTextBox.Text;

                if (CityComboBox.SelectedValue is int selectedCityId)
                {
                    furnitoriRecord.Qyteti = selectedCityId;
                }
                else if (!string.IsNullOrWhiteSpace(CityComboBox.Text))
                {
                    var city = _cities?.FirstOrDefault(c => c.Emertimi?.Equals(CityComboBox.Text, StringComparison.OrdinalIgnoreCase) == true);
                    furnitoriRecord.Qyteti = city?.ID;
                }

                furnitoriRecord.Telefoni = PhoneTextBox.Text;
                furnitoriRecord.Email = EmailTextBox.Text;
                furnitoriRecord.Data = DateTime.Now.ToString("yyyy-MM-dd");

                var partnerType = PartnerTypeComboBox.Text;
                furnitoriRecord.F = partnerType == "Furnizues" || partnerType == "Të dy";
                furnitoriRecord.K = partnerType == "Klient" || partnerType == "Të dy";

                context.SaveChanges();

                MessageBox.Show("Partneri u ruajt me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
