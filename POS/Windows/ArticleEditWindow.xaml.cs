using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    /// <summary>
    /// Window for adding or editing articles in the POS system.
    /// </summary>
    public partial class ArticleEditWindow : Window
    {
        private Article? _article;
        private readonly bool _isNewArticle;

        public ArticleEditWindow(Article? article)
        {
            InitializeComponent();

            _isNewArticle = article == null;

            if (_isNewArticle)
            {
                _article = new Article
                {
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                HeaderTitle.Text = "&#x2795; Shto Artikull të Ri";
                HeaderSubtitle.Text = "Plotëso të dhënat e nevojshme dhe ruaj artikullin";
                Title = "Shto Artikull të Ri";
            }
            else
            {
                // Use passed article directly for editing (already loaded from database)
                _article = article!;

                if (_article == null)
                {
                    MessageBox.Show("Artikulli nuk u gjet në databazë!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    Loaded += (s, e) => Close();
                    return;
                }

                HeaderTitle.Text = "&#x1F4DD; Ndrysho Artikullin";
                HeaderSubtitle.Text = $"ID: {_article.Id} | Barkoda: {_article.Barcode}";
                Title = $"Ndrysho: {_article.Name}";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isNewArticle && _article != null)
            {
                PopulateForm();
            }
            
            // Focus on barcode field for new articles (for scanning)
            txtBarcode.Focus();
            txtBarcode.SelectAll();
        }

        private void PopulateForm()
        {
            if (_article == null) return;

            txtBarcode.Text = _article.Barcode ?? "";
            txtName.Text = _article.Name ?? "";
            txtSalesPrice.Text = _article.SalesPrice.ToString("F2", CultureInfo.InvariantCulture);
            txtPurchasePrice.Text = _article.PurchasePrice.ToString("F2", CultureInfo.InvariantCulture);
            txtPack.Text = _article.Pack.ToString("F0", CultureInfo.InvariantCulture);
            txtStockQuantity.Text = _article.StockQuantity.ToString("F0", CultureInfo.InvariantCulture);
            txtMinimumStock.Text = _article.MinimumStock.ToString("F0", CultureInfo.InvariantCulture);
            txtSupplier.Text = _article.Supplier ?? "";
            txtBrand.Text = _article.Brand ?? "";
            txtSize.Text = _article.Size ?? "";
            txtColor.Text = _article.Color ?? "";

            // Set Unit ComboBox
            SetComboBoxValue(cmbUnit, _article.Unit, "Copë");

            // Set Category ComboBox
            SetComboBoxValue(cmbCategory, _article.Category, "");

            // Set VAT Rate ComboBox
            cmbVATRate.SelectedIndex = _article.VATRate == 8 ? 1 : 0;

            // Set IsActive checkbox
            chkIsActive.IsChecked = _article.IsActive;
        }

        private void SetComboBoxValue(ComboBox comboBox, string? value, string defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                comboBox.Text = defaultValue;
                return;
            }

            // Try to find matching item
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content?.ToString() == value)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            // If not found, set text directly (for editable comboboxes)
            comboBox.Text = value;
        }

        private string GetComboBoxValue(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content?.ToString() ?? "";
            }
            return comboBox.Text?.Trim() ?? "";
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S to save
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                btnSave_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Escape to cancel
            if (e.Key == Key.Escape)
            {
                btnCancel_Click(sender, e);
                e.Handled = true;
                return;
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only numbers, decimal point, and comma
            var regex = new Regex(@"^[0-9.,]+$");
            e.Handled = !regex.IsMatch(e.Text);
        }

        private void BarcodeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only numbers for barcode
            var regex = new Regex(@"^[0-9]+$");
            e.Handled = !regex.IsMatch(e.Text);
        }

        private decimal ParseDecimal(string text, decimal defaultValue = 0)
        {
            if (string.IsNullOrWhiteSpace(text))
                return defaultValue;

            // Replace comma with dot for parsing
            text = text.Replace(",", ".");

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;

            return defaultValue;
        }

        private bool ValidateForm()
        {
            // Validate barcode
            if (string.IsNullOrWhiteSpace(txtBarcode.Text))
            {
                ShowValidationError("Barkoda është e detyrueshme!", txtBarcode);
                return false;
            }

            // Validate name
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                ShowValidationError("Emri i artikullit është i detyrueshëm!", txtName);
                return false;
            }

            // Validate sales price
            var salesPrice = ParseDecimal(txtSalesPrice.Text, -1);
            if (salesPrice < 0)
            {
                ShowValidationError("Çmimi i shitjes duhet të jetë numër pozitiv!", txtSalesPrice);
                return false;
            }

            // Check for duplicate barcode using ArticleDataService
            string newBarcode = txtBarcode.Text.Trim();
            var existingArticle = ArticleDataService.FindByBarcode(newBarcode);

            if (existingArticle != null)
            {
                // If editing, allow same barcode for the same article
                if (_isNewArticle || existingArticle.Id != _article!.Id)
                {
                    var result = MessageBox.Show(
                        $"Barkoda '{newBarcode}' ekziston tashmë për artikullin:\n\n" +
                        $"'{existingArticle.Name}'\n\n" +
                        $"A doni të vazhdoni gjithsesi?",
                        "Barkoda ekziston",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        txtBarcode.Focus();
                        txtBarcode.SelectAll();
                        return false;
                    }
                }
            }

            return true;
        }

        private void ShowValidationError(string message, TextBox focusControl)
        {
            focusControl.BorderBrush = System.Windows.Media.Brushes.Red;
            focusControl.BorderThickness = new Thickness(2);
            MessageBox.Show(message, "Validim", MessageBoxButton.OK, MessageBoxImage.Warning);
            focusControl.Focus();
            focusControl.SelectAll();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            try
            {
                // Update article properties from form
                _article!.Barcode = txtBarcode.Text.Trim();
                _article.Name = txtName.Text.Trim();
                _article.SalesPrice = ParseDecimal(txtSalesPrice.Text);
                _article.PurchasePrice = ParseDecimal(txtPurchasePrice.Text);
                _article.Pack = ParseDecimal(txtPack.Text, 1);
                _article.StockQuantity = ParseDecimal(txtStockQuantity.Text);
                _article.MinimumStock = ParseDecimal(txtMinimumStock.Text);
                _article.Supplier = txtSupplier.Text.Trim();
                _article.Brand = txtBrand.Text.Trim();
                _article.Size = txtSize.Text.Trim();
                _article.Color = txtColor.Text.Trim();
                _article.Unit = GetComboBoxValue(cmbUnit);
                _article.Category = GetComboBoxValue(cmbCategory);
                _article.VATRate = cmbVATRate.SelectedIndex == 1 ? 8 : 18;
                _article.IsActive = chkIsActive.IsChecked == true;
                _article.UpdatedAt = DateTime.Now;

                // Use ArticleDataService to save (handles both SQLite and SQL Server)
                var success = ArticleDataService.SaveArticle(_article);
                
                if (!success)
                {
                    MessageBox.Show("Gabim gjatë ruajtjes së artikullit!", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Gabim gjatë ruajtjes së artikullit:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "Gabim",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
