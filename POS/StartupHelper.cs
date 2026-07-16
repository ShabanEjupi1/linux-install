using System;
using System.IO;
using System.Windows;
using KosovaPOS.Services;

namespace KosovaPOS
{
    public class StartupHelper
    {
        public static bool CheckAndInitialize()
        {
            try
            {
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var logPath = Path.Combine(appDirectory, "startup_errors.log");

                Log("=== Startup Check Begin ===", logPath);

                // 1. Check License
                Log("Checking license...", logPath);
                if (!LicenseService.ValidateLicense())
                {
                    Log("License invalid or not found. Showing activation window.", logPath);
                    return false;
                }

                var daysUntilExpiration = LicenseService.GetDaysUntilExpiration();
                Log($"License valid. Days until expiration: {daysUntilExpiration}", logPath);

                if (daysUntilExpiration < 30)
                {
                    MessageBox.Show(
                        $"Your license will expire in {daysUntilExpiration} days. Please renew soon.",
                        "License Expiration Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }

                // 2. Check and Initialize Database
                Log("Initializing database...", logPath);
                if (!POSDbContext.ValidateConnection())
                {
                    Log($"Database connection failed: {POSDbContext.LastConnectionError}", logPath);
                    MessageBox.Show(
                        $"Unable to connect to database. Please ensure SQL Server is installed and running.\n\nError: {POSDbContext.LastConnectionError}",
                        "Database Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return false;
                }

                Log("Creating/updating database schema...", logPath);
                if (!DatabaseService.EnsureDatabaseInitialized())
                {
                    Log("Database initialization failed.", logPath);
                    MessageBox.Show(
                        "Unable to initialize database. Please check the startup log for details.",
                        "Database Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return false;
                }

                Log("=== Startup Check Success ===", logPath);
                return true;
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_errors.log");
                Log($"STARTUP ERROR: {ex.Message}\n{ex.StackTrace}", logPath);
                
                MessageBox.Show(
                    $"An error occurred during startup:\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
        }

        private static void Log(string message, string logPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? "");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }
}
