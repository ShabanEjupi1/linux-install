using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class PizzaManagementWindow : Window
    {
        private ObservableCollection<PizzaTopping> _toppings = new ObservableCollection<PizzaTopping>();
        private ObservableCollection<PizzaCategory> _categories = new ObservableCollection<PizzaCategory>();

        public PizzaManagementWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            LoadToppings();
            LoadCategories();
        }

        // Opens the full ArticlesWindow for pizza article management
        private void OpenArticlesWindow_Click(object sender, RoutedEventArgs e)
        {
            var win = new ArticlesWindow();
            win.Show();
        }

        private void LoadToppings()
        {
            try
            {
                using var context = new POSDbContext();
                _toppings = new ObservableCollection<PizzaTopping>(
                    context.PizzaToppings.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name).ToList()
                );
                ToppingsDataGrid.ItemsSource = _toppings;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të shtesave:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LoadCategories()
        {
            try
            {
                using var context = new POSDbContext();
                _categories = new ObservableCollection<PizzaCategory>(
                    context.PizzaCategories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList()
                );
                CategoriesDataGrid.ItemsSource = _categories;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të kategorive:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // Topping Management
        private void AddTopping_Click(object sender, RoutedEventArgs e)
        {
            var inputWindow = new ToppingEditWindow();
            if (inputWindow.ShowDialog() == true && inputWindow.Topping != null)
            {
                try
                {
                    using var context = new POSDbContext();
                    context.PizzaToppings.Add(inputWindow.Topping);
                    context.SaveChanges();
                    LoadToppings();
                    MessageBox.Show("Shtesa u shtua me sukses!", "Sukses", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë shtimit të shtesës:\n\n{ex.Message}", "Gabim", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void EditTopping_Click(object sender, RoutedEventArgs e)
        {
            if (ToppingsDataGrid.SelectedItem is PizzaTopping selectedTopping)
            {
                var editWindow = new ToppingEditWindow(selectedTopping);
                if (editWindow.ShowDialog() == true && editWindow.Topping != null)
                {
                    try
                    {
                        using var context = new POSDbContext();
                        context.PizzaToppings.Update(editWindow.Topping);
                        context.SaveChanges();
                        LoadToppings();
                        MessageBox.Show("Shtesa u modifikua me sukses!", "Sukses", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjatë modifikimit të shtesës:\n\n{ex.Message}", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Ju lutem zgjidhni një shtesë për ta modifikuar.", "Informacion", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void DeleteTopping_Click(object sender, RoutedEventArgs e)
        {
            if (ToppingsDataGrid.SelectedItem is PizzaTopping selectedTopping)
            {
                var result = MessageBox.Show(
                    $"A jeni i sigurt që doni të fshini shtesën '{selectedTopping.Name}'?", 
                    "Konfirmimi", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var context = new POSDbContext();
                        context.PizzaToppings.Remove(selectedTopping);
                        context.SaveChanges();
                        LoadToppings();
                        MessageBox.Show("Shtesa u fshi me sukses!", "Sukses", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjatë fshirjes së shtesës:\n\n{ex.Message}", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Ju lutem zgjidhni një shtesë për ta fshirë.", "Informacion", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        // Category Management
        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            var inputWindow = new CategoryEditWindow();
            if (inputWindow.ShowDialog() == true && inputWindow.Category != null)
            {
                try
                {
                    using var context = new POSDbContext();
                    context.PizzaCategories.Add(inputWindow.Category);
                    context.SaveChanges();
                    LoadCategories();
                    MessageBox.Show("Kategoria u shtua me sukses!", "Sukses", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë shtimit të kategorisë:\n\n{ex.Message}", "Gabim", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesDataGrid.SelectedItem is PizzaCategory selectedCategory)
            {
                var editWindow = new CategoryEditWindow(selectedCategory);
                if (editWindow.ShowDialog() == true && editWindow.Category != null)
                {
                    try
                    {
                        using var context = new POSDbContext();
                        context.PizzaCategories.Update(editWindow.Category);
                        context.SaveChanges();
                        LoadCategories();
                        MessageBox.Show("Kategoria u modifikua me sukses!", "Sukses", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjatë modifikimit të kategorisë:\n\n{ex.Message}", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Ju lutem zgjidhni një kategori për ta modifikuar.", "Informacion", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesDataGrid.SelectedItem is PizzaCategory selectedCategory)
            {
                var result = MessageBox.Show(
                    $"A jeni i sigurt që doni të fshini kategorinë '{selectedCategory.Name}'?", 
                    "Konfirmimi", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var context = new POSDbContext();
                        context.PizzaCategories.Remove(selectedCategory);
                        context.SaveChanges();
                        LoadCategories();
                        MessageBox.Show("Kategoria u fshi me sukses!", "Sukses", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gabim gjatë fshirjes së kategorisë:\n\n{ex.Message}", "Gabim", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Ju lutem zgjidhni një kategori për ta fshirë.", "Informacion", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
