using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductRepository> _logger;

        public ProductRepository(IConfiguration configuration, ILogger<ProductRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<Product> GetByIdAsync(Guid id)
        {
            try
            {
                const string sql = @"
                    SELECT 
                        p.*,
                        pp.Name as PropertyName, pp.Value as PropertyValue, pp.Unit as PropertyUnit,
                        pi.ImageUrl,
                        pmm.MarketplaceName, pmm.MarketplaceId,
                        pnm.LanguageCode as NameLanguageCode, pnm.Name as MultilangName,
                        pdm.LanguageCode as DescriptionLanguageCode, pdm.Description as MultilangDescription
                    FROM Products p
                    LEFT JOIN ProductProperties pp ON p.Id = pp.ProductId
                    LEFT JOIN ProductImages pi ON p.Id = pi.ProductId
                    LEFT JOIN ProductMarketplaceMappings pmm ON p.Id = pmm.ProductId
                    LEFT JOIN ProductNamesMultilang pnm ON p.Id = pnm.ProductId
                    LEFT JOIN ProductDescriptionsMultilang pdm ON p.Id = pdm.ProductId
                    WHERE p.Id = @Id";

                using var connection = CreateConnection();
                var results = await connection.QueryAsync<dynamic>(sql, new { Id = id });
                
                if (!results.Any()) throw new KeyNotFoundException($"Product with ID {id} not found");

                var firstRow = results.First();
                var product = new Product
                {
                    Id = firstRow.Id,
                    ExternalId = firstRow.ExternalId,
                    Name = firstRow.Name,
                    Sku = firstRow.Sku,
                    Keywords = firstRow.Keywords,
                    Price = firstRow.Price,
                    OldPrice = firstRow.OldPrice,
                    Currency = firstRow.Currency,
                    Description = firstRow.Description,
                    MainImage = firstRow.MainImage,
                    Status = firstRow.Status,
                    QuantityInStock = firstRow.QuantityInStock,
                    InStock = firstRow.InStock,
                    CreatedAt = firstRow.CreatedAt,
                    UpdatedAt = firstRow.UpdatedAt,
                    DateModified = firstRow.DateModified,
                    IsVariant = firstRow.IsVariant,
                    VariantGroupId = firstRow.VariantGroupId,
                    ParentProductId = firstRow.ParentProductId,
                    CategoryId = firstRow.CategoryId,
                    CategoryName = firstRow.CategoryName,
                    GroupId = firstRow.GroupId,
                    GroupName = firstRow.GroupName
                };

                // Збираємо унікальні властивості
                product.Properties = results
                    .Where(r => r.PropertyName != null)
                    .Select(r => new ProductProperty
                    {
                        Name = (string)r.PropertyName,
                        Value = (string)r.PropertyValue,
                        Unit = (string)r.PropertyUnit
                    })
                    .Distinct()
                    .ToList();

                // Збираємо унікальні зображення
                product.Images = results
                    .Where(r => r.ImageUrl != null)
                    .Select(r => (string)r.ImageUrl)
                    .Distinct()
                    .ToList();

                // Збираємо унікальні маппінги маркетплейсів
                product.MarketplaceMappings = results
                    .Where(r => r.MarketplaceName != null)
                    .Select(r => new { Name = (string)r.MarketplaceName, Id = (string)r.MarketplaceId })
                    .Distinct()
                    .ToDictionary(x => x.Name, x => x.Id);

                // Збираємо унікальні багатомовні назви
                product.NameMultilang = results
                    .Where(r => r.NameLanguageCode != null)
                    .Select(r => new { Code = (string)r.NameLanguageCode, Name = (string)r.MultilangName })
                    .Distinct()
                    .ToDictionary(x => x.Code, x => x.Name);

                // Збираємо унікальні багатомовні описи
                product.DescriptionMultilang = results
                    .Where(r => r.DescriptionLanguageCode != null)
                    .Select(r => new { Code = (string)r.DescriptionLanguageCode, Description = (string)r.MultilangDescription })
                    .Distinct()
                    .ToDictionary(x => x.Code, x => x.Description);

                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by ID {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            try
            {
                const string sql = "SELECT * FROM Products";
                using var connection = CreateConnection();
                return await connection.QueryAsync<Product>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products");
                throw;
            }
        }

        public async Task<Guid> CreateAsync(Product product)
        {
            try
            {
                const string sql = @"
                    INSERT INTO Products (Id, ExternalId, Name, Sku, Keywords, Price, OldPrice, Currency, 
                        Description, MainImage, Status, QuantityInStock, InStock, CreatedAt, UpdatedAt, 
                        DateModified, IsVariant, VariantGroupId, ParentProductId, CategoryId, CategoryName, 
                        GroupId, GroupName)
                    VALUES (@Id, @ExternalId, @Name, @Sku, @Keywords, @Price, @OldPrice, @Currency, 
                        @Description, @MainImage, @Status, @QuantityInStock, @InStock, @CreatedAt, @UpdatedAt, 
                        @DateModified, @IsVariant, @VariantGroupId, @ParentProductId, @CategoryId, @CategoryName, 
                        @GroupId, @GroupName)
                    RETURNING Id;";

                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;

                using var connection = CreateConnection();
                var id = await connection.QuerySingleAsync<Guid>(sql, product);
                
                // Save related data
                await SaveProductProperties(product);
                await SaveProductImages(product);
                await SaveProductMarketplaceMappings(product);
                await SaveProductMultilangData(product);

                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                throw;
            }
        }

        public async Task UpdateAsync(Product product)
        {
            try
            {
                const string sql = @"
                    UPDATE Products 
                    SET ExternalId = @ExternalId, Name = @Name, Sku = @Sku, Keywords = @Keywords, 
                        Price = @Price, OldPrice = @OldPrice, Currency = @Currency, Description = @Description, 
                        MainImage = @MainImage, Status = @Status, QuantityInStock = @QuantityInStock, 
                        InStock = @InStock, UpdatedAt = @UpdatedAt, DateModified = @DateModified, 
                        IsVariant = @IsVariant, VariantGroupId = @VariantGroupId, ParentProductId = @ParentProductId, 
                        CategoryId = @CategoryId, CategoryName = @CategoryName, GroupId = @GroupId, GroupName = @GroupName
                    WHERE Id = @Id;";

                product.UpdatedAt = DateTime.UtcNow;

                using var connection = CreateConnection();
                await connection.ExecuteAsync(sql, product);
                
                // Update related data
                await SaveProductProperties(product);
                await SaveProductImages(product);
                await SaveProductMarketplaceMappings(product);
                await SaveProductMultilangData(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {Id}", product.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            try
            {
                const string sql = "DELETE FROM Products WHERE Id = @Id";
                using var connection = CreateConnection();
                await connection.ExecuteAsync(sql, new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {Id}", id);
                throw;
            }
        }

        public async Task<Product?> GetByMarketplaceIdAsync(string marketplaceId, string marketplaceType)
        {
            try
            {
                const string sql = @"
                    SELECT p.* FROM Products p
                    JOIN ProductMarketplaceMappings pm ON p.Id = pm.ProductId
                    WHERE pm.MarketplaceId = @MarketplaceId AND pm.MarketplaceName = @MarketplaceType;";

                using var connection = CreateConnection();
                return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { MarketplaceId = marketplaceId, MarketplaceType = marketplaceType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by marketplace id {MarketplaceId}", marketplaceId);
                throw;
            }
        }

        public async Task<IEnumerable<Product>> GetByMarketplaceTypeAsync(string marketplaceType)
        {
            try
            {
                const string sql = @"
                    SELECT p.* FROM Products p
                    JOIN ProductMarketplaceMappings pm ON p.Id = pm.ProductId
                    WHERE pm.MarketplaceName = @MarketplaceType;";

                using var connection = CreateConnection();
                return await connection.QueryAsync<Product>(sql, new { MarketplaceType = marketplaceType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by marketplace type {MarketplaceType}", marketplaceType);
                throw;
            }
        }

        private async Task SaveProductProperties(Product product)
        {
            if (product.Properties == null) return;

            const string deleteSql = "DELETE FROM ProductProperties WHERE ProductId = @ProductId";
            const string insertSql = @"
                INSERT INTO ProductProperties (ProductId, Name, Value, Unit)
                VALUES (@ProductId, @Name, @Value, @Unit);";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(deleteSql, new { ProductId = product.Id });

            foreach (var property in product.Properties)
            {
                await connection.ExecuteAsync(insertSql, new { ProductId = product.Id, property.Name, property.Value, property.Unit });
            }
        }

        private async Task SaveProductImages(Product product)
        {
            if (product.Images == null) return;

            const string deleteSql = "DELETE FROM ProductImages WHERE ProductId = @ProductId";
            const string insertSql = @"
                INSERT INTO ProductImages (ProductId, ImageUrl, SortOrder)
                VALUES (@ProductId, @ImageUrl, @SortOrder);";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(deleteSql, new { ProductId = product.Id });

            for (int i = 0; i < product.Images.Count; i++)
            {
                await connection.ExecuteAsync(insertSql, new { ProductId = product.Id, ImageUrl = product.Images[i], SortOrder = i });
            }
        }

        private async Task SaveProductMarketplaceMappings(Product product)
        {
            if (product.MarketplaceMappings == null) return;

            const string deleteSql = "DELETE FROM ProductMarketplaceMappings WHERE ProductId = @ProductId";
            const string insertSql = @"
                INSERT INTO ProductMarketplaceMappings (ProductId, MarketplaceName, MarketplaceId)
                VALUES (@ProductId, @MarketplaceName, @MarketplaceId);";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(deleteSql, new { ProductId = product.Id });

            foreach (var mapping in product.MarketplaceMappings)
            {
                await connection.ExecuteAsync(insertSql, new { ProductId = product.Id, MarketplaceName = mapping.Key, MarketplaceId = mapping.Value });
            }
        }

        private async Task SaveProductMultilangData(Product product)
        {
            using var connection = CreateConnection();

            if (product.NameMultilang != null)
            {
                const string deleteNamesSql = "DELETE FROM ProductNamesMultilang WHERE ProductId = @ProductId";
                const string insertNamesSql = @"
                    INSERT INTO ProductNamesMultilang (ProductId, LanguageCode, Name)
                    VALUES (@ProductId, @LanguageCode, @Name);";

                await connection.ExecuteAsync(deleteNamesSql, new { ProductId = product.Id });

                foreach (var name in product.NameMultilang)
                {
                    await connection.ExecuteAsync(insertNamesSql, new { ProductId = product.Id, LanguageCode = name.Key, Name = name.Value });
                }
            }

            if (product.DescriptionMultilang != null)
            {
                const string deleteDescriptionsSql = "DELETE FROM ProductDescriptionsMultilang WHERE ProductId = @ProductId";
                const string insertDescriptionsSql = @"
                    INSERT INTO ProductDescriptionsMultilang (ProductId, LanguageCode, Description)
                    VALUES (@ProductId, @LanguageCode, @Description);";

                await connection.ExecuteAsync(deleteDescriptionsSql, new { ProductId = product.Id });

                foreach (var description in product.DescriptionMultilang)
                {
                    await connection.ExecuteAsync(insertDescriptionsSql, new { ProductId = product.Id, LanguageCode = description.Key, Description = description.Value });
                }
            }
        }

        public async Task<IEnumerable<Product>> GetVariantsAsync(Guid productId)
        {
            _logger.LogDebug("Attempting to get variants for product ID: {ProductId}", productId);
            const string sql = @"
                SELECT p.*, pv.*, pp.*
                FROM Products p
                LEFT JOIN ProductVariants pv ON p.Id = pv.ProductId
                LEFT JOIN ProductProperties pp ON p.Id = pp.ProductId
                WHERE p.ParentProductId = @ProductId OR p.VariantGroupId = @ProductId";

            try
            {
                using var connection = CreateConnection();
                var productDictionary = new Dictionary<Guid, Product>();

                await connection.QueryAsync<Product, ProductVariant, ProductProperty, Product>(
                    sql,
                    (product, variant, property) =>
                    {
                        if (!productDictionary.TryGetValue(product.Id, out var productEntry))
                        {
                            productEntry = product;
                            productEntry.ProductVariants = new List<ProductVariant>();
                            productEntry.Properties = new List<ProductProperty>();
                            productDictionary.Add(productEntry.Id, productEntry);
                        }

                        if (variant != null && !productEntry.ProductVariants.Any(v => v.Id == variant.Id))
                        {
                            productEntry.ProductVariants.Add(variant);
                        }

                        if (property != null && !productEntry.Properties.Any(p => p.Name == property.Name))
                        {
                            productEntry.Properties.Add(property);
                        }

                        return productEntry;
                    },
                    new { ProductId = productId },
                    splitOn: "Id,Id"
                );

                _logger.LogDebug("Retrieved {Count} variants for product ID: {ProductId}", productDictionary.Count, productId);
                return productDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting variants for product ID: {ProductId}", productId);
                throw;
            }
        }

        public async Task<bool> UpdateStockAsync(Guid productId, int quantity)
        {
            _logger.LogDebug("Attempting to update stock for product ID: {ProductId} to quantity: {Quantity}", productId, quantity);
            const string sql = @"
                UPDATE Products 
                SET QuantityInStock = @Quantity,
                    InStock = @Quantity > 0,
                    DateModified = @DateModified
                WHERE Id = @Id";

            try
            {
                using var connection = CreateConnection();
                var affected = await connection.ExecuteAsync(sql, new 
                { 
                    Id = productId, 
                    Quantity = quantity,
                    DateModified = DateTime.UtcNow
                });
                _logger.LogDebug("Stock update executed for product ID: {ProductId}. Rows affected: {AffectedRows}", productId, affected);
                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for product ID: {ProductId}", productId);
                throw;
            }
        }

        public async Task<bool> UpdatePriceAsync(Guid productId, decimal price)
        {
            _logger.LogDebug("Attempting to update price for product ID: {ProductId} to price: {Price}", productId, price);
            const string sql = @"
                UPDATE Products 
                SET Price = @Price,
                    DateModified = @DateModified
                WHERE Id = @Id";

            try
            {
                using var connection = CreateConnection();
                var affected = await connection.ExecuteAsync(sql, new 
                { 
                    Id = productId, 
                    Price = price,
                    DateModified = DateTime.UtcNow
                });
                _logger.LogDebug("Price update executed for product ID: {ProductId}. Rows affected: {AffectedRows}", productId, affected);
                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating price for product ID: {ProductId}", productId);
                throw;
            }
        }

        public async Task<IEnumerable<Product>> SearchAsync(string searchTerm)
        {
            _logger.LogDebug("Attempting to search products with term: {SearchTerm}", searchTerm);
            const string sql = @"
                SELECT p.*, pv.*, pp.*
                FROM Products p
                LEFT JOIN ProductVariants pv ON p.Id = pv.ProductId
                LEFT JOIN ProductProperties pp ON p.Id = pp.ProductId
                WHERE p.Name ILIKE @SearchTerm
                   OR p.Sku ILIKE @SearchTerm
                   OR p.Description ILIKE @SearchTerm
                   OR p.Keywords ILIKE @SearchTerm";

            try
            {
                using var connection = CreateConnection();
                var productDictionary = new Dictionary<Guid, Product>();

                await connection.QueryAsync<Product, ProductVariant, ProductProperty, Product>(
                    sql,
                    (product, variant, property) =>
                    {
                        if (!productDictionary.TryGetValue(product.Id, out var productEntry))
                        {
                            productEntry = product;
                            productEntry.ProductVariants = new List<ProductVariant>();
                            productEntry.Properties = new List<ProductProperty>();
                            productDictionary.Add(productEntry.Id, productEntry);
                        }

                        if (variant != null && !productEntry.ProductVariants.Any(v => v.Id == variant.Id))
                        {
                            productEntry.ProductVariants.Add(variant);
                        }

                        if (property != null && !productEntry.Properties.Any(p => p.Name == property.Name))
                        {
                            productEntry.Properties.Add(property);
                        }

                        return productEntry;
                    },
                    new { SearchTerm = $"%{searchTerm}%" },
                    splitOn: "Id,Id"
                );

                _logger.LogDebug("Found {Count} products matching search term: {SearchTerm}", productDictionary.Count, searchTerm);
                return productDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<ProductDescriptionHistory>> GetProductHistoryAsync(int productId)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<ProductDescriptionHistory>(
                @"SELECT id, product_id, description, created_at 
                  FROM product_description_history 
                  WHERE product_id = @ProductId 
                  ORDER BY created_at DESC",
                new { ProductId = productId });
        }

        public async Task<IEnumerable<string>> GetProductHashtagsAsync(int productId)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<string>(
                @"SELECT hashtag 
                  FROM product_hashtags 
                  WHERE product_id = @ProductId",
                new { ProductId = productId });
        }

        public async Task<IEnumerable<string>> GetProductCTAsAsync(int productId)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<string>(
                @"SELECT cta 
                  FROM product_ctas 
                  WHERE product_id = @ProductId",
                new { ProductId = productId });
        }

        public async Task SaveProductDescriptionHistoryAsync(int productId, string description)
        {
            const string sql = @"
                INSERT INTO product_description_history (product_id, description)
                VALUES (@ProductId, @Description)";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, new { ProductId = productId, Description = description });
        }

        public async Task SaveProductHashtagsAsync(int productId, IEnumerable<string> hashtags)
        {
            const string sql = @"
                INSERT INTO product_hashtags (product_id, hashtag)
                VALUES (@ProductId, @Hashtag)";

            using var connection = new NpgsqlConnection(_connectionString);
            foreach (var hashtag in hashtags)
            {
                await connection.ExecuteAsync(sql, new { ProductId = productId, Hashtag = hashtag });
            }
        }

        public async Task SaveProductCTAsAsync(int productId, IEnumerable<string> ctas)
        {
            const string sql = @"
                INSERT INTO product_ctas (product_id, cta)
                VALUES (@ProductId, @CTA)";

            using var connection = new NpgsqlConnection(_connectionString);
            foreach (var cta in ctas)
            {
                await connection.ExecuteAsync(sql, new { ProductId = productId, CTA = cta });
            }
        }
    }
} 