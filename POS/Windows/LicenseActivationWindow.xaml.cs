using System;
using System.Windows;
using KosovaPOS.Services;

namespace KosovaPOS.Windows
{
    public partial class LicenseActivationWindow : Window
    {
        public LicenseActivationWindow()
        {
            InitializeComponent();
            LoadMachineInfo();
        }

        private void LoadMachineInfo()
        {
            try
            {
                var machineId = LicenseService.GetMachineId();
                MachineIdTextBlock.Text = machineId;
                MachineIdCopyButton.Click += (s, e) =>
                {
                    System.Windows.Forms.Clipboard.SetText(machineId);
                    MessageBox.Show("Machine ID copied to clipboard.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading machine ID: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            var customerName = CustomerNameTextBox.Text.Trim();
            var licenseKey = LicenseKeyTextBox.Text.Trim();

            if (string.IsNullOrEmpty(customerName))
            {
                MessageBox.Show("Please enter your name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(licenseKey))
            {
                MessageBox.Show("Please enter the license key.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LicenseService.ActivateLicense(licenseKey, customerName))
            {
                MessageBox.Show("License activated successfully! The application will now restart.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
                Application.Current.Shutdown();
            }
            else
            {
                MessageBox.Show("License activation failed. Please check your license key and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
