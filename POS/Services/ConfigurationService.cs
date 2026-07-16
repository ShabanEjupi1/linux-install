using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Service for managing application configuration settings
    /// </summary>
    public static class ConfigurationService
    {
        private static Dictionary<string, string> _settings = new Dictionary<string, string>();
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        private static bool _isLoaded = false;

        // Default configuration values
        private const string DEFAULT_BUSINESS_NAME = "Kosovo POS";
        private const string DEFAULT_BUSINESS_ADDRESS = "Prishtinë, Kosovë";
        private const string DEFAULT_BUSINESS_PHONE = "+383 44 000 000";
        private const string DEFAULT_BUSINESS_EMAIL = "info@kosovapos.com";
        private const string DEFAULT_TAX_NUMBER = "600000000";
        private const string DEFAULT_BUSINESS_NUMBER = "810000000";
        private const string DEFAULT_CURRENCY = "EUR";
        private const string DEFAULT_CURRENCY_SYMBOL = "€";

        /// <summary>
        /// Load configuration from appsettings.json
        /// </summary>
        public static void LoadConfiguration()
        {
            if (_isLoaded) return;

            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                                ?? new Dictionary<string, string>();
                }
                else
                {
                    // Create default configuration
                    _settings = GetDefaultSettings();
                    SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
                _settings = GetDefaultSettings();
            }

            _isLoaded = true;
        }

        /// <summary>
        /// Save configuration to appsettings.json
        /// </summary>
        public static void SaveConfiguration()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a configuration value
        /// </summary>
        public static string GetValue(string key, string defaultValue = "")
        {
            if (!_isLoaded) LoadConfiguration();
            return _settings.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Set a configuration value
        /// </summary>
        public static void SetValue(string key, string value)
        {
            if (!_isLoaded) LoadConfiguration();
            _settings[key] = value;
        }

        /// <summary>
        /// Get default configuration settings
        /// </summary>
        private static Dictionary<string, string> GetDefaultSettings()
        {
            return new Dictionary<string, string>
            {
                { "BusinessName", DEFAULT_BUSINESS_NAME },
                { "BusinessAddress", DEFAULT_BUSINESS_ADDRESS },
                { "BusinessPhone", DEFAULT_BUSINESS_PHONE },
                { "BusinessEmail", DEFAULT_BUSINESS_EMAIL },
                { "TaxNumber", DEFAULT_TAX_NUMBER },
                { "BusinessNumber", DEFAULT_BUSINESS_NUMBER },
                { "Currency", DEFAULT_CURRENCY },
                { "CurrencySymbol", DEFAULT_CURRENCY_SYMBOL },
                { "Language", "sq" }, // Albanian
                { "Theme", "Light" },
                { "AutoBackup", "true" },
                { "BackupFrequency", "Daily" },
                { "ReceiptFooterMessage", "Faleminderit për blerjen!" },
                { "EnableLoyaltyProgram", "false" },
                { "TaxRate", "18" },
                { "EnableInventoryTracking", "true" },
                { "LowStockAlertEnabled", "true" },
                { "DefaultPaymentMethod", "Cash" }
            };
        }

        // Convenience properties for common settings
        public static string BusinessName
        {
            get => GetValue("BusinessName", DEFAULT_BUSINESS_NAME);
            set { SetValue("BusinessName", value); SaveConfiguration(); }
        }

        public static string BusinessAddress
        {
            get => GetValue("BusinessAddress", DEFAULT_BUSINESS_ADDRESS);
            set { SetValue("BusinessAddress", value); SaveConfiguration(); }
        }

        public static string BusinessPhone
        {
            get => GetValue("BusinessPhone", DEFAULT_BUSINESS_PHONE);
            set { SetValue("BusinessPhone", value); SaveConfiguration(); }
        }

        public static string BusinessEmail
        {
            get => GetValue("BusinessEmail", DEFAULT_BUSINESS_EMAIL);
            set { SetValue("BusinessEmail", value); SaveConfiguration(); }
        }

        public static string TaxNumber
        {
            get => GetValue("TaxNumber", DEFAULT_TAX_NUMBER);
            set { SetValue("TaxNumber", value); SaveConfiguration(); }
        }

        public static string BusinessNumber
        {
            get => GetValue("BusinessNumber", DEFAULT_BUSINESS_NUMBER);
            set { SetValue("BusinessNumber", value); SaveConfiguration(); }
        }

        public static string Currency
        {
            get => GetValue("Currency", DEFAULT_CURRENCY);
            set { SetValue("Currency", value); SaveConfiguration(); }
        }

        public static string CurrencySymbol
        {
            get => GetValue("CurrencySymbol", DEFAULT_CURRENCY_SYMBOL);
            set { SetValue("CurrencySymbol", value); SaveConfiguration(); }
        }

        public static string ReceiptFooterMessage
        {
            get => GetValue("ReceiptFooterMessage", "Faleminderit për blerjen!");
            set { SetValue("ReceiptFooterMessage", value); SaveConfiguration(); }
        }

        public static bool EnableInventoryTracking
        {
            get => GetValue("EnableInventoryTracking", "true").ToLower() == "true";
            set { SetValue("EnableInventoryTracking", value.ToString()); SaveConfiguration(); }
        }

        public static bool LowStockAlertEnabled
        {
            get => GetValue("LowStockAlertEnabled", "true").ToLower() == "true";
            set { SetValue("LowStockAlertEnabled", value.ToString()); SaveConfiguration(); }
        }

        public static int TaxRate
        {
            get => int.TryParse(GetValue("TaxRate", "18"), out int rate) ? rate : 18;
            set { SetValue("TaxRate", value.ToString()); SaveConfiguration(); }
        }
    }
}
