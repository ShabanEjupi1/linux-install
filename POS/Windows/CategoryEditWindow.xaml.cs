using System;
using System.Windows;
using KosovaPOS.Models;

namespace KosovaPOS.Windows
{
    public partial class CategoryEditWindow : Window
    {
        public PizzaCategory? Category { get; private set; }
        
        public CategoryEditWindow(PizzaCategory? category = null)
        {
            InitializeComponent();
            
            if (category != null)
            {
                Category = new PizzaCategory
                {
                    Id = category.Id,
                    Name = category.Name,
                    Description = category.Description,
                    Icon = category.Icon,
                    DisplayOrder = category.DisplayOrder,
                    IsActive = category.IsActive
                };
                
                Title = $"Modifiko Kategorinë: {category.Name}";
                LoadCategory();
            }
            else
            {
                Category = new PizzaCategory();
                Title = "Shto Kategori të Re";
            }
        }
        
        private void LoadCategory()
        {
            if (Category == null) return;
            
            NameTextBox.Text = Category.Name;
            DescriptionTextBox.Text = Category.Description;
            IconTextBox.Text = Category.Icon;
            DisplayOrderTextBox.Text = Category.DisplayOrder.ToString();
            IsActiveCheckBox.IsChecked = Category.IsActive;
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            NameTextBox.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
            NameTextBox.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.BorderBrush = System.Windows.Media.Brushes.Red;
                NameTextBox.BorderThickness = new Thickness(2);
                MessageBox.Show("Fusha 'Emri i Kategorisë' është e detyrueshme.", "Validim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }
            
            if (Category == null) Category = new PizzaCategory();
            
            Category.Name = NameTextBox.Text.Trim();
            Category.Description = DescriptionTextBox.Text?.Trim();
            Category.Icon = IconTextBox.Text?.Trim();
            Category.DisplayOrder = int.TryParse(DisplayOrderTextBox.Text, out int order) ? order : 0;
            Category.IsActive = IsActiveCheckBox.IsChecked == true;
            Category.UpdatedAt = DateTime.Now;
            
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
