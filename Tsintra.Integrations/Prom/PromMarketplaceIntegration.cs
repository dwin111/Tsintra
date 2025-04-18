using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Integrations.Prom
{
    public class PromMarketplaceIntegration : IMarketplaceIntegration
    {
        private readonly IMarketplaceClient _marketplaceClient;
        private readonly ILogger<PromMarketplaceIntegration> _logger;

        public PromMarketplaceIntegration(IMarketplaceClient marketplaceClient, ILogger<PromMarketplaceIntegration> logger)
        {
            _marketplaceClient = marketplaceClient ?? throw new ArgumentNullException(nameof(marketplaceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string MarketplaceName => "Prom.ua";

        // Product operations
        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            try
            {
                _logger.LogInformation("Отримання списку товарів з Prom.ua");
                var marketplaceProducts = await _marketplaceClient.GetProductsAsync();
                var products = new List<Product>();

                foreach (var mp in marketplaceProducts)
                {
                    var product = MapToProduct(mp);
                    products.Add(product);
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку товарів з Prom.ua");
                return new List<Product>();
            }
        }

        public async Task<Product> GetProductAsync(string marketplaceProductId)
        {
            try
            {
                _logger.LogInformation("Отримання товару з Prom.ua за ID: {MarketplaceProductId}", marketplaceProductId);
                var marketplaceProduct = await _marketplaceClient.GetProductByIdAsync(marketplaceProductId);
                
                if (marketplaceProduct == null)
                {
                    _logger.LogWarning("Товар з ID {MarketplaceProductId} не знайдено в Prom.ua", marketplaceProductId);
                    return null;
                }
                
                return MapToProduct(marketplaceProduct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні товару з Prom.ua за ID: {MarketplaceProductId}", marketplaceProductId);
                return null;
            }
        }

        public async Task<bool> UpdateProductAsync(Product product)
        {
            try
            {
                _logger.LogInformation("Оновлення товару в Prom.ua: {ProductName}", product.Name);
                
                if (string.IsNullOrEmpty(product.MarketplaceId))
                {
                    _logger.LogWarning("Не вдалося оновити товар в Prom.ua: MarketplaceId не вказано");
                    return false;
                }
                
                var marketplaceProduct = MapToMarketplaceProduct(product);
                var updatedProduct = await _marketplaceClient.UpdateProductAsync(marketplaceProduct);
                
                return updatedProduct != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні товару в Prom.ua: {ProductName}", product.Name);
                return false;
            }
        }

        public async Task<bool> CreateProductAsync(Product product)
        {
            try
            {
                _logger.LogInformation("Створення нового товару в Prom.ua: {ProductName}", product.Name);
                
                var marketplaceProduct = MapToMarketplaceProduct(product);
                var createdProduct = await _marketplaceClient.AddProductAsync(marketplaceProduct);
                
                if (createdProduct != null)
                {
                    // Оновлюємо marketplaceId у вихідному продукті
                    product.MarketplaceId = createdProduct.Id;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні товару в Prom.ua: {ProductName}", product.Name);
                return false;
            }
        }

        public async Task<bool> DeleteProductAsync(string marketplaceProductId)
        {
            try
            {
                _logger.LogInformation("Видалення товару з Prom.ua за ID: {MarketplaceProductId}", marketplaceProductId);
                await _marketplaceClient.DeleteProductAsync(marketplaceProductId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при видаленні товару з Prom.ua за ID: {MarketplaceProductId}", marketplaceProductId);
                return false;
            }
        }

        // Order operations
        public Task<IEnumerable<Order>> GetOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            _logger.LogWarning("Метод GetOrdersAsync не реалізовано для Prom.ua");
            // Реалізація для отримання замовлень наразі не доступна в PromUAClient
            return Task.FromResult<IEnumerable<Order>>(new List<Order>());
        }

        public Task<Order> GetOrderAsync(string marketplaceOrderId)
        {
            _logger.LogWarning("Метод GetOrderAsync не реалізовано для Prom.ua");
            // Реалізація для отримання замовлення наразі не доступна в PromUAClient
            return Task.FromResult<Order>(null);
        }

        public Task<bool> UpdateOrderStatusAsync(string marketplaceOrderId, OrderStatus status)
        {
            _logger.LogWarning("Метод UpdateOrderStatusAsync не реалізовано для Prom.ua");
            // Реалізація для оновлення статусу замовлення наразі не доступна в PromUAClient
            return Task.FromResult(false);
        }

        // Customer operations
        public Task<Customer> GetCustomerAsync(string marketplaceCustomerId)
        {
            _logger.LogWarning("Метод GetCustomerAsync не реалізовано для Prom.ua");
            // Реалізація для отримання клієнта наразі не доступна в PromUAClient
            return Task.FromResult<Customer>(null);
        }

        public Task<IEnumerable<Customer>> GetCustomersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            _logger.LogWarning("Метод GetCustomersAsync не реалізовано для Prom.ua");
            // Реалізація для отримання клієнтів наразі не доступна в PromUAClient
            return Task.FromResult<IEnumerable<Customer>>(new List<Customer>());
        }

        // Inventory operations
        public Task<bool> UpdateStockQuantityAsync(string marketplaceProductId, int quantity)
        {
            _logger.LogWarning("Метод UpdateStockQuantityAsync не реалізовано для Prom.ua");
            // Реалізація для оновлення кількості товарів наразі не доступна в PromUAClient
            return Task.FromResult(false);
        }

        public Task<int> GetStockQuantityAsync(string marketplaceProductId)
        {
            _logger.LogWarning("Метод GetStockQuantityAsync не реалізовано для Prom.ua");
            // Реалізація для отримання кількості товарів наразі не доступна в PromUAClient
            return Task.FromResult(0);
        }

        // Authentication and configuration
        public Task<bool> ValidateCredentialsAsync()
        {
            _logger.LogWarning("Метод ValidateCredentialsAsync не реалізовано для Prom.ua");
            // Реалізація для валідації облікових даних наразі не доступна в PromUAClient
            return Task.FromResult(true);
        }

        public Task<bool> ConfigureAsync(Dictionary<string, string> settings)
        {
            _logger.LogWarning("Метод ConfigureAsync не реалізовано для Prom.ua");
            // Реалізація для конфігурації наразі не доступна в PromUAClient
            return Task.FromResult(true);
        }

        // Helper methods
        private Product MapToProduct(Core.Models.MarketplaceProduct marketplaceProduct)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = marketplaceProduct.Name,
                Price = marketplaceProduct.Price,
                Description = marketplaceProduct.Description,
                MarketplaceId = marketplaceProduct.Id,
                MarketplaceType = MarketplaceName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Додаємо маппінг ідентифікаторів маркетплейсів
            product.MarketplaceMappings[MarketplaceName] = marketplaceProduct.Id;

            // Встановлюємо додаткові властивості з SpecificAttributes
            if (marketplaceProduct.SpecificAttributes != null)
            {
                product.MarketplaceSpecificData = new Dictionary<string, object>();
                
                foreach (var attr in marketplaceProduct.SpecificAttributes)
                {
                    product.MarketplaceSpecificData[attr.Key] = attr.Value;
                    
                    // Мапінг специфічних атрибутів до властивостей продукту
                    switch (attr.Key)
                    {
                        case "external_id":
                            product.ExternalId = attr.Value?.ToString() ?? string.Empty;
                            break;
                        case "sku":
                            product.Sku = attr.Value?.ToString();
                            break;
                        case "keywords":
                            product.Keywords = attr.Value?.ToString();
                            break;
                        case "currency":
                            product.Currency = attr.Value?.ToString();
                            break;
                        case "main_image":
                            product.MainImage = attr.Value?.ToString();
                            break;
                        case "images":
                            try {
                                product.Images = System.Text.Json.JsonSerializer.Deserialize<List<string>>(attr.Value?.ToString() ?? "[]");
                            } catch {
                                product.Images = new List<string>();
                            }
                            break;
                        case "status":
                            product.Status = attr.Value?.ToString();
                            break;
                        case "in_stock":
                            if (bool.TryParse(attr.Value?.ToString(), out bool inStock))
                            {
                                product.InStock = inStock;
                            }
                            break;
                        case "quantity_in_stock":
                            if (int.TryParse(attr.Value?.ToString(), out int quantity))
                            {
                                product.QuantityInStock = quantity;
                            }
                            break;
                        case "date_modified":
                            if (DateTime.TryParse(attr.Value?.ToString(), out DateTime dateModified))
                            {
                                product.DateModified = dateModified;
                            }
                            break;
                        case "name_multilang":
                            try {
                                product.NameMultilang = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(attr.Value?.ToString() ?? "{}");
                            } catch {
                                product.NameMultilang = new Dictionary<string, string>();
                            }
                            break;
                        case "description_multilang":
                            try {
                                product.DescriptionMultilang = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(attr.Value?.ToString() ?? "{}");
                            } catch {
                                product.DescriptionMultilang = new Dictionary<string, string>();
                            }
                            break;
                        case "group_id":
                            product.GroupId = attr.Value?.ToString();
                            break;
                        case "group_name":
                            product.GroupName = attr.Value?.ToString();
                            break;
                        case "category_id":
                            product.CategoryId = attr.Value?.ToString();
                            break;
                        case "category_caption":
                        case "category_name":
                            product.CategoryName = attr.Value?.ToString();
                            break;
                    }
                }
            }

            return product;
        }

        private Core.Models.MarketplaceProduct MapToMarketplaceProduct(Product product)
        {
            var specificAttributes = new Dictionary<string, object>();
            
            // Базові атрибути
            if (!string.IsNullOrEmpty(product.ExternalId))
                specificAttributes["external_id"] = product.ExternalId;
            
            if (!string.IsNullOrEmpty(product.Sku))
                specificAttributes["sku"] = product.Sku;
            
            if (!string.IsNullOrEmpty(product.Keywords))
                specificAttributes["keywords"] = product.Keywords;
            
            if (!string.IsNullOrEmpty(product.Currency))
                specificAttributes["currency"] = product.Currency;
            
            // Категорії та групи
            if (!string.IsNullOrEmpty(product.CategoryId))
                specificAttributes["category_id"] = product.CategoryId;
            
            if (!string.IsNullOrEmpty(product.CategoryName))
                specificAttributes["category_caption"] = product.CategoryName;
            
            if (!string.IsNullOrEmpty(product.GroupId))
                specificAttributes["group_id"] = product.GroupId;
            
            if (!string.IsNullOrEmpty(product.GroupName))
                specificAttributes["group_name"] = product.GroupName;
            
            // Зображення
            if (!string.IsNullOrEmpty(product.MainImage))
                specificAttributes["main_image"] = product.MainImage;
            
            if (product.Images != null && product.Images.Count > 0)
                specificAttributes["images"] = System.Text.Json.JsonSerializer.Serialize(product.Images);
            
            // Наявність
            if (product.QuantityInStock.HasValue)
                specificAttributes["quantity_in_stock"] = product.QuantityInStock.Value.ToString();
            
            specificAttributes["in_stock"] = product.InStock.ToString();
            
            // Багатомовність
            if (product.NameMultilang != null && product.NameMultilang.Count > 0)
                specificAttributes["name_multilang"] = System.Text.Json.JsonSerializer.Serialize(product.NameMultilang);
            
            if (product.DescriptionMultilang != null && product.DescriptionMultilang.Count > 0)
                specificAttributes["description_multilang"] = System.Text.Json.JsonSerializer.Serialize(product.DescriptionMultilang);

            return new Core.Models.MarketplaceProduct(
                Id: product.MarketplaceId ?? string.Empty,
                Name: product.Name,
                Price: product.Price,
                Description: product.Description ?? string.Empty,
                SpecificAttributes: specificAttributes
            );
        }
    }
} 