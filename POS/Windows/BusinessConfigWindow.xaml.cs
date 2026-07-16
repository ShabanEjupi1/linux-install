using System;
using System.Windows;
using KosovaPOS.Services;

namespace KosovaPOS.Windows
{
    public partial class BusinessConfigWindow : Window
    {
        public BusinessConfigWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                txtBusinessName.Text = ConfigurationService.BusinessName;
                txtBusinessAddress.Text = ConfigurationService.BusinessAddress;
                txtBusinessPhone.Text = ConfigurationService.BusinessPhone;
                txtBusinessEmail.Text = ConfigurationService.BusinessEmail;
                txtTaxNumber.Text = ConfigurationService.TaxNumber;
                txtBusinessNumber.Text = ConfigurationService.BusinessNumber;
                txtCurrency.Text = ConfigurationService.Currency;
                txtTaxRate.Text = ConfigurationService.TaxRate.ToString();
                txtReceiptFooter.Text = ConfigurationService.ReceiptFooterMessage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të konfigurimeve:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(txtBusinessName.Text))
                {
                    MessageBox.Show("Emri i biznesit është i detyrueshëm!", 
                        "Validimi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtBusinessName.Focus();
                    return;
                }

                // Validate tax rate
                if (!int.TryParse(txtTaxRate.Text, out int taxRate) || taxRate < 0 || taxRate > 100)
                {
                    MessageBox.Show("Norma e TVSH-së duhet të jetë numër ndërmjet 0 dhe 100!", 
                        "Validimi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtTaxRate.Focus();
                    return;
                }

                // Save settings
                ConfigurationService.BusinessName = txtBusinessName.Text.Trim();
                ConfigurationService.BusinessAddress = txtBusinessAddress.Text.Trim();
                ConfigurationService.BusinessPhone = txtBusinessPhone.Text.Trim();
                ConfigurationService.BusinessEmail = txtBusinessEmail.Text.Trim();
                ConfigurationService.TaxNumber = txtTaxNumber.Text.Trim();
                ConfigurationService.BusinessNumber = txtBusinessNumber.Text.Trim();
                ConfigurationService.Currency = txtCurrency.Text.Trim();
                ConfigurationService.TaxRate = taxRate;
                ConfigurationService.ReceiptFooterMessage = txtReceiptFooter.Text.Trim();

                MessageBox.Show("Konfigurimet u ruajtën me sukses!", 
                    "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes së konfigurimeve:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
