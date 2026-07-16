using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KosovaPOS.Database;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Service to sync product photos between web store and POS
    /// </summary>
    public class WebStoreSyncService
    {
        private static readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(3) };
        private readonly string _webStoreUrl;
        private readonly string _localPhotosPath;

        public WebStoreSyncService(string? webStoreUrl = null)
        {
            // Default to the Netlify site URL
            _webStoreUrl = webStoreUrl ?? "https://c40710fc-b7ad-4776-b5d5-b0d607129ba8.netlify.app";
            
            // Local photos storage
            _localPhotosPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProductPhotos");
            
            if (!Directory.Exists(_localPhotosPath))
            {
                Directory.CreateDirectory(_localPhotosPath);
            }
        }

        /// <summary>
        /// Sync photos from web store to local POS database
        /// </summary>
        public async Task<int> SyncPhotosFromWebAsync()
        {
            int syncedCount = 0;

            try
            {
                // Fetch products with photos from web store
                var response = await _httpClient.GetStringAsync($"{_webStoreUrl}/.netlify/functions/get-products");
                var products = JsonSerializer.Deserialize<List<WebProduct>>(response, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                using var context = new POSDbContext();

                foreach (var webProduct in products)
                {
                    // Get the photo URL from whichever field has it
                    var photoUrl = webProduct.GetPhotoUrl();
                    if (string.IsNullOrEmpty(photoUrl)) continue;

                    var article = context.Articles.Find(webProduct.Id);
                    if (article == null) 
                    {
                        // Try to find by barcode
                        if (!string.IsNullOrEmpty(webProduct.Barcode))
                        {
                            article = context.Articles.FirstOrDefault(a => a.Barcode == webProduct.Barcode);
                        }
                        if (article == null) continue;
                    }

                    // Check if it's a base64 data URL
                    if (photoUrl.StartsWith("data:image"))
                    {
                        // Save base64 image to local file
                        var localPath = await SaveBase64ImageAsync(article.Id, photoUrl);
                        if (localPath != null && article.PhotoPath != localPath)
                        {
                            article.PhotoPath = localPath;
                            article.UpdatedAt = DateTime.Now;
                            syncedCount++;
                            Console.WriteLine($"Synced photo for: {article.Name}");
                        }
                    }
                    else if (photoUrl.StartsWith("http"))
                    {
                        // Download image from URL
                        var localPath = await DownloadImageAsync(article.Id, photoUrl);
                        if (localPath != null && article.PhotoPath != localPath)
                        {
                            article.PhotoPath = localPath;
                            article.UpdatedAt = DateTime.Now;
                            syncedCount++;
                            Console.WriteLine($"Downloaded photo for: {article.Name}");
                        }
                    }
                }

                if (syncedCount > 0)
                {
                    await context.SaveChangesAsync();
                }

                return syncedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Photo sync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Preview what would be synced to the web store (shows changes without applying)
        /// </summary>
        public async Task<SyncPreviewResult> PreviewSyncToWebStoreAsync()
        {
            try
            {
                var products = await GetProductsForSyncAsync();
                
                var requestBody = new
                {
                    previewOnly = true,
                    syncQuantity = true,
                    syncPrice = true,
                    syncPhotos = true
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_webStoreUrl}/.netlify/functions/sync-products", content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Server returned {response.StatusCode}: {responseText}");
                }

                var result = JsonSerializer.Deserialize<SyncPreviewResponse>(responseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return new SyncPreviewResult
                {
                    Success = result?.Success ?? false,
                    TotalProducts = products.Count,
                    NewProducts = result?.Summary?.NewProducts ?? 0,
                    UpdatedProducts = result?.Summary?.UpdatedProducts ?? 0,
                    UnchangedProducts = result?.Summary?.Unchanged ?? 0,
                    Changes = result?.Changes ?? new List<ChangeInfo>()
                };
            }
            catch (Exception ex)
            {
                return new SyncPreviewResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Push all products from POS to web store
        /// </summary>
        public async Task<SyncResult> PushProductsToWebStoreAsync(bool syncQuantity = true, bool syncPrice = true, bool syncPhotos = true, List<int>? productIds = null)
        {
            try
            {
                // First, update the products.json file and deploy
                var products = await GetProductsForSyncAsync();
                
                // Filter by IDs if specified
                if (productIds != null && productIds.Count > 0)
                {
                    products = products.Where(p => productIds.Contains(((dynamic)p).id)).ToList();
                }

                var requestBody = new
                {
                    syncQuantity,
                    syncPrice,
                    syncPhotos,
                    productIds,
                    previewOnly = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_webStoreUrl}/.netlify/functions/sync-products", content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Server returned {response.StatusCode}: {responseText}");
                }

                var result = JsonSerializer.Deserialize<SyncApiResponse>(responseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return new SyncResult
                {
                    Success = result?.Success ?? false,
                    Message = result?.Message ?? "Unknown response",
                    SyncedCount = result?.Stats?.Synced ?? 0,
                    UpdatedCount = result?.Stats?.Updated ?? 0,
                    ErrorCount = result?.Stats?.Errors ?? 0,
                    TotalCount = result?.Stats?.Total ?? 0
                };
            }
            catch (Exception ex)
            {
                return new SyncResult
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorCount = 1
                };
            }
        }

        /// <summary>
        /// Get products from SQL Server formatted for web store sync
        /// </summary>
        private async Task<List<object>> GetProductsForSyncAsync()
        {
            var products = new List<object>();
            
            using var context = new POSDbContext();
            var articles = ArticleDataService.GetAllArticles();
            
            foreach (var article in articles)
            {
                if (!article.IsActive) continue;

                products.Add(new
                {
                    id = article.Id,
                    barcode = article.Barcode ?? "",
                    name = article.Name ?? "Artikull",
                    unit = article.Unit ?? "Copë",
                    salesPrice = article.SalesPrice,
                    category = article.Category ?? "",
                    brand = article.Brand ?? "",
                    supplier = article.Supplier ?? "",
                    stockQuantity = article.StockQuantity,
                    photoPath = article.PhotoPath ?? "",
                    size = article.Size ?? "",
                    color = article.Color ?? "",
                    photoUrl = (string?)null
                });
            }
            
            return products;
        }

        /// <summary>
        /// Export products to JSON for web store
        /// </summary>
        public async Task ExportProductsToJsonAsync(string outputPath)
        {
            using var context = new POSDbContext();
            var products = new List<object>();

            foreach (var article in context.Articles)
            {
                if (!article.IsActive) continue;

                products.Add(new
                {
                    id = article.Id,
                    barcode = article.Barcode,
                    name = article.Name,
                    unit = article.Unit,
                    salesPrice = article.SalesPrice,
                    category = article.Category,
                    brand = article.Brand,
                    supplier = article.Supplier,
                    stockQuantity = article.StockQuantity,
                    photoPath = article.PhotoPath,
                    size = article.Size,
                    color = article.Color,
                    photoUrl = (string)null
                });
            }

            var json = JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);
        }

        private async Task<string> SaveBase64ImageAsync(int productId, string base64Data)
        {
            try
            {
                // Extract base64 content
                var base64 = base64Data.Substring(base64Data.IndexOf(',') + 1);
                var imageBytes = Convert.FromBase64String(base64);

                // Determine file extension from data URL
                var extension = ".jpg";
                if (base64Data.Contains("image/png")) extension = ".png";
                else if (base64Data.Contains("image/gif")) extension = ".gif";
                else if (base64Data.Contains("image/webp")) extension = ".webp";

                var fileName = $"product_{productId}{extension}";
                var filePath = Path.Combine(_localPhotosPath, fileName);

                await File.WriteAllBytesAsync(filePath, imageBytes);
                return filePath;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> DownloadImageAsync(int productId, string imageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(imageUrl);
                if (!response.IsSuccessStatusCode) return null;

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var extension = contentType switch
                {
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };

                var fileName = $"product_{productId}{extension}";
                var filePath = Path.Combine(_localPhotosPath, fileName);

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, imageBytes);
                return filePath;
            }
            catch
            {
                return null;
            }
        }

        private class WebProduct
        {
            public int Id { get; set; }
            public string Barcode { get; set; } = "";
            public string Name { get; set; } = "";
            public string? PhotoUrl { get; set; }
            public string? PhotoPath { get; set; }
            public string? Photo_Url { get; set; } // Alternative snake_case from DB
            
            // Get the actual photo URL from either field
            public string? GetPhotoUrl()
            {
                return !string.IsNullOrEmpty(PhotoUrl) ? PhotoUrl : Photo_Url;
            }
        }

        // Response classes for JSON deserialization
        private class SyncApiResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public SyncStats? Stats { get; set; }
        }

        private class SyncStats
        {
            public int Synced { get; set; }
            public int Updated { get; set; }
            public int Errors { get; set; }
            public int Total { get; set; }
        }

        private class SyncPreviewResponse
        {
            public bool Success { get; set; }
            public bool PreviewMode { get; set; }
            public int TotalProducts { get; set; }
            public List<ChangeInfo>? Changes { get; set; }
            public PreviewSummary? Summary { get; set; }
        }

        private class PreviewSummary
        {
            public int NewProducts { get; set; }
            public int UpdatedProducts { get; set; }
            public int Unchanged { get; set; }
        }

        // Public result classes
        public class SyncResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public int SyncedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int ErrorCount { get; set; }
            public int TotalCount { get; set; }
        }

        public class SyncPreviewResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public int TotalProducts { get; set; }
            public int NewProducts { get; set; }
            public int UpdatedProducts { get; set; }
            public int UnchangedProducts { get; set; }
            public List<ChangeInfo> Changes { get; set; } = new();
        }

        public class ChangeInfo
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Type { get; set; }
            public List<string>? Changes { get; set; }
        }
    }
}
