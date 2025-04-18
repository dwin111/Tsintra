using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tsintra.Domain.Interfaces;

namespace Tsintra.Persistence.Repositories
{
    public class PromRepository : IPromRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PromRepository> _logger;

        public PromRepository(IConfiguration configuration, ILogger<PromRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        /// <summary>
        /// Saves a PromProduct with all its nested entities (Group, Category, Images)
        /// </summary>
        public async Task<long> SaveProductAsync(object product)
        {
            try
            {
                var promProduct = product as dynamic;
                if (promProduct == null)
                {
                    throw new ArgumentException("Invalid product type", nameof(product));
                }

                using var connection = CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Save Group if it exists
                    long? groupId = null;
                    if (promProduct.Group != null)
                    {
                        groupId = await SaveGroupInternalAsync(connection, transaction, promProduct.Group);
                    }

                    // Save Category if it exists
                    long? categoryId = null;
                    if (promProduct.Category != null)
                    {
                        categoryId = await SaveCategoryInternalAsync(connection, transaction, promProduct.Category);
                    }

                    // Serialize multilang dictionaries to JSON strings
                    string nameMultilangJson = null;
                    string descriptionMultilangJson = null;

                    if (promProduct.NameMultilang != null)
                    {
                        nameMultilangJson = JsonSerializer.Serialize(promProduct.NameMultilang);
                    }

                    if (promProduct.DescriptionMultilang != null)
                    {
                        descriptionMultilangJson = JsonSerializer.Serialize(promProduct.DescriptionMultilang);
                    }

                    // Save Product
                    const string sql = @"
                        INSERT INTO PromProducts 
                        (ExternalId, Name, Sku, Keywords, Presence, Price, Currency, Description, 
                         GroupId, CategoryId, MainImage, SellingType, Status, QuantityInStock, 
                         MeasureUnit, IsVariation, VariationBaseId, VariationGroupId, DateModified, InStock,
                         NameMultilangJson, DescriptionMultilangJson)
                        VALUES 
                        (@ExternalId, @Name, @Sku, @Keywords, @Presence, @Price, @Currency, @Description, 
                         @GroupId, @CategoryId, @MainImage, @SellingType, @Status, @QuantityInStock, 
                         @MeasureUnit, @IsVariation, @VariationBaseId, @VariationGroupId, @DateModified, @InStock,
                         @NameMultilangJson, @DescriptionMultilangJson)
                        RETURNING Id;";

                    var parameters = new
                    {
                        ExternalId = (string)promProduct.ExternalId?.ToString(),
                        Name = (string)promProduct.Name,
                        Sku = (string)promProduct.Sku,
                        Keywords = (string)promProduct.Keywords,
                        Presence = (string)promProduct.Presence,
                        Price = (decimal)promProduct.Price,
                        Currency = (string)promProduct.Currency,
                        Description = (string)promProduct.Description,
                        GroupId = groupId,
                        CategoryId = categoryId,
                        MainImage = (string)promProduct.MainImage,
                        SellingType = (string)promProduct.SellingType,
                        Status = (string)promProduct.Status,
                        QuantityInStock = promProduct.QuantityInStock?.ToString(),
                        MeasureUnit = (string)promProduct.MeasureUnit,
                        IsVariation = (bool)promProduct.IsVariation,
                        VariationBaseId = promProduct.VariationBaseId?.ToString(),
                        VariationGroupId = promProduct.VariationGroupId?.ToString(),
                        DateModified = (DateTime?)promProduct.DateModified,
                        InStock = (bool)promProduct.InStock,
                        NameMultilangJson = nameMultilangJson,
                        DescriptionMultilangJson = descriptionMultilangJson
                    };

                    var productId = await connection.QuerySingleAsync<long>(sql, parameters, transaction);

                    // Save images if they exist
                    if (promProduct.Images != null)
                    {
                        int order = 0;
                        foreach (var image in promProduct.Images)
                        {
                            await SaveImageInternalAsync(connection, transaction, productId, image, order++);
                        }
                    }

                    transaction.Commit();
                    return productId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Prom product");
                throw;
            }
        }

        /// <summary>
        /// Saves a PromGroup entity
        /// </summary>
        public async Task<long> SaveGroupAsync(object group)
        {
            try
            {
                using var connection = CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    var groupId = await SaveGroupInternalAsync(connection, transaction, group as dynamic);
                    transaction.Commit();
                    return groupId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Prom group");
                throw;
            }
        }

        /// <summary>
        /// Saves a PromCategory entity
        /// </summary>
        public async Task<long> SaveCategoryAsync(object category)
        {
            try
            {
                var promCategory = category as dynamic;
                if (promCategory == null)
                {
                    throw new ArgumentException("Invalid category type", nameof(category));
                }

                using var connection = CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    var categoryId = await SaveCategoryInternalAsync(connection, transaction, promCategory);
                    transaction.Commit();
                    return categoryId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Prom category");
                throw;
            }
        }

        /// <summary>
        /// Saves PromImage entities for a product
        /// </summary>
        public async Task<int> SaveImagesAsync(long productId, IEnumerable<object> images)
        {
            try
            {
                using var connection = CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    int count = 0;
                    int order = 0;
                    
                    foreach (var image in images)
                    {
                        await SaveImageInternalAsync(connection, transaction, productId, image as dynamic, order++);
                        count++;
                    }

                    transaction.Commit();
                    return count;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Prom images for product {ProductId}", productId);
                throw;
            }
        }

        /// <summary>
        /// Gets a PromProduct by its ID
        /// </summary>
        public async Task<object> GetProductByIdAsync(long id)
        {
            try
            {
                using var connection = CreateConnection();
                
                // Get product
                const string productSql = @"SELECT * FROM PromProducts WHERE Id = @Id";
                var product = await connection.QuerySingleOrDefaultAsync(productSql, new { Id = id });
                
                if (product == null)
                {
                    return null;
                }
                
                // Get group
                const string groupSql = @"SELECT * FROM PromGroups WHERE Id = @Id";
                var group = await connection.QuerySingleOrDefaultAsync(groupSql, new { Id = product.GroupId });
                
                // Get category
                const string categorySql = @"SELECT * FROM PromCategories WHERE Id = @Id";
                var category = await connection.QuerySingleOrDefaultAsync(categorySql, new { Id = product.CategoryId });
                
                // Get images
                const string imagesSql = @"SELECT * FROM PromImages WHERE ProductId = @ProductId ORDER BY SortOrder";
                var images = await connection.QueryAsync(imagesSql, new { ProductId = id });
                
                // Parse the multilang JSON to dictionaries
                var nameMultilang = new Dictionary<string, string>();
                var descriptionMultilang = new Dictionary<string, string>();
                
                if (!string.IsNullOrEmpty(product.NameMultilangJson))
                {
                    nameMultilang = JsonSerializer.Deserialize<Dictionary<string, string>>(product.NameMultilangJson);
                }
                
                if (!string.IsNullOrEmpty(product.DescriptionMultilangJson))
                {
                    descriptionMultilang = JsonSerializer.Deserialize<Dictionary<string, string>>(product.DescriptionMultilangJson);
                }
                
                // Create dynamic object with all the data
                var result = new
                {
                    product.Id,
                    product.ExternalId,
                    product.Name,
                    product.Sku,
                    product.Keywords,
                    product.Presence,
                    product.Price,
                    product.Currency,
                    product.Description,
                    Group = group,
                    Category = category,
                    product.MainImage,
                    product.SellingType,
                    product.Status,
                    product.QuantityInStock,
                    product.MeasureUnit,
                    product.IsVariation,
                    product.VariationBaseId,
                    product.VariationGroupId,
                    product.DateModified,
                    product.InStock,
                    NameMultilang = nameMultilang,
                    DescriptionMultilang = descriptionMultilang,
                    Images = images
                };
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Prom product {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Gets all PromProducts
        /// </summary>
        public async Task<IEnumerable<object>> GetAllProductsAsync()
        {
            try
            {
                using var connection = CreateConnection();
                
                // Get all products
                const string productsSql = @"SELECT Id FROM PromProducts ORDER BY Name";
                var productIds = await connection.QueryAsync<long>(productsSql);
                
                // Get complete data for each product
                var result = new List<object>();
                foreach (var id in productIds)
                {
                    var product = await GetProductByIdAsync(id);
                    result.Add(product);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all Prom products");
                throw;
            }
        }

        /// <summary>
        /// Gets a PromGroup by its ID
        /// </summary>
        public async Task<object> GetGroupByIdAsync(long id)
        {
            try
            {
                using var connection = CreateConnection();
                
                // Get group
                const string groupSql = @"SELECT * FROM PromGroups WHERE Id = @Id";
                var group = await connection.QuerySingleOrDefaultAsync(groupSql, new { Id = id });
                
                if (group == null)
                {
                    return null;
                }
                
                // Parse multilang JSON to dictionaries
                var nameMultilang = new Dictionary<string, string>();
                var descriptionMultilang = new Dictionary<string, string>();
                
                if (!string.IsNullOrEmpty(group.NameMultilangJson))
                {
                    nameMultilang = JsonSerializer.Deserialize<Dictionary<string, string>>(group.NameMultilangJson);
                }
                
                if (!string.IsNullOrEmpty(group.DescriptionMultilangJson))
                {
                    descriptionMultilang = JsonSerializer.Deserialize<Dictionary<string, string>>(group.DescriptionMultilangJson);
                }
                
                // Create dynamic object with all the data
                var result = new
                {
                    group.Id,
                    group.Name,
                    group.Description,
                    group.Image,
                    group.ParentGroupId,
                    NameMultilang = nameMultilang,
                    DescriptionMultilang = descriptionMultilang
                };
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Prom group {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Gets all PromGroups
        /// </summary>
        public async Task<IEnumerable<object>> GetAllGroupsAsync()
        {
            try
            {
                using var connection = CreateConnection();
                
                // Get all groups
                const string groupsSql = @"SELECT Id FROM PromGroups ORDER BY Name";
                var groupIds = await connection.QueryAsync<long>(groupsSql);
                
                // Get complete data for each group
                var result = new List<object>();
                foreach (var id in groupIds)
                {
                    var group = await GetGroupByIdAsync(id);
                    result.Add(group);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all Prom groups");
                throw;
            }
        }

        /// <summary>
        /// Deletes a PromProduct by its ID
        /// </summary>
        public async Task<bool> DeleteProductAsync(long id)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"DELETE FROM PromProducts WHERE Id = @Id";
                var result = await connection.ExecuteAsync(sql, new { Id = id });
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Prom product {Id}", id);
                throw;
            }
        }

        #region Helper Methods

        private async Task<long> SaveGroupInternalAsync(IDbConnection connection, IDbTransaction transaction, dynamic group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            // Serialize multilang dictionaries to JSON strings
            string nameMultilangJson = null;
            string descriptionMultilangJson = null;

            if (group.NameMultilang != null)
            {
                nameMultilangJson = JsonSerializer.Serialize(group.NameMultilang);
            }

            if (group.DescriptionMultilang != null)
            {
                descriptionMultilangJson = JsonSerializer.Serialize(group.DescriptionMultilang);
            }

            // Check if group already exists
            const string checkSql = @"SELECT Id FROM PromGroups WHERE Id = @Id";
            var existingId = await connection.QuerySingleOrDefaultAsync<long?>(checkSql, new { Id = group.Id }, transaction);
            
            if (existingId.HasValue)
            {
                // Update existing group
                const string updateSql = @"
                    UPDATE PromGroups 
                    SET Name = @Name, 
                        Description = @Description, 
                        Image = @Image, 
                        ParentGroupId = @ParentGroupId,
                        NameMultilangJson = @NameMultilangJson,
                        DescriptionMultilangJson = @DescriptionMultilangJson
                    WHERE Id = @Id";
                
                await connection.ExecuteAsync(updateSql, new
                {
                    Id = group.Id,
                    Name = (string)group.Name,
                    Description = (string)group.Description,
                    Image = (string)group.Image,
                    ParentGroupId = (long?)group.ParentGroupId,
                    NameMultilangJson = nameMultilangJson,
                    DescriptionMultilangJson = descriptionMultilangJson
                }, transaction);
                
                return group.Id;
            }
            else
            {
                // Insert new group
                const string insertSql = @"
                    INSERT INTO PromGroups 
                    (Id, Name, Description, Image, ParentGroupId, NameMultilangJson, DescriptionMultilangJson)
                    VALUES 
                    (@Id, @Name, @Description, @Image, @ParentGroupId, @NameMultilangJson, @DescriptionMultilangJson)
                    RETURNING Id";
                
                return await connection.QuerySingleAsync<long>(insertSql, new
                {
                    Id = group.Id,
                    Name = (string)group.Name,
                    Description = (string)group.Description,
                    Image = (string)group.Image,
                    ParentGroupId = (long?)group.ParentGroupId,
                    NameMultilangJson = nameMultilangJson,
                    DescriptionMultilangJson = descriptionMultilangJson
                }, transaction);
            }
        }

        private async Task<long> SaveCategoryInternalAsync(IDbConnection connection, IDbTransaction transaction, dynamic category)
        {
            // Check if category already exists by external ID
            long? externalId = category.Id;
            if (externalId.HasValue)
            {
                const string checkSql = @"SELECT Id FROM PromCategories WHERE ExternalId = @ExternalId";
                var existingId = await connection.QuerySingleOrDefaultAsync<long?>(checkSql, new { ExternalId = externalId }, transaction);
                
                if (existingId.HasValue)
                {
                    // Update existing category
                    const string updateSql = @"
                        UPDATE PromCategories 
                        SET Caption = @Caption, UpdatedAt = NOW()
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        Caption = (string)category.Caption
                    }, transaction);
                    
                    return existingId.Value;
                }
            }
            
            // Insert new category
            const string insertSql = @"
                INSERT INTO PromCategories 
                (ExternalId, Caption)
                VALUES 
                (@ExternalId, @Caption)
                RETURNING Id";
            
            var parameters = new
            {
                ExternalId = externalId,
                Caption = (string)category.Caption
            };
            
            return await connection.QuerySingleAsync<long>(insertSql, parameters, transaction);
        }

        private async Task SaveImageInternalAsync(IDbConnection connection, IDbTransaction transaction, long productId, dynamic image, int order)
        {
            // Check if image already exists
            long? externalId = image.Id;
            if (externalId.HasValue)
            {
                const string checkSql = @"SELECT Id FROM PromImages WHERE ExternalId = @ExternalId AND ProductId = @ProductId";
                var existingId = await connection.QuerySingleOrDefaultAsync<long?>(
                    checkSql, new { ExternalId = externalId, ProductId = productId }, transaction);
                
                if (existingId.HasValue)
                {
                    // Update existing image
                    const string updateSql = @"
                        UPDATE PromImages 
                        SET ThumbnailUrl = @ThumbnailUrl, Url = @Url, SortOrder = @SortOrder, UpdatedAt = NOW()
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        ThumbnailUrl = (string)image.ThumbnailUrl,
                        Url = (string)image.Url,
                        SortOrder = order
                    }, transaction);
                    
                    return;
                }
            }
            
            // Insert new image
            const string insertSql = @"
                INSERT INTO PromImages 
                (ExternalId, ProductId, ThumbnailUrl, Url, IsMain, SortOrder)
                VALUES 
                (@ExternalId, @ProductId, @ThumbnailUrl, @Url, @IsMain, @SortOrder)";
            
            var parameters = new
            {
                ExternalId = externalId,
                ProductId = productId,
                ThumbnailUrl = (string)image.ThumbnailUrl,
                Url = (string)image.Url,
                IsMain = order == 0, // First image is main
                SortOrder = order
            };
            
            await connection.ExecuteAsync(insertSql, parameters, transaction);
        }

        private async Task SaveProductMultilangAsync(IDbConnection connection, IDbTransaction transaction, 
            long productId, string languageCode, string name, string description)
        {
            // Check if entry already exists
            const string checkSql = @"
                SELECT Id FROM PromProductMultilang 
                WHERE ProductId = @ProductId AND LanguageCode = @LanguageCode";
            
            var existingId = await connection.QuerySingleOrDefaultAsync<long?>(
                checkSql, new { ProductId = productId, LanguageCode = languageCode }, transaction);
            
            if (existingId.HasValue)
            {
                // Update existing entry
                if (name != null && description != null)
                {
                    const string updateSql = @"
                        UPDATE PromProductMultilang 
                        SET Name = @Name, Description = @Description
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        Name = name,
                        Description = description
                    }, transaction);
                }
                else if (name != null)
                {
                    const string updateSql = @"
                        UPDATE PromProductMultilang 
                        SET Name = @Name
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        Name = name
                    }, transaction);
                }
                else if (description != null)
                {
                    const string updateSql = @"
                        UPDATE PromProductMultilang 
                        SET Description = @Description
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        Description = description
                    }, transaction);
                }
            }
            else
            {
                // Insert new entry
                const string insertSql = @"
                    INSERT INTO PromProductMultilang 
                    (ProductId, LanguageCode, Name, Description)
                    VALUES 
                    (@ProductId, @LanguageCode, @Name, @Description)";
                
                var parameters = new
                {
                    ProductId = productId,
                    LanguageCode = languageCode,
                    Name = name,
                    Description = description
                };
                
                await connection.ExecuteAsync(insertSql, parameters, transaction);
            }
        }

        private async Task SaveGroupMultilangAsync(IDbConnection connection, IDbTransaction transaction, 
            long groupId, string languageCode, string name, string description)
        {
            // Check if entry already exists
            const string checkSql = @"
                SELECT Id FROM PromGroupMultilang 
                WHERE GroupId = @GroupId AND LanguageCode = @LanguageCode";
            
            var existingId = await connection.QuerySingleOrDefaultAsync<long?>(
                checkSql, new { GroupId = groupId, LanguageCode = languageCode }, transaction);
            
            if (existingId.HasValue)
            {
                // Update existing entry
                if (name != null && description != null)
                {
                    const string updateSql = @"
                        UPDATE PromGroupMultilang 
                        SET Name = @Name, Description = @Description
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        Name = name,
                        Description = description
                    }, transaction);
                }
                else if (name != null)
                {
                    const string updateSql = @"
                        UPDATE PromGroupMultilang 
                        SET Name = @Name
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        Name = name
                    }, transaction);
                }
                else if (description != null)
                {
                    const string updateSql = @"
                        UPDATE PromGroupMultilang 
                        SET Description = @Description
                        WHERE Id = @Id";
                    
                    await connection.ExecuteAsync(updateSql, new
                    {
                        Id = existingId.Value,
                        Description = description
                    }, transaction);
                }
            }
            else
            {
                // Insert new entry
                const string insertSql = @"
                    INSERT INTO PromGroupMultilang 
                    (GroupId, LanguageCode, Name, Description)
                    VALUES 
                    (@GroupId, @LanguageCode, @Name, @Description)";
                
                var parameters = new
                {
                    GroupId = groupId,
                    LanguageCode = languageCode,
                    Name = name,
                    Description = description
                };
                
                await connection.ExecuteAsync(insertSql, parameters, transaction);
            }
        }

        #endregion
    }
} 