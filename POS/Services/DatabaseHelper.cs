using System;
using System.Windows;
using KosovaPOS.Database;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Helper class for database operations with error handling
    /// </summary>
    public static class DatabaseHelper
    {
        /// <summary>
        /// Check if a specific table exists in the database
        /// </summary>
        public static bool TableExists(string tableName)
        {
            try
            {
                using var context = new POSDbContext();
                var query = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
                var result = context.Database.ExecuteSqlRaw(query);
                return result > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Show user-friendly error message when a table doesn't exist
        /// </summary>
        public static void ShowTableMissingError(string tableName, string feature)
        {
            MessageBox.Show(
                $"⚠️ Tabela '{tableName}' nuk ekziston në databazë!\n\n" +
                $"Ky funksion ({feature}) kërkon që databaza të jetë e inicializuar plotësisht.\n\n" +
                $"Zgjidhje:\n" +
                $"1. Mbyllni dhe rihapni aplikacionin (tabela duhet të krijohet automatikisht)\n" +
                $"2. Nëse problemi vazhdon, kontrolloni lidhjen me databazën\n" +
                $"3. Sigurohuni që SQL Server është duke punuar siç duhet\n\n" +
                $"Për më shumë informacion, kontrolloni skedarin 'startup_errors.log'",
                "Gabim në Databazë",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        /// <summary>
        /// Execute a database operation with error handling
        /// </summary>
        public static T ExecuteWithErrorHandling<T>(Func<T> operation, string operationName, T defaultValue = default)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                
                // Check for common SQL Server errors
                if (errorMessage.Contains("Invalid object name"))
                {
                    var tableName = ExtractTableName(errorMessage);
                    ShowTableMissingError(tableName, operationName);
                }
                else if (errorMessage.Contains("A network-related") || errorMessage.Contains("Cannot open database"))
                {
                    MessageBox.Show(
                        $"⚠️ Gabim në lidhjen me databazën!\n\n" +
                        $"Operacioni: {operationName}\n\n" +
                        $"Detajet: {errorMessage}\n\n" +
                        $"Sigurohuni që SQL Server është duke punuar dhe databaza është e aksesueshme.",
                        "Gabim në Lidhje",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(
                        $"Gabim gjatë: {operationName}\n\n{errorMessage}",
                        "Gabim",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                
                return defaultValue;
            }
        }

        /// <summary>
        /// Execute a database operation with error handling (void return)
        /// </summary>
        public static void ExecuteWithErrorHandling(Action operation, string operationName)
        {
            ExecuteWithErrorHandling<object>(() => 
            {
                operation();
                return null;
            }, operationName);
        }

        /// <summary>
        /// Extract table name from SQL error message
        /// </summary>
        private static string ExtractTableName(string errorMessage)
        {
            // Extract table name from "Invalid object name 'TableName'" error
            var startIndex = errorMessage.IndexOf("'");
            if (startIndex >= 0)
            {
                var endIndex = errorMessage.IndexOf("'", startIndex + 1);
                if (endIndex > startIndex)
                {
                    return errorMessage.Substring(startIndex + 1, endIndex - startIndex - 1);
                }
            }
            return "unknown";
        }
    }
}
