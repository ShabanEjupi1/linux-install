using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using DotNetEnv;
using KosovaPOS.Database;
using KosovaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS
{
    public partial class App : Application
    {
        private static string _logPath = "";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Check license before anything else
            if (!LicenseService.ValidateLicense())
            {
                // Show license activation window
                var licenseWindow = new Windows.LicenseActivationWindow();
                licenseWindow.Show();
                base.OnStartup(e);
                return;
            }

            base.OnStartup(e);
            
            // Use application directory for log file
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _logPath = System.IO.Path.Combine(appDirectory, "startup_errors.log");
            var logPath = _logPath;
            
            // Log initial startup
            try
            {
                System.IO.File.AppendAllText(logPath, $"\n\n========== [{DateTime.Now}] Application Starting ==========\n");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Base Directory: {appDirectory}\n");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Current Directory: {Environment.CurrentDirectory}\n");
            }
            catch (Exception logEx)
            {
                MessageBox.Show($"Cannot write to log file at {logPath}\nError: {logEx.Message}\n\nApp will continue but errors may not be logged.", 
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            
            // Global exception handlers - suppress vcruntime140 errors during cleanup
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = (Exception)args.ExceptionObject;
                
                // Suppress vcruntime140 DLL errors during app exit (known issue with native libs)
                if (ex.Message.Contains("vcruntime140") || ex.Message.Contains("_scrt_uninitialize") ||
                    ex.Message.Contains("type_info_destroy_list") || ex.Message.Contains("ModuleUninitializer"))
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Suppressed exit cleanup error (non-fatal): {ex.Message}\n");
                    return;
                }
                
                var errorMsg = $"Gabim fatal:\n{ex.Message}\n\n{ex.StackTrace}";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] FATAL: {errorMsg}\n\n");
                MessageBox.Show(errorMsg, "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            
            DispatcherUnhandledException += (s, args) =>
            {
                // Suppress vcruntime140 DLL errors during app exit
                if (args.Exception.Message.Contains("vcruntime140") || args.Exception.Message.Contains("_scrt_uninitialize") ||
                    args.Exception.Message.Contains("type_info_destroy_list") || args.Exception.Message.Contains("ModuleUninitializer"))
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Suppressed exit cleanup error (non-fatal): {args.Exception.Message}\n");
                    args.Handled = true;
                    return;
                }
                
                var errorMsg = $"Gabim:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] DISPATCHER: {errorMsg}\n\n");
                MessageBox.Show(errorMsg, "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
                Shutdown();
            };
            
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Loading resources and initializing...\n");
                
                // Verify resource dictionaries are loaded
                try
                {
                    var resourceCount = Application.Current.Resources.MergedDictionaries.Count;
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Loaded {resourceCount} merged resource dictionaries\n");
                    
                    if (resourceCount == 0)
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] WARNING: No resource dictionaries loaded! UI may not display correctly.\n");
                    }
                }
                catch (Exception resEx)
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] ERROR checking resources: {resEx.Message}\n");
                    throw new Exception($"Failed to load application resources. Please ensure all XAML resource files are present in the Resources folder.\n\nDetails: {resEx.Message}", resEx);
                }
                
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Starting application...\n");
                
                // Load environment variables from application directory (not current working directory)
                try
                {
                    var envPath = System.IO.Path.Combine(appDirectory, ".env");
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Looking for .env at: {envPath}\n");
                    
                    if (System.IO.File.Exists(envPath))
                    {
                        Env.Load(envPath);
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Environment variables loaded from: {envPath}\n");
                        
                        // Log FISCAL_ENABLED value for debugging
                        var fiscalEnabled = Environment.GetEnvironmentVariable("FISCAL_ENABLED");
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] FISCAL_ENABLED = {fiscalEnabled ?? "null"}\n");
                        
                        // Log SQL Server configuration
                        var useSqlServer = Environment.GetEnvironmentVariable("USE_SQL_SERVER");
                        var sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER");
                        var sqlDatabase = Environment.GetEnvironmentVariable("SQL_DATABASE");
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] USE_SQL_SERVER = {useSqlServer ?? "null"}\n");
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SQL_SERVER = {sqlServer ?? "null"}\n");
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SQL_DATABASE = {sqlDatabase ?? "null"}\n");
                    }
                    else
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] .env file not found at: {envPath}\n");
                    }
                }
                catch (Exception envEx)
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Error loading .env file: {envEx.Message}\n");
                    // .env file is optional
                }
                
                // Initialize configuration service
                try
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Loading configuration service...\n");
                    KosovaPOS.Services.ConfigurationService.LoadConfiguration();
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Configuration loaded: Business Name = {KosovaPOS.Services.ConfigurationService.BusinessName}\n");
                }
                catch (Exception configEx)
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Configuration loading error (non-fatal): {configEx.Message}\n");
                }

                // Log which database mode will be used
                var dbMode = "SQL Server (BMDData)";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Database Mode: {dbMode}\n");

                if (POSDbContext.UseSqlServer)
                {
                    // Initialize SQL Server database
                    var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "localhost";
                    var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Connecting to SQL Server: {server}/{database}\n");
                    
                    // Run POSUsers table migration first
                    try
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Running POSUsers table migration...\n");
                        var migrationSuccess = KosovaPOS.Database.Migrations.POSUsersMigration.EnsureSchema();
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] POSUsers migration: {(migrationSuccess ? "SUCCESS" : "FAILED")}\n");
                    }
                    catch (Exception migEx)
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] POSUsers migration error: {migEx.Message}\n");
                    }
                    
                    // Run Pizzeria migration (create tables and sample data)
                    try
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Running Pizzeria migration...\n");
                        var pizzeriaResult = System.Threading.Tasks.Task.Run(async () => 
                            await KosovaPOS.Database.Migrations.PizzeriaMigration.RunAsync()).Result;
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Pizzeria migration: {(pizzeriaResult ? "SUCCESS" : "FAILED")}\n");
                    }
                    catch (Exception pizzaEx)
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Pizzeria migration error (non-fatal): {pizzaEx.Message}\n");
                    }

                    // Run Restaurant Management migration (create all feature tables)
                    try
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Running Restaurant Management migration...\n");
                        var restaurantResult = System.Threading.Tasks.Task.Run(async () => 
                            await KosovaPOS.Database.Migrations.RestaurantManagementMigration.EnsureTablesExistAsync()).Result;
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Restaurant Management migration: {(restaurantResult ? "SUCCESS" : "FAILED")}\n");
                    }
                    catch (Exception rmEx)
                    {
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Restaurant Management migration error (non-fatal): {rmEx.Message}\n");
                    }

                    using (var context = new POSDbContext())
                    {
                        // Test connection
                        var canConnect = false;
                        try
                        {
                            canConnect = context.Database.CanConnect();
                            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SQL Server connection test: {(canConnect ? "SUCCESS" : "FAILED")}\n");
                        }
                        catch (Exception connEx)
                        {
                            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SQL Server connection error: {connEx.Message}\n");
                            canConnect = false;
                        }
                        
                        if (!canConnect)
                        {
                            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SQL Server connection failed. Application will exit.\n");
                            MessageBox.Show(
                                $"⚠️ Lidhja me SQL Server dështoi!\n\n" +
                                $"Server: {server}\nDatabaza: {database}\n\n" +
                                $"Aplikacioni nuk mund të vazhdojë pa lidhje me SQL Server.\n\n" +
                                $"Zgjidhje të mundshme:\n" +
                                $"1. Sigurohuni që SQL Server është duke punuar\n" +
                                $"2. Hapni SQL Server Configuration Manager\n" +
                                $"3. Kontrolloni që databaza '{database}' ekziston\n" +
                                $"4. Verifikoni të drejtat e përdoruesit (Windows Authentication)\n" +
                                $"5. Kontrolloni variablin SQL_SERVER në skedarin .env",
                                "Gabim Fatal - Lidhja me Databazën",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            Shutdown();
                            return;
                        }
                        else
                        {
                            try
                            {
                                // Log article count from Artikujt table
                                var articleCount = context.Artikujt.Count();
                                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Articles in Artikujt table: {articleCount}\n");
                            }
                            catch (Exception ex)
                            {
                                // Artikujt table doesn't exist - database needs to be initialized
                                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Warning: Artikujt table not found. Database may need initialization: {ex.Message}\n");

                                // Check if Articles table exists as fallback
                                try
                                {
                                    var articlesCount = context.Articles.Count();
                                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Using Articles table instead: {articlesCount} articles found\n");
                                }
                                catch
                                {
                                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Neither Artikujt nor Articles tables found. Please run CreatePOSTables.sql or script.sql\n");
                                }
                            }
                        }
                    }
                }

                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Database initialized\n");
                
                // Initialize services
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Initializing services...\n");
                ServiceLocator.Initialize();
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Services initialized\n");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Startup complete!\n");

                // Show main window directly (login removed for streamlined user experience)
                var mainWindow = new KosovaPOS.Windows.MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Gabim gjatë inicializimit:\n{ex.Message}\n\n{ex.StackTrace}";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] STARTUP ERROR: {errorMsg}\n\n");
                MessageBox.Show(errorMsg, "Gabim Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                System.IO.File.AppendAllText(_logPath, $"[{DateTime.Now}] Application exiting cleanly...\n");
                
                // Force garbage collection before native cleanup to avoid vcruntime140 issues
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                System.IO.File.AppendAllText(_logPath, $"[{DateTime.Now}] Application exit complete.\n");
            }
            catch
            {
                // Suppress all exit errors silently
            }
            
            base.OnExit(e);
        }
    }
}
