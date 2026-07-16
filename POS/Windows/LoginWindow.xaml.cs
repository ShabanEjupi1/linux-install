using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KosovaPOS.Database;
using KosovaPOS.Models.BMDData;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace KosovaPOS.Windows
{
    public partial class LoginWindow : Window
    {
        public bool IsAuthenticated { get; private set; }
        public POSUser? AuthenticatedUser { get; private set; }
        
        private readonly string? _requiredPermission;
        
        public LoginWindow(string? requiredPermission = null)
        {
            InitializeComponent();
            _requiredPermission = requiredPermission;
            UsernameTextBox.Focus();
        }
        
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            TryLogin();
        }
        
        private void UsernameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Move to password field
                PasswordBox.Focus();
            }
        }
        
        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryLogin();
            }
        }
        
        private void TryLogin()
        {
            var username = UsernameTextBox.Text?.Trim();
            var password = PasswordBox.Password;
            
            if (string.IsNullOrEmpty(username))
            {
                ShowError("Ju lutem shkruani emrin e përdoruesit.");
                UsernameTextBox.Focus();
                return;
            }
            
            if (string.IsNullOrEmpty(password))
            {
                ShowError("Ju lutem shkruani fjalëkalimin.");
                PasswordBox.Focus();
                return;
            }
            
            try
            {
                using var context = new POSDbContext();
                
                // Try to find user by username
                var user = context.POSUsers
                    .AsNoTracking()
                    .FirstOrDefault(u => u.Username == username);
                
                if (user == null)
                {
                    ShowError("Përdoruesi nuk u gjet.");
                    UsernameTextBox.Focus();
                    return;
                }
                
                // Check password using BCrypt for hashed passwords, fallback to plain text for legacy
                bool passwordValid = false;
                try
                {
                    // Try BCrypt verification first (for properly hashed passwords)
                    passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                catch
                {
                    // Fallback to plain text comparison for legacy passwords
                    passwordValid = (user.PasswordHash == password);
                }
                
                if (!passwordValid)
                {
                    ShowError("Fjalëkalimi nuk është i saktë.");
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                    return;
                }
                
                // Check if user is active
                if (!user.IsActive)
                {
                    ShowError("Llogaria juaj është e çaktivizuar.");
                    return;
                }
                
                // Check required permission if specified
                if (!string.IsNullOrEmpty(_requiredPermission) && !HasPermission(user, _requiredPermission))
                {
                    ShowError("Nuk keni leje për këtë veprim.");
                    return;
                }
                
                IsAuthenticated = true;
                AuthenticatedUser = user;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë identifikimit: {ex.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private bool HasPermission(POSUser user, string permission)
        {
            // Admin role has all permissions
            if (user.Role == "Admin")
                return true;
            
            // Manager role has most permissions
            if (user.Role == "Manager" || user.Role == "Cashier")
            {
                // Allow common permissions for managers and cashiers
                if (permission.ToLower() == "settings" || permission.ToLower() == "articles")
                    return true;
            }
            
            return permission.ToLower() switch
            {
                "finance" => user.CanViewReports,
                "reports" => user.CanViewReports,
                "purchases" => user.CanManagePurchases,
                "articles" => user.CanManageArticles,
                "partners" => user.CanManagePurchases, // Partners tied to purchases
                "settings" => true, // Allow all authenticated users to access settings
                "users" => user.CanManageUsers,
                _ => false
            };
        }
        
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            ErrorBorder.Visibility = Visibility.Visible;
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsAuthenticated = false;
            DialogResult = false;
            Close();
        }
    }
}