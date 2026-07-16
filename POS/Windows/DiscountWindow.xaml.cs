using System.Windows;

namespace KosovaPOS.Windows
{
    public partial class DiscountWindow : Window
    {
        public decimal DiscountPercent { get; private set; }
        public decimal DiscountAmount { get; private set; }
        public bool IsPercentageDiscount { get; private set; } = true;
        
        private readonly decimal _originalPrice;

        public DiscountWindow(string articleName, decimal originalPrice, decimal currentDiscount = 0)
        {
            InitializeComponent();
            
            _originalPrice = originalPrice;
            ArticleNameText.Text = articleName;
            OriginalPriceText.Text = $"{originalPrice:F2} €";
            
            // Set current discount if any
            if (currentDiscount > 0)
            {
                DiscountValueTextBox.Text = currentDiscount.ToString("F2");
            }
            
            UpdateFinalPrice();
            
            // Focus on discount textbox
            DiscountValueTextBox.Focus();
            DiscountValueTextBox.SelectAll();
        }

        private void DiscountType_Changed(object sender, RoutedEventArgs e)
        {
            if (PercentageRadio == null || AmountRadio == null || DiscountLabel == null) return;
            
            IsPercentageDiscount = PercentageRadio.IsChecked == true;
            DiscountLabel.Text = IsPercentageDiscount ? "Zbritje (%):" : "Zbritje (€):";
            
            // Clear the value when switching types
            DiscountValueTextBox.Text = "";
            UpdateFinalPrice();
        }

        private void DiscountValue_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateFinalPrice();
        }

        private void UpdateFinalPrice()
        {
            if (decimal.TryParse(DiscountValueTextBox.Text, out decimal discountValue))
            {
                decimal finalPrice;
                
                if (IsPercentageDiscount)
                {
                    // Validate discount percentage range
                    if (discountValue < 0)
                    {
                        discountValue = 0;
                        DiscountValueTextBox.Text = "0";
                    }
                    else if (discountValue > 100)
                    {
                        discountValue = 100;
                        DiscountValueTextBox.Text = "100";
                    }
                    
                    var discountAmountCalc = _originalPrice * (discountValue / 100);
                    finalPrice = _originalPrice - discountAmountCalc;
                }
                else
                {
                    // Amount discount
                    if (discountValue < 0)
                    {
                        discountValue = 0;
                        DiscountValueTextBox.Text = "0";
                    }
                    else if (discountValue > _originalPrice)
                    {
                        discountValue = _originalPrice;
                        DiscountValueTextBox.Text = _originalPrice.ToString("F2");
                    }
                    
                    finalPrice = _originalPrice - discountValue;
                }
                
                FinalPriceText.Text = $"{finalPrice:F2} €";
            }
            else
            {
                FinalPriceText.Text = $"{_originalPrice:F2} €";
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(DiscountValueTextBox.Text, out decimal discountValue))
            {
                if (IsPercentageDiscount)
                {
                    // Validate percentage range
                    if (discountValue < 0 || discountValue > 100)
                    {
                        MessageBox.Show("Zbritja duhet të jetë mes 0 dhe 100%.", 
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    DiscountPercent = discountValue;
                    DiscountAmount = _originalPrice * (discountValue / 100);
                }
                else
                {
                    // Validate amount range
                    if (discountValue < 0 || discountValue > _originalPrice)
                    {
                        MessageBox.Show($"Zbritja duhet të jetë mes 0 dhe {_originalPrice:F2} €.", 
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    DiscountAmount = discountValue;
                    // Calculate equivalent percentage for compatibility
                    DiscountPercent = _originalPrice > 0 ? (discountValue / _originalPrice) * 100 : 0;
                }
                
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Ju lutem vendosni një vlerë numerike për zbritjen.", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
                DiscountValueTextBox.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
