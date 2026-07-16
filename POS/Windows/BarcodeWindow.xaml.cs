using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KosovaPOS.Models;
using KosovaPOS.Services;

namespace KosovaPOS.Windows
{
    /// <summary>
    /// Print queue item for barcode printing
    /// </summary>
    public class PrintQueueItem
    {
        public Article Article { get; set; } = null!;
        public int Quantity { get; set; } = 1;
    }
    
    public partial class BarcodeWindow : Window
    {
        private List<Article> _allArticles = new List<Article>();
        private ObservableCollection<PrintQueueItem> _printQueue = new ObservableCollection<PrintQueueItem>();
        
        public BarcodeWindow()
        {
            InitializeComponent();
            PrintQueueListBox.ItemsSource = _printQueue;
            LoadArticles();
            LoadPrinterSettings();
        }
        
        private void LoadArticles()
        {
            try
            {
                _allArticles = ArticleDataService.GetAllArticles();
                ArticlesDataGrid.ItemsSource = _allArticles;
                UpdateResultCount(_allArticles.Count, _allArticles.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të artikujve: {ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LoadPrinterSettings()
        {
            var printerName = Environment.GetEnvironmentVariable("BARCODE_PRINTER") ?? "HPRT LPQ80";
            var labelWidth = Environment.GetEnvironmentVariable("BARCODE_LABEL_WIDTH_MM") ?? "55";
            var labelHeight = Environment.GetEnvironmentVariable("BARCODE_LABEL_HEIGHT_MM") ?? "25";
            
            PrinterNameText.Text = printerName;
            LabelSizeText.Text = $"{labelWidth}x{labelHeight}mm";
        }
        
        private void UpdateResultCount(int shown, int total)
        {
            ResultCountText.Text = shown == total 
                ? $"{total} artikuj" 
                : $"{shown} nga {total} artikuj";
        }
        
        private void UpdatePrintQueueCount()
        {
            var totalItems = _printQueue.Sum(q => q.Quantity);
            PrintQueueCountText.Text = $"{totalItems} etiketa";
        }
        
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text?.Trim().ToLower() ?? "";
            
            if (string.IsNullOrEmpty(searchText))
            {
                ArticlesDataGrid.ItemsSource = _allArticles;
                UpdateResultCount(_allArticles.Count, _allArticles.Count);
            }
            else
            {
                var filtered = _allArticles.Where(a =>
                    (a.Barcode?.ToLower().Contains(searchText) ?? false) ||
                    (a.Name?.ToLower().Contains(searchText) ?? false)).ToList();
                
                ArticlesDataGrid.ItemsSource = filtered;
                UpdateResultCount(filtered.Count, _allArticles.Count);
            }
        }
        
        private void ArticlesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Optional: Show preview of selected item
        }
        
        private void AddSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedArticles = ArticlesDataGrid.SelectedItems.Cast<Article>().ToList();
            
            if (!selectedArticles.Any())
            {
                MessageBox.Show("Ju lutem zgjidhni artikuj për të shtuar në radhë.", 
                    "Vërejtje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            int copies = 1;
            int.TryParse(CopiesTextBox.Text, out copies);
            if (copies < 1) copies = 1;
            
            foreach (var article in selectedArticles)
            {
                var existing = _printQueue.FirstOrDefault(q => q.Article.Id == article.Id);
                if (existing != null)
                {
                    existing.Quantity += copies;
                }
                else
                {
                    _printQueue.Add(new PrintQueueItem { Article = article, Quantity = copies });
                }
            }
            
            PrintQueueListBox.Items.Refresh();
            UpdatePrintQueueCount();
        }
        
        private void IncreasePrintQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PrintQueueItem item)
            {
                item.Quantity++;
                PrintQueueListBox.Items.Refresh();
                UpdatePrintQueueCount();
            }
        }
        
        private void DecreasePrintQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PrintQueueItem item)
            {
                item.Quantity--;
                if (item.Quantity <= 0)
                {
                    _printQueue.Remove(item);
                }
                PrintQueueListBox.Items.Refresh();
                UpdatePrintQueueCount();
            }
        }
        
        private void IncreaseCopiesButton_Click(object sender, RoutedEventArgs e)
        {
            int copies = 1;
            int.TryParse(CopiesTextBox.Text, out copies);
            CopiesTextBox.Text = (copies + 1).ToString();
        }
        
        private void DecreaseCopiesButton_Click(object sender, RoutedEventArgs e)
        {
            int copies = 1;
            int.TryParse(CopiesTextBox.Text, out copies);
            if (copies > 1)
            {
                CopiesTextBox.Text = (copies - 1).ToString();
            }
        }
        
        private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
        {
            _printQueue.Clear();
            UpdatePrintQueueCount();
        }
        
        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_printQueue.Any())
            {
                MessageBox.Show("Radha e printimit është bosh.", "Vërejtje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                int totalPrinted = 0;
                var printerService = new BarcodePrinterService();
                
                foreach (var item in _printQueue)
                {
                    // Print the quantity of barcodes for this item
                    printerService.PrintBarcode(item.Article, item.Quantity);
                    totalPrinted += item.Quantity;
                }
                
                MessageBox.Show($"Printimi u krye me sukses!\n\n{totalPrinted} etiketa u printuan.", 
                    "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _printQueue.Clear();
                UpdatePrintQueueCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit:\n\n{ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.F4 || (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.P))
            {
                PrintButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }
}
