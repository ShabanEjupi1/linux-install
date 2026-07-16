using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace KosovaPOS.Database
{
    /// <summary>
    /// Design-time factory for POSDbContext to enable migrations
    /// This allows EF Core tools like Add-Migration and Update-Database to work properly
    /// </summary>
    public class POSDbContextFactory : IDesignTimeDbContextFactory<POSDbContext>
    {
        public POSDbContext CreateDbContext(string[] args)
        {
            // Load environment variables
            try
            {
                var envPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
                if (System.IO.File.Exists(envPath))
                {
                    DotNetEnv.Env.Load(envPath);
                }
            }
            catch
            {
                // .env file is optional
            }
            
            // Get connection string from environment or use default
            var server = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "(localdb)\\MSSQLLocalDB";
            var database = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "BMDData";
            var connectionString = $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30;";
            
            var optionsBuilder = new DbContextOptionsBuilder<POSDbContext>();
            optionsBuilder.UseSqlServer(connectionString, options =>
            {
                options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                options.CommandTimeout(60);
                // Specify migrations assembly to ensure migrations are found
                options.MigrationsAssembly("KosovaPOS");
            });
            
            return new POSDbContext(optionsBuilder.Options);
        }
    }
}
