using System;
using System.Windows;
using System.Windows.Input;

namespace KosovaPOS.Windows
{
    public partial class QuickPaymentWindow : Window
    {
        public decimal CashReceived { get; private set; }
        public decimal ChangeAmount { get; private set; }
        private readonly decimal _totalAmount;

        public QuickPaymentWindow(decimal totalAmount)
        {
            InitializeComponent();
            
            _totalAmount = totalAmount;
            TotalAmountText.Text = $"{totalAmount:F2} €";
            ChangeAmountText.Text = "0.00 €";
            
            // Focus on cash received textbox
            Loaded += (s, e) =>
            {
                CashReceivedTextBox.Focus();
            };
            
            // Set default to total amount
            CashReceivedTextBox.Text = totalAmount.ToString("F2");
            CashReceivedTextBox.SelectAll();
            
            // Listen to text changes for instant calculation
            CashReceivedTextBox.TextChanged += CashReceivedTextBox_TextChanged;
            
            // Calculate change immediately
            CalculateChange();
        }

        private void CashReceivedTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Ok_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void CashReceivedTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CalculateChange();
        }

        private void CalculateChange()
        {
            if (decimal.TryParse(CashReceivedTextBox.Text, out decimal cashReceived))
            {
                ChangeAmount = cashReceived - _totalAmount;
                ChangeAmountText.Text = $"{ChangeAmount:F2} €";
                
                // Change color and message based on whether it's positive or negative
                if (ChangeAmount < 0)
                {
                    // Customer hasn't given enough money
                    ChangeAmountText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(185, 28, 28)); // Red
                    ChangeBorder.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(254, 226, 226)); // Light red
                    ChangeMessageText.Text = $"⚠️ Klienti duhet të japë edhe {Math.Abs(ChangeAmount):F2} €";
                }
                else if (ChangeAmount > 0)
                {
                    // Customer gave extra money - show change to return
                    ChangeAmountText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(21, 128, 61)); // Green
                    ChangeBorder.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(187, 247, 208)); // Light green
                    ChangeMessageText.Text = $"✅ Ktheje klientit: {ChangeAmount:F2} €";
                }
                else
                {
                    // Exact amount
                    ChangeAmountText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(79, 70, 229)); // Primary
                    ChangeBorder.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(237, 233, 254)); // Light primary
                    ChangeMessageText.Text = "✓ Shuma e saktë - s'ka kusur";
                }
            }
            else
            {
                ChangeAmountText.Text = "0.00 €";
                ChangeAmount = 0;
                ChangeMessageText.Text = "Shkruani shumën që dha klienti";
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(CashReceivedTextBox.Text, out decimal cashReceived))
            {
                CashReceived = cashReceived;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Ju lutem vendosni një vlerë numerike!", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
                CashReceivedTextBox.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CashReceivedTextBox_TextChanged_1(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
