using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using KosovaPOS.Models;

namespace KosovaPOS.Services
{
    public class FiscalPrinterService
    {
        private readonly string _tempPath;
        private readonly string _errorsPath;
        private readonly string _printedPath;
        private readonly string _printerModel;
        private readonly string _logFilePath;
        private readonly string _comPort;
        private readonly string _fiscalNumber;
        private readonly bool _useInlineArticles; // Don't program articles to device memory
        
        public FiscalPrinterService()
        {
            // Get temp path and normalize it for Windows
            var tempPathEnv = Environment.GetEnvironmentVariable("FISCAL_TEMP_PATH") ?? "C:\\TEMP\\";
            _tempPath = NormalizePath(tempPathEnv);
            _errorsPath = Path.Combine(_tempPath, "PrintErrors");
            _printedPath = Path.Combine(_tempPath, "Printed");
            _printerModel = Environment.GetEnvironmentVariable("FISCAL_PRINTER_MODEL") ?? "FP700+";
            _comPort = Environment.GetEnvironmentVariable("FISCAL_PRINTER_PORT") ?? "COM1";
            _fiscalNumber = Environment.GetEnvironmentVariable("FISCAL_NUMBER") ?? "003910";
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fiscal_printer.log");
            // Use inline articles (don't program to device) - this avoids Error #11
            _useInlineArticles = bool.Parse(Environment.GetEnvironmentVariable("FISCAL_INLINE_ARTICLES") ?? "true");
            
            // Ensure temp directory exists
            try
            {
                if (!Directory.Exists(_tempPath))
                {
                    Directory.CreateDirectory(_tempPath);
                    LogMessage($"Created fiscal temp directory: {_tempPath}");
                }
                else
                {
                    LogMessage($"Using existing fiscal temp directory: {_tempPath}");
                }
                
                // Ensure errors directory exists
                if (!Directory.Exists(_errorsPath))
                {
                    Directory.CreateDirectory(_errorsPath);
                    LogMessage($"Created print errors directory: {_errorsPath}");
                }
                
                // Ensure printed directory exists (for checking processed receipts)
                if (!Directory.Exists(_printedPath))
                {
                    Directory.CreateDirectory(_printedPath);
                    LogMessage($"Created printed receipts directory: {_printedPath}");
                }
                
                // Clean up old synchronization files to prevent Error #11
                CleanupSynchronizationFiles();
                
                // Check for orphaned receipt files that failed to process
                MonitorFailedReceipts();
                
                // Log COM port configuration
                LogMessage($"Fiscal Printer Configuration:");
                LogMessage($"  - Model: {_printerModel}");
                LogMessage($"  - COM Port: {_comPort}");
                LogMessage($"  - Fiscal Number: {_fiscalNumber}");
                LogMessage($"  - Temp Path: {_tempPath}");
                LogMessage($"  - Errors Path: {_errorsPath}");
                LogMessage($"  - Printed Path: {_printedPath}");
                LogMessage($"  - Format: S,1,FiscalNum,Seq,Ok;Name;Price;Qty;Tax;Dept;PayType;0;PLU;0;0");
                
                // Check if COM port exists
                var availablePorts = SerialPort.GetPortNames();
                if (availablePorts.Length == 0)
                {
                    LogMessage($"WARNING: No COM ports detected on this system!");
                    LogMessage($"  - Fiscal printer will not be able to communicate.");
                    LogMessage($"  - Fiscal receipt files will still be generated in: {_tempPath}");
                    LogMessage($"  - Failed receipts will be moved to: {_errorsPath}");
                    LogMessage($"  - Please connect the fiscal printer device and restart the application.");
                }
                else if (!availablePorts.Contains(_comPort))
                {
                    LogMessage($"WARNING: Configured COM port '{_comPort}' not found!");
                    LogMessage($"  - Available ports: {string.Join(", ", availablePorts)}");
                    LogMessage($"  - Please update FISCAL_PRINTER_PORT in Settings to match an available port.");
                }
                else
                {
                    LogMessage($"SUCCESS: COM port '{_comPort}' is available and ready.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to create temp directory '{_tempPath}': {ex.Message}");
                throw new InvalidOperationException($"Cannot create fiscal printer temp directory: {_tempPath}", ex);
            }
        }
        
        /// <summary>
        /// Gets all available COM ports on the system
        /// </summary>
        public static string[] GetAvailableComPorts()
        {
            try
            {
                return SerialPort.GetPortNames();
            }
            catch
            {
                return new string[0];
            }
        }
        
        /// <summary>
        /// Validates if a specific COM port exists
        /// </summary>
        public static bool IsComPortAvailable(string portName)
        {
            try
            {
                var availablePorts = SerialPort.GetPortNames();
                return availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        private string NormalizePath(string path)
        {
            // Convert forward slashes to backslashes for Windows
            path = path.Replace('/', '\\');
            
            // Ensure path ends with backslash
            if (!path.EndsWith("\\"))
            {
                path += "\\";
            }
            
            return path;
        }
        
        private void LogMessage(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                File.AppendAllText(_logFilePath, logEntry, Encoding.UTF8);
            }
            catch
            {
                // Silently fail if logging doesn't work
            }
        }
        
        /// <summary>
        /// Clean up synchronization files that can cause Error #11
        /// </summary>
        private void CleanupSynchronizationFiles()
        {
            try
            {
                // These files attempt to program articles to fiscalized devices causing Error #11
                var syncFiles = new[] 
                { 
                    "StartupSynchronizeDB.$$$", 
                    "SynchronizeDB.$$$",
                    "ClearArticle.in$"  // Article clearing file - also causes Error #11
                };
                
                foreach (var syncFile in syncFiles)
                {
                    var fullPath = Path.Combine(_tempPath, syncFile);
                    if (File.Exists(fullPath))
                    {
                        // Move to errors folder with timestamp
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var errorFileName = $"{Path.GetFileNameWithoutExtension(syncFile)}_{timestamp}{Path.GetExtension(syncFile)}";
                        var errorPath = Path.Combine(_errorsPath, errorFileName);
                        var errorLogPath = Path.Combine(_errorsPath, $"{Path.GetFileNameWithoutExtension(syncFile)}_{timestamp}_ERROR.txt");
                        
                        File.Move(fullPath, errorPath, true);
                        
                        // Create error log
                        var errorLog = $"File: {syncFile}\n" +
                                     $"Moved at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                     $"Reason: Article programming/synchronization not allowed after fiscalization\n" +
                                     $"Error: Cannot program or change article (Error #11 - This command is not allowed in the current fiscal mode)\n" +
                                     $"Solution: Do not program articles to fiscalized device. Use inline articles mode instead.\n" +
                                     $"Moved to: {errorPath}\n";
                        
                        File.WriteAllText(errorLogPath, errorLog, Encoding.UTF8);
                        
                        LogMessage($"⚠️ Moved synchronization file to errors folder: {syncFile} -> {errorFileName}");
                        LogMessage($"   This file tried to program articles to fiscalized device (Error #11)");
                        LogMessage($"   Error details saved to: {errorLogPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Warning: Could not clean up synchronization files: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Monitor for failed receipt files that were not processed
        /// </summary>
        private void MonitorFailedReceipts()
        {
            try
            {
                // Check for Fatura.inp that is older than 2 minutes (likely stuck/failed)
                var cutoffTime = DateTime.Now.AddMinutes(-2);
                var faturaFile = Path.Combine(_tempPath, "Fatura.inp");
                
                if (File.Exists(faturaFile))
                {
                    var fileInfo = new FileInfo(faturaFile);
                    
                    // Skip if file is still recent (might be processing)
                    if (fileInfo.LastWriteTime > cutoffTime)
                    {
                        LogMessage($"ℹ️  Found recent Fatura.inp (created {fileInfo.LastWriteTime:HH:mm:ss})");
                        LogMessage($"   File is less than 2 minutes old - likely being processed by F-Link");
                    }
                    else
                    {
                        // This file has been sitting unprocessed for too long
                        LogMessage($"⚠️  Found old Fatura.inp (created {fileInfo.LastWriteTime:HH:mm:ss})");
                        LogMessage($"   File is older than 2 minutes - F-Link may not be processing it");
                        LogMessage($"   Possible F-Link service issue or printer communication problem");
                        
                        // Move to error folder for investigation
                        MoveToErrorFolder(faturaFile, "Fatura.inp not processed by F-Link within 2 minutes. Check F-Link service status and fiscal printer connection.");
                    }
                }
                
                // Check today's printed receipts
                CheckPrintedReceipts();
            }
            catch (Exception ex)
            {
                LogMessage($"Warning: Could not monitor failed receipts: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check for printed receipts in the Printed folder (organized by date in ZIP files)
        /// </summary>
        private void CheckPrintedReceipts()
        {
            try
            {
                var todayDate = DateTime.Now.ToString("yyyy-MM-dd");
                var todayZipFile = Path.Combine(_printedPath, $"{todayDate}.zip");
                
                if (File.Exists(todayZipFile))
                {
                    var zipInfo = new FileInfo(todayZipFile);
                    LogMessage($"✓ Today's printed receipts archive found: {todayZipFile} ({zipInfo.Length} bytes)");
                    LogMessage($"  Archive contains successfully printed and fiscalized receipts from {todayDate}");
                }
                else
                {
                    LogMessage($"ℹ️ No printed receipts archive yet for today: {todayZipFile}");
                    LogMessage($"  Archive will be created automatically by fiscal printer service after first successful print");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Warning: Could not check printed receipts: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Move a failed file to the errors folder with error details
        /// </summary>
        private void MoveToErrorFolder(string filePath, string errorReason)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;
                
                var fileName = Path.GetFileName(filePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var errorFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
                var errorPath = Path.Combine(_errorsPath, errorFileName);
                var errorLogPath = Path.Combine(_errorsPath, $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}_ERROR.txt");
                
                // Move the file
                File.Move(filePath, errorPath, true);
                
                // Create error log
                var errorLog = $"Original File: {fileName}\n" +
                             $"Moved at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                             $"Error Reason: {errorReason}\n" +
                             $"Moved to: {errorPath}\n" +
                             $"\n--- File Contents ---\n" +
                             File.ReadAllText(errorPath, Encoding.UTF8);
                
                File.WriteAllText(errorLogPath, errorLog, Encoding.UTF8);
                
                LogMessage($"❌ Moved failed receipt to errors folder: {fileName} -> {errorFileName}");
                LogMessage($"   Reason: {errorReason}");
                LogMessage($"   Error details saved to: {errorLogPath}");
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Could not move file to error folder: {ex.Message}");
            }
        }
        
        public string GenerateZReport()
        {
            var fileName = Path.Combine(_tempPath, "Fatura.inp");
            var content = $"Z,1,{_fiscalNumber},1,Ok;\n";
            
            try
            {
                // Use UTF-8 without BOM to avoid encoding issues with fiscal printer software
                var encoding = new UTF8Encoding(false);
                File.WriteAllText(fileName, content, encoding);
                LogMessage($"Generated Z-Report file: {fileName}");
                return fileName;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to generate Z-Report at '{fileName}': {ex.Message}");
                throw;
            }
        }
        
        public string GenerateXReport()
        {
            var fileName = Path.Combine(_tempPath, "Fatura.inp");
            var content = $"X,1,{_fiscalNumber},1,Ok;\n";
            
            try
            {
                // Use UTF-8 without BOM to avoid encoding issues with fiscal printer software
                var encoding = new UTF8Encoding(false);
                File.WriteAllText(fileName, content, encoding);
                LogMessage($"Generated X-Report file: {fileName}");
                return fileName;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to generate X-Report at '{fileName}': {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Clear fiscal printer cache to allow article name changes during the day
        /// WARNING: Use only when absolutely necessary (e.g., printer not working)
        /// </summary>
        public string ClearPrinterCache()
        {
            var fileName = Path.Combine(_tempPath, "ClearCache.inp");
            var sb = new StringBuilder();
            
            LogMessage($"⚠️ WARNING: Clearing fiscal printer cache!");
            LogMessage($"⚠️ This will allow article names to be changed during the day");
            LogMessage($"⚠️ Use only if printer is not working properly");
            
            // Send cache clearing command - F-Link INPUT format (not response format)
            // Format: C,1,FiscalNum,SeqNum,Status; (Clear cache command)
            sb.AppendLine($"C,1,{_fiscalNumber},1,Ok;");
            
            try
            {
                var encoding = new UTF8Encoding(false);
                File.WriteAllText(fileName, sb.ToString(), encoding);
                LogMessage($"Cache clearing command created: {fileName}");
                LogMessage($"Cache will be cleared after this file is processed by fiscal printer");
                return fileName;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to create cache clearing file: {ex.Message}");
                throw;
            }
        }
        
        public string GenerateFiscalReceipt(Receipt receipt)
        {
            var receiptNumber = receipt.ReceiptNumber.Replace("-", "");
            // F-Link expects files in format: Fatura.inp or Fat XXX.inp
            // CRITICAL: File must be named "Fatura.inp" for F-Link to process it
            // F-Link monitors temp folder for "Fatura.inp" files specifically
            var fileName = Path.Combine(_tempPath, "Fatura.inp");
            var sb = new StringBuilder();
            
            LogMessage($"Generating fiscal receipt for {receipt.ReceiptNumber}...");
            LogMessage($"Using F-Link INPUT format: Fatura.inp");
            LogMessage($"F-Link will monitor temp folder and process this file automatically");
            
            // F-Link INPUT file format - INLINE ARTICLE MODE (no PLU lookup):
            // S,1,______,_,__;ArticleName;Price;Qty;TaxGroup;Dept;PayType
            // 
            // When articles are cleared from the fiscal printer or PLU is not mapped,
            // we use inline mode which prints the article name directly without
            // looking up articles in the fiscal printer's database.
            
            // VALIDATION: Check all items have required fields before proceeding
            ValidateReceiptItems(receipt);
            
            // Add receipt items
            foreach (var item in receipt.Items)
            {
                // Validate article code/name is not missing
                if (string.IsNullOrWhiteSpace(item.ArticleName))
                {
                    var errorMsg = $"Missing article code/name for item with barcode: {item.Barcode}. Article name cannot be empty.";
                    LogMessage($"ERROR: {errorMsg}");
                    throw new InvalidOperationException(errorMsg);
                }
                
                // F-Link INPUT FORMAT - CORRECT FORMAT:
                // S,1,______,_,__;ArticleName;Price;Qty;TaxGrp;Dept;PayType;0;PLU;0;0
                // 
                // Fields:
                // - S = Sale command
                // - 1 = Operation type
                // - ______ = Placeholder (F-Link fills this with fiscal number)
                // - _ = Placeholder (F-Link fills this with sequence)
                // - __ = Placeholder (F-Link fills this with status)
                // - ArticleName = Name to print on receipt
                // - Price = Unit price per item
                // - Qty = Quantity
                // - TaxGroup = Tax group (1=18%, 2=8%, 3=0%)
                // - Dept = Department (1 = default)
                // - PayType = Payment type (3 = cash)
                // - 0 = Unknown field
                // - PLU = Article PLU code
                // - 0;0 = Additional fields
                //
                // Note: PLU fields are omitted to use inline article mode
                
                var taxGroup = GetTaxGroup(item.VATRate);
                var dept = 1; // Department 1
                var payType = 3; // Cash payment type
                
                // Use Culture-invariant decimal format (dot as decimal separator)
                var unitPriceStr = item.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                var qtyStr = item.Quantity.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                
                // Sanitize article name - remove special characters that might break F-Link format
                var sanitizedName = SanitizeArticleName(item.ArticleName);
                
                LogMessage($"Item: Name={sanitizedName}, Price: {item.Price:F2}, Qty: {item.Quantity}, TaxGrp: {taxGroup}");
                
                // F-Link INPUT format - CORRECT FORMAT:
                // Format: S,1,______,_,__;ArticleName;Price;Qty;TaxGrp;Dept;PayType;0;PLU;0;0
                // NOTE: Use underscores as placeholders - F-Link fills these in
                // NOTE: Order is Price;Qty for proper receipt display
                var plu = item.PLU > 0 ? item.PLU : 0; // Use PLU from item if available
                sb.AppendLine($"S,1,______,_,__;{sanitizedName};{unitPriceStr};{qtyStr};{taxGroup};{dept};{payType};0;{plu};0;0");
            }
            
            // Payment line - F-Link INPUT format with placeholder underscores
            var paidStr = receipt.PaidAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"Q,1,______,_,__;1;Pagoi: {paidStr}");
            
            // Change line
            var changeAmount = receipt.PaidAmount - receipt.TotalAmount;
            var changeStr = changeAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"Q,1,______,_,__;2;Kusur: {changeStr}");
            
            // Total/End transaction - F-Link INPUT format with placeholder underscores
            sb.AppendLine($"T,1,______,_,__;");
            
            try
            {
                // CRITICAL: Delete existing Fatura.inp if it exists (from previous receipt)
                // This ensures F-Link processes only the new receipt
                if (File.Exists(fileName))
                {
                    try
                    {
                        File.Delete(fileName);
                        LogMessage($"Deleted existing Fatura.inp to prevent conflicts");
                        // Wait a moment to ensure file deletion is complete
                        System.Threading.Thread.Sleep(100);
                    }
                    catch (Exception deleteEx)
                    {
                        LogMessage($"WARNING: Could not delete existing Fatura.inp: {deleteEx.Message}");
                        // Try to proceed anyway - might be in use by F-Link
                    }
                }
                
                // Use UTF-8 without BOM to avoid encoding issues with fiscal printer software
                var encoding = new UTF8Encoding(false);
                File.WriteAllText(fileName, sb.ToString(), encoding);
                LogMessage($"✓ Successfully created fiscal receipt file: {fileName}");
                LogMessage($"Receipt content ({sb.Length} bytes):\n{sb.ToString()}");
                LogMessage($"⏳ F-Link service should detect and process this file automatically");
                LogMessage($"Expected workflow:");
                LogMessage($"  1. Fatura.inp created in: {_tempPath}");
                LogMessage($"  2. F-Link detects file and sends to fiscal printer");
                LogMessage($"  3. On success: File moved to {_printedPath}\\{DateTime.Now:yyyy-MM-dd}.zip");
                LogMessage($"  4. On error: Error details created in same folder or error logs");
                return fileName;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to create fiscal receipt file '{fileName}': {ex.Message}");
                throw;
            }
        }
        
        private int GetTaxGroup(decimal vatRate)
        {
            // Kosovo VAT groups
            // 1 = 18% (Standard)
            // 2 = 8% (Reduced)
            // 3 = 0% (Zero-rated)
            return vatRate switch
            {
                18 => 1,
                8 => 2,
                _ => 3
            };
        }
        
        /// <summary>
        /// Sanitize article name for F-Link fiscal printer format
        /// Removes or replaces special characters that might break the F-Link protocol
        /// </summary>
        private string SanitizeArticleName(string articleName)
        {
            if (string.IsNullOrWhiteSpace(articleName))
                return "UNKNOWN_ARTICLE";
            
            // Remove characters that are F-Link protocol delimiters or problematic
            // Keep alphanumeric, basic punctuation, and common symbols
            var sanitized = new StringBuilder();
            foreach (var ch in articleName)
            {
                // Keep letters, digits, spaces, and these special characters
                if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '.' || ch == ',' || ch == '&' || ch == '/' || ch == '(' || ch == ')')
                {
                    sanitized.Append(ch);
                }
                else
                {
                    // Replace forbidden characters with space
                    sanitized.Append(' ');
                }
            }
            
            var result = sanitized.ToString().Trim();
            
            // F-Link has a maximum length for article names (typically 250 chars)
            if (result.Length > 250)
            {
                result = result.Substring(0, 250);
            }
            
            // If empty after sanitization, return a default
            if (string.IsNullOrWhiteSpace(result))
            {
                result = "UNNAMED_ARTICLE";
            }
            
            return result;
        }
        
        /// <summary>
        /// Generate article synchronization file (ONLY use before fiscalization)
        /// WARNING: This will fail with Error #11 if device is already fiscalized
        /// </summary>
        public string GenerateArticleSynchronization(List<Article> articles)
        {
            var fileName = Path.Combine(_tempPath, "StartupSynchronizeDB.$$$");
            var sb = new StringBuilder();
            
            LogMessage($"⚠️ WARNING: Generating article synchronization for {articles.Count} articles");
            LogMessage($"⚠️ This will FAIL if the fiscal printer is already fiscalized!");
            LogMessage($"⚠️ Use this ONLY during initial device setup before first fiscalization");
            
            // Start synchronization
            sb.AppendLine($"L,1,{_fiscalNumber},1,Ok;SynchronizeDB;Articles;");
            
            // Add each article programming command
            var seqNum = 2;
            foreach (var article in articles)
            {
                // P command: Program article to device memory
                // Format: P,1,FiscalNum,SeqNum,Status;ProductCode;ArticleName;Price;TaxGroup;Department;VATRate
                var taxGroup = GetTaxGroup(article.VATRate);
                sb.AppendLine($"P,1,{_fiscalNumber},{seqNum},Ok;{article.Barcode};{article.Name};{article.SalesPrice:F2};{taxGroup};1;{article.VATRate:F0}");
                seqNum++;
            }
            
            // End synchronization
            sb.AppendLine($"J,1,{_fiscalNumber},{seqNum},Ok;SynchronizeDB;Done;");
            
            try
            {
                var encoding = new UTF8Encoding(false);
                File.WriteAllText(fileName, sb.ToString(), encoding);
                LogMessage($"Article synchronization file created: {fileName}");
                LogMessage($"⚠️ IMPORTANT: Delete or rename this file after fiscalization to prevent Error #11");
                return fileName;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to create article synchronization file: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Send to fiscal printer and WAIT for F-Link to process the file.
        /// Returns true only if the file was actually processed (deleted by F-Link).
        /// </summary>
        /// <param name="filePath">Path to the fiscal receipt file</param>
        /// <param name="errorMessage">Error message if printing failed</param>
        /// <param name="timeoutSeconds">How long to wait for F-Link to process (default 30 seconds)</param>
        /// <returns>True if file was processed, false otherwise</returns>
        public bool SendToFiscalPrinterAndWait(string filePath, out string errorMessage, int timeoutSeconds = 30)
        {
            errorMessage = "";
            
            try
            {
                LogMessage($"[SYNC] Starting synchronous fiscal print for: {filePath}");
                
                // Verify the file exists
                if (!File.Exists(filePath))
                {
                    errorMessage = $"Skedari fiskal nuk u gjet: {filePath}";
                    LogMessage($"ERROR: {errorMessage}");
                    return false;
                }
                
                var fileInfo = new FileInfo(filePath);
                LogMessage($"[SYNC] File exists: {filePath} ({fileInfo.Length} bytes)");
                
                // Check if COM port is available
                var availablePorts = SerialPort.GetPortNames();
                if (availablePorts.Length == 0)
                {
                    errorMessage = "Asnjë port COM nuk u gjet në sistem. Lidhni printerin fiskal.";
                    LogMessage($"ERROR: {errorMessage}");
                    return false;
                }
                
                if (!availablePorts.Contains(_comPort))
                {
                    errorMessage = $"Porti COM '{_comPort}' nuk u gjet. Portet e disponueshme: {string.Join(", ", availablePorts)}";
                    LogMessage($"WARNING: {errorMessage}");
                    // Continue anyway - F-Link might be configured with a different port
                }
                
                LogMessage($"[SYNC] Waiting for F-Link to process file (timeout: {timeoutSeconds}s)...");
                
                // Wait for F-Link to process the file
                var startTime = DateTime.Now;
                var checkIntervalMs = 500; // Check every 500ms
                var maxWaitTime = TimeSpan.FromSeconds(timeoutSeconds);
                
                while (DateTime.Now - startTime < maxWaitTime)
                {
                    // Check if file still exists
                    if (!File.Exists(filePath))
                    {
                        // File was deleted by F-Link - SUCCESS!
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        LogMessage($"[SYNC] ✅ SUCCESS: File processed by F-Link in {elapsed:F1} seconds");
                        
                        // Check for error file
                        var errorFilePath = Path.ChangeExtension(filePath, ".err");
                        var errorTxtPath = Path.ChangeExtension(filePath, ".txt");
                        
                        if (File.Exists(errorFilePath))
                        {
                            var errContent = File.ReadAllText(errorFilePath);
                            errorMessage = $"F-Link raportoi gabim: {errContent}";
                            LogMessage($"[SYNC] ❌ ERROR: F-Link created error file: {errContent}");
                            return false;
                        }
                        
                        if (File.Exists(errorTxtPath))
                        {
                            var errContent = File.ReadAllText(errorTxtPath);
                            if (errContent.ToLower().Contains("error") || errContent.ToLower().Contains("gabim"))
                            {
                                errorMessage = $"F-Link raportoi gabim: {errContent}";
                                LogMessage($"[SYNC] ❌ ERROR: F-Link created error file: {errContent}");
                                return false;
                            }
                        }
                        
                        // Check printed folder for confirmation
                        var todayZip = Path.Combine(_printedPath, $"{DateTime.Now:yyyy-MM-dd}.zip");
                        if (File.Exists(todayZip))
                        {
                            LogMessage($"[SYNC] ✅ Receipt archived in: {todayZip}");
                        }
                        
                        return true;
                    }
                    
                    // Check for error files while waiting
                    var possibleErrorFile = Path.ChangeExtension(filePath, ".err");
                    if (File.Exists(possibleErrorFile))
                    {
                        var errContent = File.ReadAllText(possibleErrorFile);
                        errorMessage = $"F-Link raportoi gabim: {errContent}";
                        LogMessage($"[SYNC] ❌ ERROR: F-Link created error file while processing: {errContent}");
                        
                        // Clean up the original file if it still exists
                        if (File.Exists(filePath))
                        {
                            try { File.Delete(filePath); } catch { }
                        }
                        
                        return false;
                    }
                    
                    System.Threading.Thread.Sleep(checkIntervalMs);
                }
                
                // Timeout - file was not processed
                var waitedTime = (DateTime.Now - startTime).TotalSeconds;
                errorMessage = $"Printeri fiskal nuk u përgjigj për {waitedTime:F0} sekonda. Kontrollo që F-Link është aktiv dhe printeri është i lidhur.";
                LogMessage($"[SYNC] ❌ TIMEOUT: File not processed after {waitedTime:F0} seconds");
                LogMessage($"[SYNC] Possible causes:");
                LogMessage($"   - F-Link service not running");
                LogMessage($"   - F-Link not monitoring: {_tempPath}");
                LogMessage($"   - Fiscal printer not connected or powered off");
                LogMessage($"   - COM port {_comPort} incorrect or in use");
                
                // Move to error folder for troubleshooting
                MoveToErrorFolder(filePath, errorMessage);
                
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Gabim i papritur: {ex.Message}";
                LogMessage($"[SYNC] EXCEPTION: {ex.Message}");
                LogMessage($"[SYNC] Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        public bool SendToFiscalPrinter(string filePath)
        {
            try
            {
                LogMessage($"Checking fiscal printer file: {filePath}");
                
                // Verify the file exists
                if (!File.Exists(filePath))
                {
                    LogMessage($"ERROR: Fiscal receipt file not found: {filePath}");
                    return false;
                }
                
                var fileInfo = new FileInfo(filePath);
                LogMessage($"File exists: {filePath} ({fileInfo.Length} bytes)");
                
                // Check if COM port is available (for logging only - don't move to errors)
                var availablePorts = SerialPort.GetPortNames();
                if (availablePorts.Length == 0)
                {
                    LogMessage($"WARNING: No COM ports available on this system!");
                    LogMessage($"  - Receipt file created in temp folder: {_tempPath}");
                    LogMessage($"  - External fiscal printer service will handle this file");
                    LogMessage($"  - If fiscal printer is connected and service is running, the receipt will be printed");
                }
                else if (!availablePorts.Contains(_comPort))
                {
                    LogMessage($"WARNING: Configured COM port '{_comPort}' not found!");
                    LogMessage($"  - Available ports: {string.Join(", ", availablePorts)}");
                    LogMessage($"  - Receipt file created in temp folder: {_tempPath}");
                    LogMessage($"  - External fiscal printer service will handle this file");
                }
                else
                {
                    LogMessage($"✓ COM port '{_comPort}' is available");
                }
                
                LogMessage($"✓ Fiscal receipt file ready for F-Link processing: {Path.GetFileName(filePath)}");
                LogMessage($"  F-Link Service Configuration:");
                LogMessage($"    - Model: {_printerModel}");
                LogMessage($"    - COM Port: {_comPort}");
                LogMessage($"    - Monitoring folder: {_tempPath}");
                LogMessage($"");
                LogMessage($"  Expected F-Link Workflow:");
                LogMessage($"    1. ✓ File created: {_tempPath}Fatura.inp");
                LogMessage($"    2. ⏳ F-Link detects Fatura.inp");
                LogMessage($"    3. ⏳ F-Link sends data to fiscal printer via {_comPort}");
                LogMessage($"    4a. ON SUCCESS: F-Link deletes Fatura.inp and creates entry in {_printedPath}\\{DateTime.Now:yyyy-MM-dd}.zip");
                LogMessage($"    4b. ON ERROR: F-Link keeps Fatura.inp or creates error file with details");
                LogMessage($"");
                LogMessage($"  ℹ️  This file will be monitored for 2 minutes to verify F-Link processing");
                LogMessage($"  ℹ️  If file is not processed, it indicates F-Link service issue");
                
                // Start monitoring this file for processing with shorter timeout
                var fileName = Path.GetFileName(filePath);
                System.Threading.Tasks.Task.Run(async () =>
                {
                    // Check every 10 seconds for 2 minutes (12 checks)
                    for (int i = 0; i < 12; i++)
                    {
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10));
                        
                        // Check if file still exists
                        if (!File.Exists(filePath))
                        {
                            // File was processed by F-Link!
                            LogMessage($"✅ SUCCESS: Fatura.inp was processed by F-Link after {(i + 1) * 10} seconds");
                            
                            // Check if it's in the Printed folder
                            var todayZip = Path.Combine(_printedPath, $"{DateTime.Now:yyyy-MM-dd}.zip");
                            if (File.Exists(todayZip))
                            {
                                var zipInfo = new FileInfo(todayZip);
                                LogMessage($"✅ Receipt archived in: {todayZip} ({zipInfo.Length} bytes)");
                                LogMessage($"📄 Fiscal receipt was successfully printed and archived");
                            }
                            else
                            {
                                LogMessage($"⚠️  Fatura.inp was processed but Printed archive not found yet");
                                LogMessage($"⚠️  F-Link may create the ZIP file after accumulating multiple receipts");
                            }
                            
                            return; // Exit monitoring
                        }
                    }
                    
                    // File still exists after 2 minutes - F-Link is not processing it
                    LogMessage($"❌ ERROR: Fatura.inp NOT processed by F-Link after 2 minutes!");
                    LogMessage($"❌ Possible causes:");
                    LogMessage($"   1. F-Link service is not running");
                    LogMessage($"   2. F-Link is not monitoring folder: {_tempPath}");
                    LogMessage($"   3. Fiscal printer is not connected or powered off");
                    LogMessage($"   4. COM port {_comPort} is incorrect or in use by another program");
                    LogMessage($"   5. File format is incorrect or contains errors");
                    LogMessage($"");
                    LogMessage($"📝 Troubleshooting steps:");
                    LogMessage($"   1. Check if F-Link service is running in Task Manager");
                    LogMessage($"   2. Check F-Link configuration points to: {_tempPath}");
                    LogMessage($"   3. Verify fiscal printer is connected and powered on");
                    LogMessage($"   4. Check F-Link logs for error messages");
                    LogMessage($"   5. Manually check {_tempPath} for error files (.err or .txt)");
                    
                    // Don't automatically move to error folder - let user investigate
                    // The file stays in temp folder for manual inspection/troubleshooting
                });
                
                // In a real implementation, the external fiscal printer service (like F-Link KS)
                // monitors the temp folder and automatically sends .inp files to the fiscal printer
                // via serial port (COM8 or configured port).
                //
                // The file format follows the F-Link KS protocol for Kosovo fiscal printers.
                // Common errors:
                // - "There is no COM port with number X": The configured COM port doesn't exist
                //   Solution: Check available ports and update FISCAL_PRINTER_PORT in Settings
                // - "Specified device not found": COM port exists but device is not connected
                //   Solution: Connect fiscal printer and ensure it's powered on
                // - "Access to the path 'COMX' is denied": Another application is using the COM port
                //   Solution: Ensure only one fiscal service is running, or restart the fiscal service
                // - Error #11: Cannot program or change article in fiscal mode
                //   Solution: Use inline articles mode (FISCAL_INLINE_ARTICLES=true)
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Exception in SendToFiscalPrinter: {ex.Message}");
                
                // Don't move to error folder on exception - let the file stay for the external service
                LogMessage($"  Receipt file remains in temp folder for external service to process: {filePath}");
                
                return false;
            }
        }
        
        /// <summary>
        /// Generate a test fiscal receipt to verify the system is working
        /// </summary>
        public string GenerateTestReceipt()
        {
            var fileName = Path.Combine(_tempPath, $"TestReceipt_{DateTime.Now:yyyyMMdd_HHmmss}.inp");
            var sb = new StringBuilder();
            
            LogMessage($"Generating TEST fiscal receipt...");
            LogMessage($"Using F-Link INPUT format with inline articles");
            
            // Test receipt with one item
            // Format: S,1,FiscalNum,Seq,Ok;ArticleName;Price;Qty;TaxGrp;Dept;PayType;0;PLU;0;0
            sb.AppendLine($"S,1,{_fiscalNumber},1,Ok;TEST ARTICLE;1.00;1.00;1;1;3;0;0;0;0");
            
            // Payment line
            sb.AppendLine($"Q,1,{_fiscalNumber},2,Ok;1;Pagoi: 1.00");
            
            // Change line
            sb.AppendLine($"Q,1,{_fiscalNumber},3,Ok;2;Kusur: 0.00");
            
            // Total/End transaction
            sb.AppendLine($"T,1,{_fiscalNumber},4,Ok;");
            
            try
            {
                var encoding = new UTF8Encoding(false);
                File.WriteAllText(fileName, sb.ToString(), encoding);
                LogMessage($"✓ Test receipt created successfully: {fileName}");
                LogMessage($"Test receipt content ({sb.Length} bytes):\n{sb.ToString()}");
                LogMessage($"\nTest receipt should be processed by F-Link service automatically.");
                LogMessage($"Check C:\\TEMP\\Printed\\{DateTime.Now:yyyy-MM-dd}.zip for successfully printed receipts.");
                LogMessage($"Check C:\\TEMP\\PrintErrors for any failed receipts.");
                return fileName;
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to create test receipt file '{fileName}': {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Validates that all receipt items have required fields for fiscal printing
        /// </summary>
        private void ValidateReceiptItems(Receipt receipt)
        {
            if (receipt.Items == null || receipt.Items.Count == 0)
            {
                throw new InvalidOperationException("Cannot create fiscal receipt - no items in receipt");
            }
            
            var itemsWithErrors = new List<string>();
            
            foreach (var item in receipt.Items)
            {
                var errors = new List<string>();
                
                // Check article name
                if (string.IsNullOrWhiteSpace(item.ArticleName))
                {
                    errors.Add("Missing article name");
                }
                
                // Check barcode/article code
                if (string.IsNullOrWhiteSpace(item.Barcode))
                {
                    errors.Add("Missing barcode/article code");
                }
                
                // Check quantity
                if (item.Quantity <= 0)
                {
                    errors.Add($"Invalid quantity: {item.Quantity}");
                }
                
                // Check price
                if (item.Price <= 0)
                {
                    errors.Add($"Invalid price: {item.Price}");
                }
                
                if (errors.Any())
                {
                    var itemDesc = !string.IsNullOrEmpty(item.ArticleName) 
                        ? item.ArticleName 
                        : !string.IsNullOrEmpty(item.Barcode) 
                            ? $"[{item.Barcode}]" 
                            : "[Unknown]";
                    itemsWithErrors.Add($"{itemDesc}: {string.Join("; ", errors)}");
                }
            }
            
            if (itemsWithErrors.Any())
            {
                var errorMessage = "Cannot create fiscal receipt - validation errors:\n" + 
                                 string.Join("\n", itemsWithErrors);
                LogMessage($"VALIDATION ERROR:\n{errorMessage}");
                throw new InvalidOperationException(errorMessage);
            }
            
            LogMessage($"✓ All {receipt.Items.Count} receipt items validated successfully");
        }
    }
}

