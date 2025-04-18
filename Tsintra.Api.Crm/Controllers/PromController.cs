using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Api.Crm.Services;
using Tsintra.Api.Crm.Models;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
//    [Authorize]
    public class PromController : ControllerBase
    {
        private readonly IPromService _promService;
        private readonly IProductRepository _productRepository;
        private readonly ILogger<PromController> _logger;

        public PromController(
            IPromService promService, 
            IProductRepository productRepository,
            ILogger<PromController> logger)
        {
            _promService = promService;
            _productRepository = productRepository;
            _logger = logger;
        }

        #region Product endpoints

        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving products from Prom.ua", userInfo?.Id);
                
                var products = await _promService.GetProductsAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products from Prom.ua");
                return StatusCode(500, "An error occurred while retrieving products from Prom.ua");
            }
        }

        [HttpGet("products/{marketplaceProductId}")]
        public async Task<ActionResult<Product>> GetProductByMarketplaceId(string marketplaceProductId)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving product from Prom.ua with ID: {MarketplaceProductId}", 
                    userInfo?.Id, marketplaceProductId);
                
                var product = await _promService.GetProductByMarketplaceIdAsync(marketplaceProductId);
                
                if (product == null)
                {
                    return NotFound();
                }
                
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product from Prom.ua with ID: {MarketplaceProductId}", marketplaceProductId);
                return StatusCode(500, "An error occurred while retrieving the product from Prom.ua");
            }
        }

        [HttpPost("products")]
        public async Task<ActionResult<Product>> CreateProduct([FromBody] Tsintra.Api.Crm.Models.Prom.PromProductRequest productRequest)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} creating product in Prom.ua: {ProductName}", 
                    userInfo?.Id, productRequest?.Product?.Name);
                
                if (productRequest?.Product == null)
                {
                    _logger.LogWarning("Product creation failed: The product field is required");
                    return BadRequest(new { error = "The product field is required." });
                }
                
                // Конвертуємо дані з PromProductRequest в Product
                var product = new Product
                {
                    Name = productRequest.Product.Name,
                    Price = productRequest.Product.Price,
                    Description = productRequest.Product.Description,
                    Sku = productRequest.Product.Sku,
                    Currency = productRequest.Product.Currency ?? "UAH",
                    Keywords = productRequest.Product.Keywords,
                    QuantityInStock = productRequest.Product.QuantityInStock,
                    MainImage = productRequest.Product.MainImage,
                    Images = productRequest.Product.Images,
                    NameMultilang = productRequest.Product.NameMultilang,
                    DescriptionMultilang = productRequest.Product.DescriptionMultilang,
                    Status = productRequest.Product.Status ?? "on_display"
                };
                
                // Ініціалізуємо MarketplaceSpecificData, якщо він null
                if (product.MarketplaceSpecificData == null)
                {
                    product.MarketplaceSpecificData = new Dictionary<string, object>();
                }
                
                // Додатково встановлюємо GroupId, якщо він вказаний
                if (productRequest.Product.GroupId.HasValue)
                {
                    product.GroupId = productRequest.Product.GroupId.ToString();
                    product.MarketplaceSpecificData["group_id"] = productRequest.Product.GroupId;
                }
                
                // Додатково встановлюємо CategoryId, якщо він вказаний
                if (productRequest.Product.CategoryId.HasValue)
                {
                    product.CategoryId = productRequest.Product.CategoryId.ToString();
                    product.MarketplaceSpecificData["category_id"] = productRequest.Product.CategoryId;
                }
                
                // Додатково встановлюємо Presence, якщо він вказаний
                if (!string.IsNullOrEmpty(productRequest.Product.Presence))
                {
                    product.MarketplaceSpecificData["presence"] = productRequest.Product.Presence;
                }
                
                // Додатково встановлюємо MeasureUnit, якщо він вказаний
                if (!string.IsNullOrEmpty(productRequest.Product.MeasureUnit))
                {
                    product.MarketplaceSpecificData["measure_unit"] = productRequest.Product.MeasureUnit;
                }
                
                // Додатково встановлюємо Discount, якщо він вказаний
                if (productRequest.Product.Discount.HasValue)
                {
                    product.MarketplaceSpecificData["discount"] = productRequest.Product.Discount.Value;
                }
                
                // Додатково встановлюємо MinimumOrderQuantity, якщо він вказаний
                if (productRequest.Product.MinimumOrderQuantity.HasValue)
                {
                    product.MarketplaceSpecificData["minimum_order_quantity"] = productRequest.Product.MinimumOrderQuantity.Value;
                }
                
                // Додатково встановлюємо ExternalId, якщо він вказаний
                if (!string.IsNullOrEmpty(productRequest.Product.ExternalId))
                {
                    product.ExternalId = productRequest.Product.ExternalId;
                }
                
                // Додатково встановлюємо дані про варіації, якщо вони вказані
                if (productRequest.Product.IsVariation.HasValue)
                {
                    product.IsVariant = productRequest.Product.IsVariation.Value;
                    product.MarketplaceSpecificData["is_variation"] = productRequest.Product.IsVariation.Value;
                    
                    if (productRequest.Product.VariationBaseId.HasValue)
                    {
                        product.MarketplaceSpecificData["variation_base_id"] = productRequest.Product.VariationBaseId.Value;
                    }
                    
                    if (productRequest.Product.VariationGroupId.HasValue)
                    {
                        product.MarketplaceSpecificData["variation_group_id"] = productRequest.Product.VariationGroupId.Value;
                    }
                }
                
                _logger.LogInformation("Converting PromProductRequest to Product for creation. Product name: {ProductName}", product.Name);
                
                // Створюємо продукт через сервіс
                var createdProduct = await _promService.CreateProductAsync(product);
                
                // Перевіряємо, чи було успішно отримано marketplaceId
                if (string.IsNullOrEmpty(createdProduct.MarketplaceId))
                {
                    _logger.LogError("Failed to create product in Prom.ua: No marketplace ID returned");
                    return StatusCode(500, new { error = "Failed to create product in Prom.ua: No marketplace ID returned" });
                }
                
                _logger.LogInformation("Product created successfully in Prom.ua. Marketplace ID: {MarketplaceId}", createdProduct.MarketplaceId);
                
                return CreatedAtAction(
                    nameof(GetProductByMarketplaceId), 
                    new { marketplaceProductId = createdProduct.MarketplaceId }, 
                    createdProduct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product in Prom.ua: {ErrorMessage}", ex.Message);
                return StatusCode(500, new { error = "An error occurred while creating the product in Prom.ua", details = ex.Message });
            }
        }

        [HttpPut("products/{marketplaceProductId}")]
        public async Task<IActionResult> UpdateProduct(string marketplaceProductId, [FromBody] Tsintra.Api.Crm.Models.Prom.PromProductRequest productRequest)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} updating product in Prom.ua with ID: {MarketplaceProductId}", 
                    userInfo?.Id, marketplaceProductId);
                
                if (productRequest?.Product == null)
                {
                    _logger.LogWarning("Product update failed: The product field is required");
                    return BadRequest(new { error = "The product field is required." });
                }
                
                // Спершу отримаємо існуючий продукт, щоб не втратити дані
                var existingProduct = await _promService.GetProductByMarketplaceIdAsync(marketplaceProductId);
                
                if (existingProduct == null)
                {
                    _logger.LogWarning("Product with ID {MarketplaceProductId} not found in Prom.ua", marketplaceProductId);
                    return NotFound(new { error = $"Product with ID {marketplaceProductId} not found in Prom.ua" });
                }
                
                // Конвертуємо дані з PromProductRequest в Product, зберігаючи існуючі дані
                existingProduct.Name = productRequest.Product.Name;
                existingProduct.Price = productRequest.Product.Price;
                existingProduct.Description = productRequest.Product.Description;
                existingProduct.Sku = productRequest.Product.Sku;
                existingProduct.Currency = productRequest.Product.Currency ?? existingProduct.Currency ?? "UAH";
                existingProduct.Keywords = productRequest.Product.Keywords;
                existingProduct.QuantityInStock = productRequest.Product.QuantityInStock;
                existingProduct.MainImage = productRequest.Product.MainImage;
                existingProduct.Images = productRequest.Product.Images;
                existingProduct.NameMultilang = productRequest.Product.NameMultilang;
                existingProduct.DescriptionMultilang = productRequest.Product.DescriptionMultilang;
                existingProduct.Status = productRequest.Product.Status ?? "on_display";
                
                // Ініціалізуємо MarketplaceSpecificData, якщо він null
                if (existingProduct.MarketplaceSpecificData == null)
                {
                    existingProduct.MarketplaceSpecificData = new Dictionary<string, object>();
                }
                
                // Додатково встановлюємо GroupId, якщо він вказаний
                if (productRequest.Product.GroupId.HasValue)
                {
                    existingProduct.GroupId = productRequest.Product.GroupId.ToString();
                    existingProduct.MarketplaceSpecificData["group_id"] = productRequest.Product.GroupId;
                }
                
                // Додатково встановлюємо CategoryId, якщо він вказаний
                if (productRequest.Product.CategoryId.HasValue)
                {
                    existingProduct.CategoryId = productRequest.Product.CategoryId.ToString();
                    existingProduct.MarketplaceSpecificData["category_id"] = productRequest.Product.CategoryId;
                }
                
                // Додатково встановлюємо Presence, якщо він вказаний
                if (!string.IsNullOrEmpty(productRequest.Product.Presence))
                {
                    existingProduct.MarketplaceSpecificData["presence"] = productRequest.Product.Presence;
                }
                
                // Додатково встановлюємо MeasureUnit, якщо він вказаний
                if (!string.IsNullOrEmpty(productRequest.Product.MeasureUnit))
                {
                    existingProduct.MarketplaceSpecificData["measure_unit"] = productRequest.Product.MeasureUnit;
                }
                
                // Додатково встановлюємо Discount, якщо він вказаний
                if (productRequest.Product.Discount.HasValue)
                {
                    existingProduct.MarketplaceSpecificData["discount"] = productRequest.Product.Discount.Value;
                }
                
                // Додатково встановлюємо MinimumOrderQuantity, якщо він вказаний
                if (productRequest.Product.MinimumOrderQuantity.HasValue)
                {
                    existingProduct.MarketplaceSpecificData["minimum_order_quantity"] = productRequest.Product.MinimumOrderQuantity.Value;
                }
                
                // Додатково встановлюємо ExternalId, якщо він вказаний
                if (!string.IsNullOrEmpty(productRequest.Product.ExternalId))
                {
                    existingProduct.ExternalId = productRequest.Product.ExternalId;
                }
                
                // Додатково встановлюємо дані про варіації, якщо вони вказані
                if (productRequest.Product.IsVariation.HasValue)
                {
                    existingProduct.IsVariant = productRequest.Product.IsVariation.Value;
                    existingProduct.MarketplaceSpecificData["is_variation"] = productRequest.Product.IsVariation.Value;
                    
                    if (productRequest.Product.VariationBaseId.HasValue)
                    {
                        existingProduct.MarketplaceSpecificData["variation_base_id"] = productRequest.Product.VariationBaseId.Value;
                    }
                    
                    if (productRequest.Product.VariationGroupId.HasValue)
                    {
                        existingProduct.MarketplaceSpecificData["variation_group_id"] = productRequest.Product.VariationGroupId.Value;
                    }
                }
                
                _logger.LogInformation("Converting PromProductRequest to Product for update. Product name: {ProductName}", existingProduct.Name);
                
                var updated = await _promService.UpdateProductAsync(existingProduct);
                
                if (!updated)
                {
                    _logger.LogWarning("Failed to update product with ID {MarketplaceProductId} in Prom.ua", marketplaceProductId);
                    return StatusCode(500, new { error = "Failed to update product in Prom.ua" });
                }
                
                _logger.LogInformation("Product updated successfully in Prom.ua. Marketplace ID: {MarketplaceId}", marketplaceProductId);
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product in Prom.ua with ID: {MarketplaceProductId}. Error: {ErrorMessage}", 
                    marketplaceProductId, ex.Message);
                return StatusCode(500, new { error = "An error occurred while updating the product in Prom.ua", details = ex.Message });
            }
        }

        [HttpDelete("products/{marketplaceProductId}")]
        public async Task<IActionResult> DeleteProduct(string marketplaceProductId)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} deleting product from Prom.ua with ID: {MarketplaceProductId}", 
                    userInfo?.Id, marketplaceProductId);
                
                var deleted = await _promService.DeleteProductAsync(marketplaceProductId);
                
                if (!deleted)
                {
                    return NotFound();
                }
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product from Prom.ua with ID: {MarketplaceProductId}", marketplaceProductId);
                return StatusCode(500, "An error occurred while deleting the product from Prom.ua");
            }
        }

        [HttpPost("products/import")]
        public async Task<ActionResult<int>> ImportProducts()
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} importing products from Prom.ua", userInfo?.Id);
                
                var importCount = await _promService.ImportProductsAsync();
                
                return Ok(new { count = importCount, message = $"Successfully imported {importCount} products from Prom.ua" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing products from Prom.ua");
                return StatusCode(500, "An error occurred while importing products from Prom.ua");
            }
        }

        [HttpPost("products/export")]
        public async Task<ActionResult<int>> ExportProducts([FromBody] IEnumerable<Guid> productIds = null)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} exporting products to Prom.ua", userInfo?.Id);
                
                var exportCount = await _promService.ExportProductsAsync(productIds);
                
                return Ok(new { count = exportCount, message = $"Successfully exported {exportCount} products to Prom.ua" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting products to Prom.ua");
                return StatusCode(500, "An error occurred while exporting products to Prom.ua");
            }
        }

        [HttpPut("products/{marketplaceProductId}/inventory")]
        public async Task<IActionResult> SyncProductInventory(string marketplaceProductId, [FromQuery] int quantity)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} syncing product inventory with Prom.ua: Product ID {MarketplaceProductId}, Quantity {Quantity}", 
                    userInfo?.Id, marketplaceProductId, quantity);
                
                var updated = await _promService.SyncProductInventoryAsync(marketplaceProductId, quantity);
                
                if (!updated)
                {
                    return NotFound();
                }
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing product inventory with Prom.ua: Product ID {MarketplaceProductId}", marketplaceProductId);
                return StatusCode(500, "An error occurred while syncing product inventory with Prom.ua");
            }
        }

        [HttpPost("products/{productId}/publish")]
        public async Task<IActionResult> PublishProduct(Guid productId)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} publishing product to Prom.ua with ID: {ProductId}", 
                    userInfo?.Id, productId);
                
                var published = await _promService.PublishProductByIdAsync(productId);
                
                if (!published)
                {
                    return NotFound("Product not found or could not be published to Prom.ua");
                }
                
                return Ok(new { success = true, message = "Product successfully published to Prom.ua" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing product to Prom.ua with ID: {ProductId}", productId);
                return StatusCode(500, "An error occurred while publishing the product to Prom.ua");
            }
        }

        [HttpGet("products/all")]
        public async Task<ActionResult<IEnumerable<Product>>> GetAllProductsFromProm()
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving all products from Prom.ua and storing in database", userInfo?.Id);
                
                // Import products from Prom.ua to the database
                var importCount = await _promService.ImportProductsAsync();
                
                // Get all products with Prom.ua marketplace type
                var products = await _productRepository.GetByMarketplaceTypeAsync("Prom.ua");
                
                return Ok(new { 
                    count = products.Count(), 
                    imported = importCount,
                    products = products 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all products from Prom.ua");
                return StatusCode(500, "An error occurred while retrieving products from Prom.ua");
            }
        }

        [HttpGet("db/products")]
        public async Task<ActionResult<IEnumerable<Product>>> GetPromProductsFromDatabase()
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving Prom.ua products from database", userInfo?.Id);
                
                // Get all products with Prom.ua marketplace type
                var products = await _productRepository.GetByMarketplaceTypeAsync("Prom.ua");
                
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Prom.ua products from database");
                return StatusCode(500, "An error occurred while retrieving Prom.ua products from database");
            }
        }

        [HttpPost("products/sync")]
        public async Task<ActionResult<object>> SyncProductsWithDatabase([FromQuery] string direction = "both", [FromBody] IEnumerable<Guid> productIds = null)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} syncing products with Prom.ua, direction: {Direction}", 
                    userInfo?.Id, direction);
                
                // Парсимо напрямок синхронізації
                SyncDirection syncDirection;
                switch (direction.ToLower())
                {
                    case "import":
                        syncDirection = SyncDirection.Import;
                        break;
                    case "export":
                        syncDirection = SyncDirection.Export;
                        break;
                    case "both":
                    default:
                        syncDirection = SyncDirection.Both;
                        break;
                }
                
                var result = await _promService.SyncProductsWithDatabaseAsync(syncDirection, productIds);
                
                return Ok(new { 
                    imported = result.Imported, 
                    exported = result.Exported, 
                    failed = result.Failed,
                    message = $"Sync completed: Imported {result.Imported}, Exported {result.Exported}, Failed {result.Failed}" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing products with Prom.ua");
                return StatusCode(500, "An error occurred while syncing products with Prom.ua");
            }
        }

        [HttpPost("products/import-from-file")]
        public async Task<ActionResult<object>> ImportProductsFromFile(IFormFile file, [FromQuery] bool publishToProm = false)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} importing products from file, PublishToProm: {PublishToProm}", 
                    userInfo?.Id, publishToProm);
                
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded or file is empty");
                }
                
                // Перевіряємо тип файлу
                string extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".csv" && extension != ".xlsx" && extension != ".xls" && extension != ".json")
                {
                    return BadRequest("Unsupported file format. Supported formats: CSV, XLSX, XLS, JSON");
                }
                
                // Створюємо тимчасовий файл
                string tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var stream = new FileStream(tempFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    
                    // Імпортуємо продукти з файлу
                    int savedCount = await _promService.SaveProductsFromFileAsync(tempFilePath);
                    
                    // Якщо вказано опцію публікації на Prom.UA, публікуємо їх
                    int publishedCount = 0;
                    if (publishToProm && savedCount > 0)
                    {
                        // Отримуємо всі продукти і публікуємо їх на Prom.UA
                        var products = await _productRepository.GetAllAsync();
                        // Беремо останні додані продукти (сортуємо за датою створення)
                        var recentProducts = products
                            .OrderByDescending(p => p.CreatedAt)
                            .Take(savedCount)
                            .ToList();
                        
                        var productIds = recentProducts.Select(p => p.Id).ToList();
                        
                        var result = await _promService.SyncProductsWithDatabaseAsync(SyncDirection.Export, productIds);
                        publishedCount = result.Exported;
                    }
                    
                    return Ok(new {
                        savedCount = savedCount,
                        publishedCount = publishedCount,
                        message = $"Successfully imported {savedCount} products from file. Published {publishedCount} products to Prom.ua."
                    });
                }
                finally
                {
                    // Видаляємо тимчасовий файл
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing products from file");
                return StatusCode(500, "An error occurred while importing products from file");
            }
        }

        #endregion

        #region Order endpoints

        [HttpGet("orders")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving orders from Prom.ua", userInfo?.Id);
                
                var orders = await _promService.GetOrdersAsync(startDate, endDate);
                
                // Використовуємо спеціальні налаштування серіалізації JSON для збереження вкладених об'єктів
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.Preserve,
                    // Важливий параметр для збереження структури Dictionary<string, object>
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                
                // Перетворюємо замовлення в JSON вручну і потім назад у об'єкт для збереження повної структури
                var jsonString = JsonSerializer.Serialize(orders, options);
                
                // Повертаємо результат як JSON відповідь
                return Content(jsonString, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders from Prom.ua");
                return StatusCode(500, "An error occurred while retrieving orders from Prom.ua");
            }
        }

        [HttpGet("orders/{orderId}")]
        public async Task<ActionResult<Order>> GetOrderById(string orderId)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving order from Prom.ua with ID: {OrderId}", 
                    userInfo?.Id, orderId);
                
                var order = await _promService.GetOrderByIdAsync(orderId);
                
                if (order == null)
                {
                    return NotFound();
                }
                
                // Використовуємо спеціальні налаштування серіалізації JSON для збереження вкладених об'єктів
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.Preserve,
                    // Важливий параметр для збереження структури Dictionary<string, object>
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                
                // Перетворюємо замовлення в JSON вручну і потім назад у об'єкт для збереження повної структури
                var jsonString = JsonSerializer.Serialize(order, options);
                
                // Повертаємо результат як JSON відповідь
                return Content(jsonString, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order from Prom.ua with ID: {OrderId}", orderId);
                return StatusCode(500, "An error occurred while retrieving the order from Prom.ua");
            }
        }

        [HttpGet("orders/raw/{orderId}")]
        public async Task<ActionResult<Models.Prom.PromOrder>> GetRawOrderById(string orderId)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving raw order from Prom.ua with ID: {OrderId}", 
                    userInfo?.Id, orderId);
                
                var order = await _promService.GetRawOrderByIdAsync(orderId);
                
                if (order == null)
                {
                    return NotFound();
                }
                
                // Використовуємо спеціальні налаштування серіалізації JSON для збереження вкладених об'єктів
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.Preserve,
                    // Важливий параметр для збереження структури Dictionary<string, object>
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                
                // Перетворюємо замовлення в JSON вручну і потім назад у об'єкт для збереження повної структури
                var jsonString = JsonSerializer.Serialize(order, options);
                
                // Повертаємо результат як JSON відповідь
                return Content(jsonString, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving raw order from Prom.ua with ID: {OrderId}", orderId);
                return StatusCode(500, "An error occurred while retrieving the raw order from Prom.ua");
            }
        }

        [HttpPost("orders/import")]
        public async Task<ActionResult<int>> ImportOrders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} importing orders from Prom.ua", userInfo?.Id);
                
                var importCount = await _promService.ImportOrdersAsync(startDate, endDate);
                
                return Ok(new { count = importCount, message = $"Successfully imported {importCount} orders from Prom.ua" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing orders from Prom.ua");
                return StatusCode(500, "An error occurred while importing orders from Prom.ua");
            }
        }

        [HttpPut("orders/{marketplaceOrderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(string marketplaceOrderId, [FromBody] OrderStatus status)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} updating order status in Prom.ua: Order ID {MarketplaceOrderId}, Status {Status}", 
                    userInfo?.Id, marketplaceOrderId, status);
                
                var updated = await _promService.UpdateOrderStatusAsync(marketplaceOrderId, status);
                
                if (!updated)
                {
                    return NotFound();
                }
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status in Prom.ua: Order ID {MarketplaceOrderId}", marketplaceOrderId);
                return StatusCode(500, "An error occurred while updating the order status in Prom.ua");
            }
        }

        #endregion

        #region Group endpoints

        [HttpGet("groups")]
        public async Task<ActionResult<IEnumerable<PromGroup>>> GetGroups()
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving groups from Prom.ua", userInfo?.Id);
                
                var groups = await _promService.GetGroupsAsync();
                return Ok(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups from Prom.ua");
                return StatusCode(500, "An error occurred while retrieving groups from Prom.ua");
            }
        }

        [HttpGet("groups/{id}")]
        public async Task<ActionResult<PromGroup>> GetGroupById(string id, [FromQuery] string language = null)
        {
            try
            {
                var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
                _logger.LogInformation("User {UserId} retrieving group with ID {GroupId} from Prom.ua", userInfo?.Id, id);
                
                var group = await _promService.GetGroupByIdAsync(id, language);
                if (group == null)
                {
                    return NotFound($"Group with ID {id} not found in Prom.ua");
                }
                
                return Ok(group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group with ID {GroupId} from Prom.ua", id);
                return StatusCode(500, $"An error occurred while retrieving group with ID {id} from Prom.ua");
            }
        }

        #endregion
    }
} 