using System;
using System.Windows;
using KosovaPOS.Models;

namespace KosovaPOS.Windows
{
    public partial class ToppingEditWindow : Window
    {
        public PizzaTopping? Topping { get; private set; }
        
        public ToppingEditWindow(PizzaTopping? topping = null)
        {
            InitializeComponent();
            
            if (topping != null)
            {
                Topping = new PizzaTopping
                {
                    Id = topping.Id,
                    Name = topping.Name,
                    Description = topping.Description,
                    Price = topping.Price,
                    Category = topping.Category,
                    IsAvailable = topping.IsAvailable,
                    Icon = topping.Icon,
                    DisplayOrder = topping.DisplayOrder
                };
                
                Title = $"Modifiko Shtesën: {topping.Name}";
                LoadTopping();
            }
            else
            {
                Topping = new PizzaTopping();
                Title = "Shto Shtesë të Re";
            }
        }
        
        private void LoadTopping()
        {
            if (Topping == null) return;
            
            NameTextBox.Text = Topping.Name;
            DescriptionTextBox.Text = Topping.Description;
            PriceTextBox.Text = Topping.Price.ToString("F2");
            CategoryTextBox.Text = Topping.Category;
            IconTextBox.Text = Topping.Icon;
            DisplayOrderTextBox.Text = Topping.DisplayOrder.ToString();
            IsAvailableCheckBox.IsChecked = Topping.IsAvailable;
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            NameTextBox.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
            NameTextBox.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
            PriceTextBox.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
            PriceTextBox.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.BorderBrush = System.Windows.Media.Brushes.Red;
                NameTextBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Emri i Shtesës' është e detyrueshme.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            if (!decimal.TryParse(PriceTextBox.Text, out decimal price) || price < 0)
            {
                PriceTextBox.BorderBrush = System.Windows.Media.Brushes.Red;
                PriceTextBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Çmimi' duhet të jetë një numër pozitiv.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PriceTextBox.Focus();
                return;
            }
            
            if (Topping == null) Topping = new PizzaTopping();
            
            Topping.Name = NameTextBox.Text.Trim();
            Topping.Description = DescriptionTextBox.Text?.Trim();
            Topping.Price = price;
            Topping.Category = CategoryTextBox.Text?.Trim();
            Topping.Icon = IconTextBox.Text?.Trim();
            Topping.DisplayOrder = int.TryParse(DisplayOrderTextBox.Text, out int order) ? order : 0;
            Topping.IsAvailable = IsAvailableCheckBox.IsChecked == true;
            Topping.UpdatedAt = DateTime.Now;
            
            DialogResult = true;
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
