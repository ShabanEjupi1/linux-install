using System;
using System.Collections.Generic;
using System.Linq;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Models.BMDData;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Data access service for article operations via SQL Server (Artikujt table)
    /// </summary>
    public static class ArticleDataService
    {
        // Cache for stock quantities (barcode -> quantity)
        private static DateTime _stockCacheTime = DateTime.MinValue;
        private static readonly TimeSpan StockCacheExpiry = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Get all active articles from the database with real stock quantities calculated from DitariH
        /// </summary>
        public static List<Article> GetAllArticles()
        {
            using var context = new POSDbContext();
            // Use Sasia directly from Artikujt – it is the authoritative stock field
            // maintained by both the BMD system and CashRegisterWindow sales deductions.
            return context.Artikujt.ToList().Select(MapToArticle).ToList();
        }
        
        /// <summary>
        /// Clear stock cache to force recalculation
        /// </summary>
        public static void ClearStockCache()
        {
            _stockCacheTime = DateTime.MinValue;
        }
        
        /// <summary>
        /// Search articles by barcode or name
        /// </summary>
        public static List<Article> SearchArticles(string searchText, int limit = 15)
        {
            if (string.IsNullOrEmpty(searchText))
                return new List<Article>();

            using var context = new POSDbContext();

            var artikujt = context.Artikujt
                .Where(a => EF.Functions.Like(a.Barkodi ?? "", $"%{searchText}%") || 
                           EF.Functions.Like(a.Emertimi ?? "", $"%{searchText}%"))
                .Take(limit)
                .ToList();
            return artikujt.Select(MapToArticle).ToList();
        }
        
        /// <summary>
        /// Find article by exact barcode match
        /// </summary>
        public static Article? FindByBarcode(string barcode)
        {
            if (string.IsNullOrEmpty(barcode))
                return null;

            using var context = new POSDbContext();

            var art = context.Artikujt.FirstOrDefault(a => a.Barkodi == barcode);
            return art != null ? MapToArticle(art) : null;
        }
        
        /// <summary>
        /// Find article by ID
        /// </summary>
        public static Article? FindById(int id)
        {
            using var context = new POSDbContext();

            var art = context.Artikujt.FirstOrDefault(a => a.Id == id);
            return art != null ? MapToArticle(art) : null;
        }
        
        /// <summary>
        /// Save or update an article
        /// </summary>
        public static bool SaveArticle(Article article)
        {
            try
            {
                using var context = new POSDbContext();
                
                var art = article.Id > 0 
                    ? context.Artikujt.FirstOrDefault(a => a.Id == article.Id)
                    : null;
                
                if (art == null)
                {
                    // Create new
                    art = MapToArtikujt(article);
                    context.Artikujt.Add(art);
                }
                else
                {
                    // Update existing
                    UpdateArtikujtFromArticle(art, article);
                }
                
                context.SaveChanges();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Delete (or deactivate) an article
        /// </summary>
        public static bool DeleteArticle(int id)
        {
            try
            {
                using var context = new POSDbContext();
                
                var art = context.Artikujt.FirstOrDefault(a => a.Id == id);
                if (art != null)
                {
                    context.Artikujt.Remove(art);
                    context.SaveChanges();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Update stock quantity
        /// </summary>
        public static bool UpdateStock(int articleId, decimal quantity, bool isDecrease = true)
        {
            try
            {
                using var context = new POSDbContext();
                
                var art = context.Artikujt.FirstOrDefault(a => a.Id == articleId);
                if (art != null)
                {
                    if (isDecrease)
                    {
                        art.Sasia = (art.Sasia ?? 0) - (double)quantity;
                        art.SasiaDalje = (art.SasiaDalje ?? 0) + (double)quantity;
                    }
                    else
                    {
                        art.Sasia = (art.Sasia ?? 0) + (double)quantity;
                        art.SasiaHyrje = (art.SasiaHyrje ?? 0) + (double)quantity;
                    }
                    context.SaveChanges();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Get distinct categories
        /// </summary>
        public static List<string> GetCategories()
        {
            using var context = new POSDbContext();
            
            return context.Artikujt
                .Where(a => a.Kategoria != null)
                .Select(a => a.Kategoria!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }
        
        /// <summary>
        /// Get article count
        /// </summary>
        public static int GetArticleCount()
        {
            using var context = new POSDbContext();
            
            return context.Artikujt.Count();
        }
        
        #region Mapping Methods
        
        /// <summary>
        /// Map Artikujt (SQL Server) to Article (legacy model) with calculated stock
        /// </summary>
        private static Article MapToArticleWithStock(Artikujt art, Dictionary<string, decimal> stockDict)
        {
            // Get stock from calculated dictionary (from DitariH purchases - DitariD sales)
            decimal calculatedStock = 0;
            if (!string.IsNullOrEmpty(art.Barkodi) && stockDict.TryGetValue(art.Barkodi, out var stock))
            {
                calculatedStock = stock;
            }
            
            return new Article
            {
                Id = (int)art.Id,
                Barcode = art.Barkodi ?? "",
                Name = art.Emertimi ?? "",
                Unit = art.NjesiaP ?? "Copë",
                SalesUnit = art.NjesiaSH,
                Pack = (decimal)(art.Paketimi ?? 1),
                PurchasePrice = (decimal)(art.CFurnizimit ?? 0),
                Margin = (decimal)(art.Marzha ?? 0),
                PackagePrice = (decimal)(art.CPaketimit ?? 0),
                WholesalePrice = (decimal)(art.CShumices ?? 0),
                SalesPrice = (decimal)(art.CShitjes ?? 0),
                SalesPrice1 = (decimal)(art.CShitjes1 ?? 0),
                VATRate = (decimal)(art.Tatimi ?? (art.Vat == 3 ? 18 : art.Vat == 2 ? 8 : 0)),
                VATType = art.Vat ?? 3,
                Category = art.Kategoria,
                Supplier = null, // Would need join to get supplier name
                SupplierId = art.Furnitori ?? 0,
                StockQuantity = calculatedStock, // Use calculated stock from DitariH - DitariD
                StockIn = (decimal)(art.SasiaHyrje ?? 0),
                StockOut = (decimal)(art.SasiaDalje ?? 0),
                AverageSalesPrice = (decimal)(art.CMesatarShites ?? 0),
                AveragePurchasePrice = (decimal)(art.CMesatarFurnizues ?? 0),
                MinimumStock = (decimal)(art.StoguMinimal ?? 0),
                ExpiryDate = art.Afati,
                HasBarcode = art.PaBarkod != "Y",
                IsWeighed = art.Peshore ?? false,
                IsActive = art.IsActiveProp ?? true,
                ProductType = art.Tipi ?? 1,
                POSCategoryId = art.KategoriaPosId,
                Notes = art.Verejtje,
                Location = art.Vendi,
                Brand = art.Prodhuesi,
                Importer = art.Importuesi,
                Sector = art.Sektori,
                Branch = art.Filiala?.ToString()
            };
        }
        
        /// <summary>
        /// Map Artikujt (SQL Server) to Article (legacy model) - uses Artikujt.Sasia directly
        /// </summary>
        private static Article MapToArticle(Artikujt art)
        {
            return new Article
            {
                Id = (int)art.Id,
                Barcode = art.Barkodi ?? "",
                Name = art.Emertimi ?? "",
                Unit = art.NjesiaP ?? "Copë",
                SalesUnit = art.NjesiaSH,
                Pack = (decimal)(art.Paketimi ?? 1),
                PurchasePrice = (decimal)(art.CFurnizimit ?? 0),
                Margin = (decimal)(art.Marzha ?? 0),
                PackagePrice = (decimal)(art.CPaketimit ?? 0),
                WholesalePrice = (decimal)(art.CShumices ?? 0),
                SalesPrice = (decimal)(art.CShitjes ?? 0),
                SalesPrice1 = (decimal)(art.CShitjes1 ?? 0),
                VATRate = (decimal)(art.Tatimi ?? (art.Vat == 3 ? 18 : art.Vat == 2 ? 8 : 0)),
                VATType = art.Vat ?? 3,
                Category = art.Kategoria,
                Supplier = null, // Would need join to get supplier name
                SupplierId = art.Furnitori ?? 0,
                StockQuantity = (decimal)(art.Sasia ?? 0),
                StockIn = (decimal)(art.SasiaHyrje ?? 0),
                StockOut = (decimal)(art.SasiaDalje ?? 0),
                AverageSalesPrice = (decimal)(art.CMesatarShites ?? 0),
                AveragePurchasePrice = (decimal)(art.CMesatarFurnizues ?? 0),
                MinimumStock = (decimal)(art.StoguMinimal ?? 0),
                ExpiryDate = art.Afati,
                HasBarcode = art.PaBarkod != "Y",
                IsWeighed = art.Peshore ?? false,
                IsActive = art.IsActiveProp ?? true,
                ProductType = art.Tipi ?? 1,
                POSCategoryId = art.KategoriaPosId,
                Notes = art.Verejtje,
                Location = art.Vendi,
                Brand = art.Prodhuesi,
                Importer = art.Importuesi,
                Sector = art.Sektori,
                Branch = art.Filiala?.ToString()
            };
        }
        
        /// <summary>
        /// Map Article (legacy model) to Artikujt (SQL Server)
        /// </summary>
        private static Artikujt MapToArtikujt(Article article)
        {
            return new Artikujt
            {
                Barkodi = article.Barcode ?? "",
                Emertimi = article.Name ?? "",
                NjesiaP = article.Unit,
                NjesiaSH = article.SalesUnit,
                Paketimi = (double)article.Pack,
                CFurnizimit = (double)article.PurchasePrice,
                Marzha = (double)article.Margin,
                CPaketimit = (double)article.PackagePrice,
                CShumices = (double)article.WholesalePrice,
                CShitjes = (double)article.SalesPrice,
                CShitjes1 = (double)article.SalesPrice1,
                Tatimi = (double)article.VATRate,
                Vat = article.VATType,
                Kategoria = article.Category,
                Furnitori = article.SupplierId > 0 ? article.SupplierId : null,
                Sasia = (double)article.StockQuantity,
                SasiaHyrje = (double)article.StockIn,
                SasiaDalje = (double)article.StockOut,
                CMesatarShites = (double)article.AverageSalesPrice,
                CMesatarFurnizues = (double)article.AveragePurchasePrice,
                Afati = article.ExpiryDate,
                PaBarkod = article.HasBarcode ? "N" : "Y",
                Peshore = article.IsWeighed,
                Tipi = article.ProductType,
                KategoriaPosId = article.POSCategoryId,
                Verejtje = article.Notes,
                Vendi = article.Location,
                Prodhuesi = article.Brand,
                Importuesi = article.Importer,
                Sektori = article.Sector,
                IsActiveProp = article.IsActive,
                StoguMinimal = (double)article.MinimumStock
            };
        }
        
        /// <summary>
        /// Update existing Artikujt from Article
        /// </summary>
        private static void UpdateArtikujtFromArticle(Artikujt art, Article article)
        {
            art.Barkodi = article.Barcode ?? "";
            art.Emertimi = article.Name ?? "";
            art.NjesiaP = article.Unit;
            art.NjesiaSH = article.SalesUnit;
            art.Paketimi = (double)article.Pack;
            art.CFurnizimit = (double)article.PurchasePrice;
            art.Marzha = (double)article.Margin;
            art.CPaketimit = (double)article.PackagePrice;
            art.CShumices = (double)article.WholesalePrice;
            art.CShitjes = (double)article.SalesPrice;
            art.CShitjes1 = (double)article.SalesPrice1;
            art.Tatimi = (double)article.VATRate;
            art.Vat = article.VATType;
            art.Kategoria = article.Category;
            art.Furnitori = article.SupplierId > 0 ? article.SupplierId : null;
            art.Sasia = (double)article.StockQuantity;
            art.Afati = article.ExpiryDate;
            art.PaBarkod = article.HasBarcode ? "N" : "Y";
            art.Peshore = article.IsWeighed;
            art.Tipi = article.ProductType;
            art.KategoriaPosId = article.POSCategoryId;
            art.Verejtje = article.Notes;
            art.Vendi = article.Location;
            art.Prodhuesi = article.Brand;
            art.Importuesi = article.Importer;
            art.Sektori = article.Sector;
            art.IsActiveProp = article.IsActive;
            art.StoguMinimal = (double)article.MinimumStock;
        }
        
        #endregion
    }
}
