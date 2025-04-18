using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.Api.Crm.Models;
using Prom = Tsintra.Api.Crm.Models.Prom;
using System.Globalization;

namespace Tsintra.Api.Crm.Services
{
    /// <summary>
    /// Service for integration with Prom.ua API
    /// </summary>
    public class PromService : IPromService
    {
        private readonly HttpClient _httpClient;
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IPromRepository _promRepository;
        private readonly ILogger<PromService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public PromService(
            HttpClient httpClient,
            IConfiguration configuration,
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            ICustomerRepository customerRepository,
            IPromRepository promRepository,
            ILogger<PromService> logger)
        {
            _productRepository = productRepository;
            _orderRepository = orderRepository;
            _customerRepository = customerRepository;
            _promRepository = promRepository;
            _logger = logger;

            // Configure the HttpClient
            _httpClient = httpClient;
            var apiKey = configuration["PromUa:ApiKey"];
            var baseUrl = configuration["PromUa:BaseUrl"];

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("Prom.ua API key is not configured");
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentException("Prom.ua base URL is not configured");
            }

            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        #region Product Methods

        /// <inheritdoc />
        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            try
            {
                _logger.LogInformation("Getting products from Prom.ua");
                
                // Спроба отримати з бази даних
                try
                {
                    var allProducts = await _promRepository.GetAllProductsAsync();
                    if (allProducts != null && allProducts.Any())
                    {
                        _logger.LogInformation("Retrieved {Count} products from database", allProducts.Count());
                        var productsFromDb = new List<Product>();
                        
                        foreach (dynamic productDynamic in allProducts)
                        {
                            try
                            {
                                var product = MapToProduct(productDynamic);
                                productsFromDb.Add(product);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error mapping product from database");
                            }
                        }
                        
                        return productsFromDb;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving products from database, will try API");
                }
                
                // Якщо немає в базі даних, отримуємо з API
                _logger.LogInformation("Calling Prom.ua API for products");
                var response = await _httpClient.GetAsync("products/list");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get products from Prom.ua. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<Product>();
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received response from Prom API: {Length} bytes", content.Length);
                
                // Парсимо через JsonDocument для безпечної обробки різних типів
                using var document = JsonDocument.Parse(content);
                
                if (!document.RootElement.TryGetProperty("products", out var productsElement) || 
                    productsElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("No 'products' element found in response or it's not an array");
                    return Enumerable.Empty<Product>();
                }
                
                var products = new List<Product>();
                
                foreach (var productElement in productsElement.EnumerateArray())
                {
                    try
                    {
                        // Парсимо основні поля продукту
                        long id = 0;
                        if (productElement.TryGetProperty("id", out var idElement) &&
                            idElement.ValueKind == JsonValueKind.Number)
                        {
                            id = idElement.GetInt64();
                        }
                        
                        string name = string.Empty;
                        if (productElement.TryGetProperty("name", out var nameElement) &&
                            nameElement.ValueKind == JsonValueKind.String)
                        {
                            name = nameElement.GetString();
                        }
                        
                        decimal price = 0;
                        if (productElement.TryGetProperty("price", out var priceElement) &&
                            priceElement.ValueKind == JsonValueKind.Number)
                        {
                            price = priceElement.GetDecimal();
                        }
                        
                        string currency = "UAH";
                        if (productElement.TryGetProperty("currency", out var currencyElement) &&
                            currencyElement.ValueKind == JsonValueKind.String)
                        {
                            currency = currencyElement.GetString();
                        }
                        
                        string sku = string.Empty;
                        if (productElement.TryGetProperty("sku", out var skuElement) &&
                            skuElement.ValueKind == JsonValueKind.String)
                        {
                            sku = skuElement.GetString();
                        }
                        
                        string description = string.Empty;
                        if (productElement.TryGetProperty("description", out var descElement) &&
                            descElement.ValueKind == JsonValueKind.String)
                        {
                            description = descElement.GetString();
                        }
                        
                        string keywords = string.Empty;
                        if (productElement.TryGetProperty("keywords", out var keywordsElement) &&
                            keywordsElement.ValueKind == JsonValueKind.String)
                        {
                            keywords = keywordsElement.GetString();
                        }
                        
                        string presence = string.Empty;
                        if (productElement.TryGetProperty("presence", out var presenceElement) &&
                            presenceElement.ValueKind == JsonValueKind.String)
                        {
                            presence = presenceElement.GetString();
                        }
                        
                        object quantityInStock = null;
                        if (productElement.TryGetProperty("quantity_in_stock", out var quantityElement))
                        {
                            if (quantityElement.ValueKind == JsonValueKind.Number)
                            {
                                quantityInStock = quantityElement.GetInt32();
                            }
                            else if (quantityElement.ValueKind == JsonValueKind.String)
                            {
                                quantityInStock = quantityElement.GetString();
                            }
                        }
                        
                        string mainImage = string.Empty;
                        if (productElement.TryGetProperty("main_image", out var mainImageElement) &&
                            mainImageElement.ValueKind == JsonValueKind.String)
                        {
                            mainImage = mainImageElement.GetString();
                        }
                        
                        long? externalId = null;
                        if (productElement.TryGetProperty("external_id", out var externalIdElement))
                        {
                            if (externalIdElement.ValueKind == JsonValueKind.Number)
                            {
                                externalId = externalIdElement.GetInt64();
                            }
                            else if (externalIdElement.ValueKind == JsonValueKind.String &&
                                     long.TryParse(externalIdElement.GetString(), out var parsedId))
                            {
                                externalId = parsedId;
                            }
                        }
                        
                        string sellingType = string.Empty;
                        if (productElement.TryGetProperty("selling_type", out var sellingTypeElement) &&
                            sellingTypeElement.ValueKind == JsonValueKind.String)
                        {
                            sellingType = sellingTypeElement.GetString();
                        }
                        
                        string status = string.Empty;
                        if (productElement.TryGetProperty("status", out var statusElement) &&
                            statusElement.ValueKind == JsonValueKind.String)
                        {
                            status = statusElement.GetString();
                        }
                        
                        string measureUnit = string.Empty;
                        if (productElement.TryGetProperty("measure_unit", out var measureUnitElement) &&
                            measureUnitElement.ValueKind == JsonValueKind.String)
                        {
                            measureUnit = measureUnitElement.GetString();
                        }
                        
                        bool isVariation = false;
                        if (productElement.TryGetProperty("is_variation", out var isVariationElement) &&
                            isVariationElement.ValueKind == JsonValueKind.True)
                        {
                            isVariation = true;
                        }
                        
                        long? variationBaseId = null;
                        if (productElement.TryGetProperty("variation_base_id", out var variationBaseIdElement) &&
                            variationBaseIdElement.ValueKind == JsonValueKind.Number)
                        {
                            variationBaseId = variationBaseIdElement.GetInt64();
                        }
                        
                        long? variationGroupId = null;
                        if (productElement.TryGetProperty("variation_group_id", out var variationGroupIdElement) &&
                            variationGroupIdElement.ValueKind == JsonValueKind.Number)
                        {
                            variationGroupId = variationGroupIdElement.GetInt64();
                        }
                        
                        bool inStock = true;
                        if (productElement.TryGetProperty("in_stock", out var inStockElement))
                        {
                            if (inStockElement.ValueKind == JsonValueKind.False)
                            {
                                inStock = false;
                            }
                            else if (inStockElement.ValueKind == JsonValueKind.True)
                            {
                                inStock = true;
                            }
                        }
                        
                        DateTime? dateModified = null;
                        if (productElement.TryGetProperty("date_modified", out var dateModifiedElement) &&
                            dateModifiedElement.ValueKind == JsonValueKind.String)
                        {
                            if (DateTime.TryParse(dateModifiedElement.GetString(), out var parsedDate))
                            {
                                dateModified = parsedDate;
                            }
                        }
                        
                        // Парсинг вкладених об'єктів (група, категорія)
                        dynamic group = null;
                        if (productElement.TryGetProperty("group", out var groupElement) &&
                            groupElement.ValueKind == JsonValueKind.Object)
                        {
                            long groupId = 0;
                            if (groupElement.TryGetProperty("id", out var groupIdElement) &&
                                groupIdElement.ValueKind == JsonValueKind.Number)
                            {
                                groupId = groupIdElement.GetInt64();
                            }
                            
                            string groupName = string.Empty;
                            if (groupElement.TryGetProperty("name", out var groupNameElement) &&
                                groupNameElement.ValueKind == JsonValueKind.String)
                            {
                                groupName = groupNameElement.GetString();
                            }
                            
                            string groupDescription = string.Empty;
                            if (groupElement.TryGetProperty("description", out var groupDescElement) &&
                                groupDescElement.ValueKind == JsonValueKind.String)
                            {
                                groupDescription = groupDescElement.GetString();
                            }
                            
                            string groupImage = string.Empty;
                            if (groupElement.TryGetProperty("image", out var groupImageElement) &&
                                groupImageElement.ValueKind == JsonValueKind.String)
                            {
                                groupImage = groupImageElement.GetString();
                            }
                            
                            long? parentGroupId = null;
                            if (groupElement.TryGetProperty("parent_group_id", out var parentGroupIdElement) &&
                                parentGroupIdElement.ValueKind == JsonValueKind.Number)
                            {
                                parentGroupId = parentGroupIdElement.GetInt64();
                            }
                            
                            // Створюємо динамічний об'єкт групи
                            group = new
                            {
                                Id = groupId,
                                Name = groupName,
                                Description = groupDescription,
                                Image = groupImage,
                                ParentGroupId = parentGroupId
                            };
                        }
                        
                        dynamic category = null;
                        if (productElement.TryGetProperty("category", out var categoryElement) &&
                            categoryElement.ValueKind == JsonValueKind.Object)
                        {
                            long categoryId = 0;
                            if (categoryElement.TryGetProperty("id", out var categoryIdElement) &&
                                categoryIdElement.ValueKind == JsonValueKind.Number)
                            {
                                categoryId = categoryIdElement.GetInt64();
                            }
                            
                            string categoryCaption = string.Empty;
                            if (categoryElement.TryGetProperty("caption", out var captionElement) &&
                                captionElement.ValueKind == JsonValueKind.String)
                            {
                                categoryCaption = captionElement.GetString();
                            }
                            
                            // Створюємо динамічний об'єкт категорії
                            category = new
                            {
                                Id = categoryId,
                                Caption = categoryCaption
                            };
                        }
                        
                        // Парсинг зображень
                        List<dynamic> images = new List<dynamic>();
                        if (productElement.TryGetProperty("images", out var imagesElement) &&
                            imagesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var imageElement in imagesElement.EnumerateArray())
                            {
                                long imageId = 0;
                                if (imageElement.TryGetProperty("id", out var imageIdElement) &&
                                    imageIdElement.ValueKind == JsonValueKind.Number)
                                {
                                    imageId = imageIdElement.GetInt64();
                                }
                                
                                string thumbnailUrl = string.Empty;
                                if (imageElement.TryGetProperty("thumbnail_url", out var thumbnailElement) &&
                                    thumbnailElement.ValueKind == JsonValueKind.String)
                                {
                                    thumbnailUrl = thumbnailElement.GetString();
                                }
                                
                                string url = string.Empty;
                                if (imageElement.TryGetProperty("url", out var urlElement) &&
                                    urlElement.ValueKind == JsonValueKind.String)
                                {
                                    url = urlElement.GetString();
                                }
                                
                                images.Add(new
                                {
                                    Id = imageId,
                                    ThumbnailUrl = thumbnailUrl,
                                    Url = url
                                });
                            }
                        }
                        
                        // Парсинг багатомовних полів
                        Dictionary<string, string> nameMultilang = new Dictionary<string, string>();
                        if (productElement.TryGetProperty("name_multilang", out var nameMultilangElement) &&
                            nameMultilangElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var langProperty in nameMultilangElement.EnumerateObject())
                            {
                                if (langProperty.Value.ValueKind == JsonValueKind.String)
                                {
                                    nameMultilang[langProperty.Name] = langProperty.Value.GetString();
                                }
                            }
                        }
                        
                        Dictionary<string, string> descriptionMultilang = new Dictionary<string, string>();
                        if (productElement.TryGetProperty("description_multilang", out var descMultilangElement) &&
                            descMultilangElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var langProperty in descMultilangElement.EnumerateObject())
                            {
                                if (langProperty.Value.ValueKind == JsonValueKind.String)
                                {
                                    descriptionMultilang[langProperty.Name] = langProperty.Value.GetString();
                                }
                            }
                        }
                        
                        // Створюємо динамічний об'єкт продукту для збереження в базу даних
                        var dynamicProduct = new
                        {
                            Id = id,
                            Name = name,
                            Price = price,
                            Currency = currency,
                            Sku = sku,
                            Description = description,
                            Keywords = keywords,
                            Presence = presence,
                            QuantityInStock = quantityInStock,
                            MainImage = mainImage,
                            ExternalId = externalId,
                            SellingType = sellingType,
                            Status = status,
                            MeasureUnit = measureUnit,
                            IsVariation = isVariation,
                            VariationBaseId = variationBaseId,
                            VariationGroupId = variationGroupId,
                            InStock = inStock,
                            DateModified = dateModified,
                            Group = group,
                            Category = category,
                            Images = images,
                            NameMultilang = nameMultilang,
                            DescriptionMultilang = descriptionMultilang
                        };
                        
                        // Асинхронно зберігаємо в базу даних
                        _ = Task.Run(async () => {
                            try
                            {
                                try
                                {
                                    await _promRepository.SaveProductAsync(dynamicProduct);
                                    _logger.LogInformation("Saved product '{Name}' to database", name);
                                }
                                catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "42P01")
                                {
                                    _logger.LogWarning("Tables for Prom products don't exist. Skip saving: {Error}", pgEx.Message);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error saving product '{Name}' to database", name);
                            }
                        });
                        
                        // Конвертуємо в доменний об'єкт Product для повернення клієнту
                        string marketplaceId = id.ToString();
                        Guid productId;
                        if (!Guid.TryParse(marketplaceId, out productId))
                        {
                            productId = CreateDeterministicGuid(marketplaceId);
                        }
                        
                        // Створюємо список URL зображень, явно конвертуючи динамічні значення в рядки
                        var imageUrls = new List<string>();
                        foreach (var img in images)
                        {
                            imageUrls.Add(img.Url.ToString());
                        }
                        
                        var product = new Product
                        {
                            Id = productId,
                            Name = name,
                            Price = price,
                            Description = description,
                            Sku = sku,
                            MarketplaceId = marketplaceId,
                            MarketplaceType = "Prom.ua",
                            Currency = currency,
                            Keywords = keywords,
                            MainImage = mainImage,
                            Images = imageUrls,
                            DateModified = dateModified,
                            InStock = inStock,
                            NameMultilang = nameMultilang,
                            DescriptionMultilang = descriptionMultilang,
                            MarketplaceMappings = new Dictionary<string, string> { { "Prom.ua", marketplaceId } },
                        };
                        
                        // Додаємо специфічні дані маркетплейсу
                        var marketplaceSpecificData = new Dictionary<string, object>
                        {
                            { "external_id", externalId },
                            { "presence", presence },
                            { "selling_type", sellingType },
                            { "status", status },
                            { "quantity_in_stock", quantityInStock },
                            { "measure_unit", measureUnit },
                            { "is_variation", isVariation },
                            { "variation_base_id", variationBaseId },
                            { "variation_group_id", variationGroupId }
                        };
                        
                        if (group != null)
                        {
                            marketplaceSpecificData.Add("group_id", group.Id.ToString());
                            marketplaceSpecificData.Add("group_name", group.Name);
                            marketplaceSpecificData.Add("group_description", group.Description);
                            marketplaceSpecificData.Add("group_image", group.Image);
                            if (group.ParentGroupId != null)
                            {
                                marketplaceSpecificData.Add("group_parent_id", group.ParentGroupId.ToString());
                            }
                        }
                        
                        if (category != null)
                        {
                            marketplaceSpecificData.Add("category_id", category.Id.ToString());
                            marketplaceSpecificData.Add("category_name", category.Caption);
                        }
                        
                        product.MarketplaceSpecificData = marketplaceSpecificData;
                        
                        // Встановлюємо кількість на складі
                        if (quantityInStock != null)
                        {
                            if (quantityInStock is int quantity)
                            {
                                product.QuantityInStock = quantity;
                            }
                            else if (quantityInStock is string quantityStr && int.TryParse(quantityStr, out int parsedQuantity))
                            {
                                product.QuantityInStock = parsedQuantity;
                            }
                        }
                        
                    products.Add(product);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing product element from JSON");
                    }
                }
                
                _logger.LogInformation("Retrieved {Count} products from Prom.ua API", products.Count);
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products from Prom.ua");
                return Enumerable.Empty<Product>();
            }
        }

        /// <inheritdoc />
        public async Task<Product> GetProductByMarketplaceIdAsync(string marketplaceProductId)
        {
            try
            {
                _logger.LogInformation("Getting product from Prom.ua with ID: {MarketplaceProductId}", marketplaceProductId);
                var response = await _httpClient.GetAsync($"products/{marketplaceProductId}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                
                // Використовуємо JsonElement замість конкретного класу для гнучкішого парсингу
                using var document = JsonDocument.Parse(content);
                
                if (!document.RootElement.TryGetProperty("product", out var productElement))
                {
                    _logger.LogWarning("No 'product' property found in response");
                    return null;
                }
                
                // Намагаємося перетворити рядок ID в Guid
                Guid productId;
                
                // Якщо не вдалось перетворити, генеруємо детермінований Guid
                if (!Guid.TryParse(marketplaceProductId, out productId))
                {
                    productId = CreateDeterministicGuid(marketplaceProductId);
                }
                
                var product = new Product
                {
                    Id = productId, // Використовуємо перетворений ID
                    Name = productElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty,
                    Description = productElement.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? string.Empty : string.Empty,
                    Price = productElement.TryGetProperty("price", out var priceProp) ? priceProp.GetDecimal() : 0m,
                    Sku = productElement.TryGetProperty("sku", out var skuProp) ? skuProp.GetString() ?? string.Empty : string.Empty,
                    Keywords = productElement.TryGetProperty("keywords", out var keywordsProp) ? keywordsProp.GetString() ?? string.Empty : string.Empty,
                    MainImage = productElement.TryGetProperty("main_image", out var mainImageProp) ? mainImageProp.GetString() ?? string.Empty : string.Empty,
                    Currency = productElement.TryGetProperty("currency", out var currencyProp) ? currencyProp.GetString() ?? "UAH" : "UAH",
                    MarketplaceType = "Prom.ua",
                    MarketplaceId = marketplaceProductId,
                    MarketplaceMappings = new Dictionary<string, string> { { "Prom.ua", marketplaceProductId } }
                };
                
                // Отримуємо quantity_in_stock (може бути числом або рядком)
                if (productElement.TryGetProperty("quantity_in_stock", out var quantityProp))
                {
                    if (quantityProp.ValueKind == JsonValueKind.Number)
                    {
                        product.QuantityInStock = quantityProp.GetInt32();
                    }
                    else if (quantityProp.ValueKind == JsonValueKind.String && int.TryParse(quantityProp.GetString(), out int qty))
                    {
                        product.QuantityInStock = qty;
                    }
                }
                
                // Додаємо зображення, якщо вони є
                if (productElement.TryGetProperty("images", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Array)
                {
                    var images = new List<string>();
                    foreach (var img in imagesProp.EnumerateArray())
                    {
                        if (img.TryGetProperty("url", out var urlProp))
                        {
                            images.Add(urlProp.GetString() ?? string.Empty);
                        }
                    }
                    product.Images = images;
                }
                else
                {
                    product.Images = new List<string>();
                }
                
                // Додаємо додаткові дані, якщо вони є
                var specificData = new Dictionary<string, object>();
                
                if (productElement.TryGetProperty("status", out var statusProp))
                {
                    specificData["status"] = statusProp.GetString() ?? string.Empty;
                }
                
                if (productElement.TryGetProperty("group", out var groupProp))
                {
                    if (groupProp.TryGetProperty("id", out var groupIdProp))
                    {
                        specificData["group_id"] = groupIdProp.ToString();
                    }
                    
                    if (groupProp.TryGetProperty("name", out var groupNameProp))
                    {
                        specificData["group_name"] = groupNameProp.GetString() ?? string.Empty;
                    }
                }
                
                if (productElement.TryGetProperty("category", out var categoryProp))
                {
                    if (categoryProp.TryGetProperty("id", out var categoryIdProp))
                    {
                        specificData["category_id"] = categoryIdProp.ToString();
                    }
                    
                    if (categoryProp.TryGetProperty("caption", out var captionProp))
                    {
                        specificData["category_name"] = captionProp.GetString() ?? string.Empty;
                    }
                }
                
                product.MarketplaceSpecificData = specificData;
                
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product from Prom.ua with ID: {MarketplaceProductId}", marketplaceProductId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Product> CreateProductAsync(Product product)
        {
            try
            {
                _logger.LogInformation("Creating product in Prom.ua: {ProductName}", product.Name);

                // Валідація обов'язкових полів
                if (string.IsNullOrEmpty(product.Name))
                {
                    _logger.LogError("Product name is required for Prom.ua API");
                    return product; // Повертаємо продукт без MarketplaceId
                }

                if (product.Price <= 0)
                {
                    _logger.LogError("Product price must be greater than 0 for Prom.ua API");
                    return product; // Повертаємо продукт без MarketplaceId
                }

                // Створюємо об'єкт запиту на основі моделі PromProductRequest
                var promProductRequest = new Prom.PromProductRequest
                {
                    Product = new Prom.PromProductData
                    {
                        Name = product.Name,
                        Price = product.Price,
                        Sku = product.Sku ?? string.Empty,
                        Description = product.Description ?? string.Empty,
                        Currency = product.Currency ?? "UAH",
                        Keywords = product.Keywords ?? string.Empty,
                        QuantityInStock = product.QuantityInStock ?? 0,
                        Status = "on_display", // За замовчуванням товар відображається на сайті
                        Presence = "available" // За замовчуванням товар доступний для покупки
                    }
                };

                // Додаємо багатомовні поля, якщо вони є
                if (product.NameMultilang != null && product.NameMultilang.Count > 0)
                {
                    promProductRequest.Product.NameMultilang = product.NameMultilang;
                }

                if (product.DescriptionMultilang != null && product.DescriptionMultilang.Count > 0)
                {
                    promProductRequest.Product.DescriptionMultilang = product.DescriptionMultilang;
                }

                // Додаємо зображення, якщо вони є
                if (product.Images != null && product.Images.Count > 0)
                {
                    promProductRequest.Product.Images = product.Images;
                    promProductRequest.Product.MainImage = product.MainImage ?? product.Images.FirstOrDefault();
                }
                
                // Додаємо дані групи, якщо вони є
                if (!string.IsNullOrEmpty(product.GroupId) && long.TryParse(product.GroupId, out long groupId))
                {
                    promProductRequest.Product.GroupId = groupId;
                }
                
                // Додаємо зовнішній ID, якщо він є
                if (!string.IsNullOrEmpty(product.ExternalId))
                {
                    promProductRequest.Product.ExternalId = product.ExternalId;
                }
                
                // Додаємо додаткові дані, якщо вони є в MarketplaceSpecificData
                if (product.MarketplaceSpecificData != null)
                {
                    // MeasureUnit
                    if (product.MarketplaceSpecificData.TryGetValue("measure_unit", out var measureUnit) && measureUnit != null)
                    {
                        promProductRequest.Product.MeasureUnit = measureUnit.ToString();
                    }
                    
                    // Presence (наявність)
                    if (product.MarketplaceSpecificData.TryGetValue("presence", out var presence) && presence != null)
                    {
                        promProductRequest.Product.Presence = presence.ToString();
                    }
                    
                    // Знижка
                    if (product.MarketplaceSpecificData.TryGetValue("discount", out var discount) && discount != null 
                        && decimal.TryParse(discount.ToString(), out decimal discountValue))
                    {
                        promProductRequest.Product.Discount = discountValue;
                    }
                    
                    // Мінімальна кількість для замовлення
                    if (product.MarketplaceSpecificData.TryGetValue("minimum_order_quantity", out var minOrderQty) && minOrderQty != null 
                        && int.TryParse(minOrderQty.ToString(), out int minQty))
                    {
                        promProductRequest.Product.MinimumOrderQuantity = minQty;
                    }
                    
                    // Категорія
                    if (product.MarketplaceSpecificData.TryGetValue("category_id", out var categoryId) && categoryId != null 
                        && long.TryParse(categoryId.ToString(), out long catId))
                    {
                        promProductRequest.Product.CategoryId = catId;
                    }
                    
                    // Варіації
                    if (product.MarketplaceSpecificData.TryGetValue("is_variation", out var isVariation) && isVariation != null 
                        && bool.TryParse(isVariation.ToString(), out bool isVar))
                    {
                        promProductRequest.Product.IsVariation = isVar;
                    }
                    
                    if (product.MarketplaceSpecificData.TryGetValue("variation_base_id", out var variationBaseId) && variationBaseId != null 
                        && long.TryParse(variationBaseId.ToString(), out long varBaseId))
                    {
                        promProductRequest.Product.VariationBaseId = varBaseId;
                    }
                    
                    if (product.MarketplaceSpecificData.TryGetValue("variation_group_id", out var variationGroupId) && variationGroupId != null 
                        && long.TryParse(variationGroupId.ToString(), out long varGroupId))
                    {
                        promProductRequest.Product.VariationGroupId = varGroupId;
                    }
                }
                
                // Серіалізуємо об'єкт запиту в JSON
                var jsonContent = JsonSerializer.Serialize(promProductRequest, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending request to Prom.ua API: {JsonContent}", jsonContent);
                
                // Використовуємо правильний ендпоінт Prom API для створення товару
                var response = await _httpClient.PostAsync("products/edit", content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Create product response: {Response}", responseContent);
                
                // Перевіряємо на наявність помилки у відповіді
                if (responseContent.Contains("error"))
                {
                    _logger.LogError("Error response from Prom.ua API: {Response}", responseContent);
                    return product; // Повертаємо продукт без MarketplaceId
                }
                
                response.EnsureSuccessStatusCode();
                
                // Використовуємо JsonElement для гнучкого парсингу
                using var document = JsonDocument.Parse(responseContent);
                
                // ID товару може бути в різних місцях залежно від структури відповіді
                string marketplaceId = string.Empty;
                
                // Спробуємо різні варіанти отримання ID
                if (document.RootElement.TryGetProperty("id", out var idProp))
                {
                    if (idProp.ValueKind == JsonValueKind.Number)
                    {
                        marketplaceId = idProp.GetInt64().ToString();
                    }
                    else
                    {
                        marketplaceId = idProp.GetString() ?? string.Empty;
                    }
                }
                else if (document.RootElement.TryGetProperty("product_id", out var productIdProp))
                {
                    if (productIdProp.ValueKind == JsonValueKind.Number)
                    {
                        marketplaceId = productIdProp.GetInt64().ToString();
                    }
                    else
                    {
                        marketplaceId = productIdProp.GetString() ?? string.Empty;
                    }
                }
                else if (document.RootElement.TryGetProperty("product", out var productProp) 
                         && productProp.TryGetProperty("id", out var productInnerIdProp))
                {
                    if (productInnerIdProp.ValueKind == JsonValueKind.Number)
                    {
                        marketplaceId = productInnerIdProp.GetInt64().ToString();
                    }
                    else
                    {
                        marketplaceId = productInnerIdProp.GetString() ?? string.Empty;
                    }
                }
                
                if (!string.IsNullOrEmpty(marketplaceId))
                {
                    // Створюємо детермінований Guid на основі ID маркетплейсу
                    Guid productId;
                    if (!Guid.TryParse(marketplaceId, out productId))
                    {
                        productId = CreateDeterministicGuid(marketplaceId);
                    }
                    
                    // Оновлюємо ID товару
                    product.Id = productId;
                    product.MarketplaceId = marketplaceId;
                    product.MarketplaceType = "Prom.ua";
                    
                    if (product.MarketplaceMappings == null)
                    {
                        product.MarketplaceMappings = new Dictionary<string, string>();
                    }
                    
                    product.MarketplaceMappings["Prom.ua"] = marketplaceId;
                    
                    // Get full product details
                    var createdDetails = await GetProductByMarketplaceIdAsync(marketplaceId);
                    if (createdDetails != null)
                    {
                        // Update relevant product properties from the created product
                        product.MainImage = createdDetails.MainImage;
                        product.Images = createdDetails.Images;
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    _logger.LogWarning("Could not extract product ID from response: {Response}", responseContent);
                }

                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product in Prom.ua: {ProductName}. Error: {ErrorMessage}", 
                    product.Name, ex.Message);
                return product;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProductAsync(string marketplaceProductId)
        {
            try
            {
                _logger.LogInformation("Deleting product from Prom.ua with ID: {MarketplaceProductId}", marketplaceProductId);
                
                // Для видалення товару використовуємо API products/edit з встановленням status = "delete"
                var promProductRequest = new Prom.PromProductRequest
                {
                    Product = new Prom.PromProductData
                    {
                        Id = long.TryParse(marketplaceProductId, out long id) ? id : 0,
                        Status = "delete" // Статус "delete" для видалення товару
                    }
                };
                
                // Серіалізуємо об'єкт запиту в JSON
                var jsonContent = JsonSerializer.Serialize(promProductRequest, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Використовуємо правильний ендпоінт Prom API для видалення товару
                var response = await _httpClient.PostAsync("products/edit", content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Delete product response: {Response}", responseContent);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product from Prom.ua with ID: {MarketplaceProductId}. Error: {ErrorMessage}", 
                    marketplaceProductId, ex.Message);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<int> ImportProductsAsync()
        {
            try
            {
                _logger.LogInformation("Importing products from Prom.ua");
                
                // Get products from Prom.ua API
                var response = await _httpClient.GetAsync("products/list");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                
                // Deserialize JSON response
                var productListResponse = JsonSerializer.Deserialize<Prom.PromProductListResponse>(content, _jsonOptions);
                if (productListResponse?.Products == null || !productListResponse.Products.Any())
                {
                    _logger.LogWarning("No products found in Prom.ua API response");
                    return 0;
                }
                
                int importCount = 0;
                
                // First, save all products to Prom tables
                foreach (var promProduct in productListResponse.Products)
                {
                    try
                    {
                        // Save to Prom tables with nested entities
                        await _promRepository.SaveProductAsync(promProduct);
                        
                        // Also map to Product domain model and save to regular product tables
                        var product = MapToProduct(promProduct);
                        await _productRepository.CreateAsync(product);
                        
                        importCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error importing product '{Name}' from Prom.ua", promProduct.Name);
                    }
                }

                _logger.LogInformation("Imported {ImportCount} products from Prom.ua", importCount);
                return importCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing products from Prom.ua");
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<int> ExportProductsAsync(IEnumerable<Guid> productIds = null)
        {
            try
            {
                _logger.LogInformation("Exporting products to Prom.ua");
                
                // Get products to export
                IEnumerable<Product> productsToExport;
                if (productIds != null && productIds.Any())
                {
                    productsToExport = new List<Product>();
                    foreach (var id in productIds)
                    {
                        var product = await _productRepository.GetByIdAsync(id);
                        if (product != null)
                        {
                            ((List<Product>)productsToExport).Add(product);
                        }
                    }
                }
                else
                {
                    productsToExport = await _productRepository.GetAllAsync();
                }
                
                int exportCount = 0;

                foreach (var product in productsToExport)
                {
                    // Check if the product already exists in Prom.ua
                    if (!string.IsNullOrEmpty(product.MarketplaceId) && product.MarketplaceType == "Prom.ua")
                    {
                        // Update existing product
                        var updated = await UpdateProductAsync(product);
                        if (updated)
                        {
                            exportCount++;
                        }
                    }
                    else
                    {
                        // Create new product
                        var createdProduct = await CreateProductAsync(product);
                        if (!string.IsNullOrEmpty(createdProduct.MarketplaceId))
                        {
                            // Update the product in the database with the new marketplace ID
                            product.MarketplaceId = createdProduct.MarketplaceId;
                            product.MarketplaceType = "Prom.ua";
                            product.MarketplaceMappings["Prom.ua"] = createdProduct.MarketplaceId;
                            await _productRepository.UpdateAsync(product);
                            
                            exportCount++;
                        }
                    }
                }

                _logger.LogInformation("Exported {ExportCount} products to Prom.ua", exportCount);
                return exportCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting products to Prom.ua");
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SyncProductInventoryAsync(string marketplaceProductId, int quantity)
        {
            try
            {
                _logger.LogInformation("Syncing product inventory with Prom.ua: Product ID {MarketplaceProductId}, Quantity {Quantity}", marketplaceProductId, quantity);
                
                // Get product from Prom.ua
                var product = await GetProductByMarketplaceIdAsync(marketplaceProductId);
                if (product == null)
                {
                    _logger.LogWarning("Product not found in Prom.ua: {MarketplaceProductId}", marketplaceProductId);
                    return false;
                }
                
                // Update quantity
                product.QuantityInStock = quantity;
                
                // Update product in Prom.ua
                return await UpdateProductAsync(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing product inventory with Prom.ua: Product ID {MarketplaceProductId}", marketplaceProductId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> PublishProductByIdAsync(Guid productId)
        {
            try
            {
                _logger.LogInformation("Publishing product to Prom.ua with ID: {ProductId}", productId);
                
                // Get the product from the database
                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("Product not found with ID: {ProductId}", productId);
                    return false;
                }
                
                // Check if the product already exists in Prom.ua
                if (!string.IsNullOrEmpty(product.MarketplaceId) && product.MarketplaceType == "Prom.ua")
                {
                    // Product already exists in Prom.ua, update it
                    return await UpdateProductAsync(product);
                }
                else
                {
                    // Create new product in Prom.ua
                    var createdProduct = await CreateProductAsync(product);
                    if (!string.IsNullOrEmpty(createdProduct.MarketplaceId))
                    {
                        // Update the product in the database with the new marketplace ID
                        product.MarketplaceId = createdProduct.MarketplaceId;
                        product.MarketplaceType = "Prom.ua";
                        product.MarketplaceMappings["Prom.ua"] = createdProduct.MarketplaceId;
                        await _productRepository.UpdateAsync(product);
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing product to Prom.ua with ID: {ProductId}", productId);
                return false;
            }
        }

        /// <summary>
        /// Зберігає продукти з Prom.ua в базу даних
        /// </summary>
        /// <param name="products">Список продуктів для збереження</param>
        /// <returns>Кількість збережених продуктів</returns>
        public async Task<int> SaveProductsToDatabaseAsync(IEnumerable<Product> products)
        {
            try
            {
                _logger.LogInformation("Saving products to database");
                int savedCount = 0;

                foreach (var product in products)
                {
                    // Перевірити чи вказаний MarketplaceId
                    if (string.IsNullOrEmpty(product.MarketplaceId))
                    {
                        _logger.LogWarning("Product does not have MarketplaceId: {ProductName}", product.Name);
                        continue;
                    }

                    // Перевірити чи існує продукт в базі даних за marketplaceId
                    var existingProduct = await _productRepository.GetByMarketplaceIdAsync(
                        product.MarketplaceId, product.MarketplaceType ?? "Prom.ua");
                    
                    if (existingProduct != null)
                    {
                        // Оновити існуючий продукт але зберегти ID незмінним
                        existingProduct.Name = product.Name;
                        existingProduct.Price = product.Price;
                        existingProduct.Description = product.Description;
                        existingProduct.Sku = product.Sku;
                        existingProduct.MainImage = product.MainImage;
                        existingProduct.Images = product.Images;
                        existingProduct.QuantityInStock = product.QuantityInStock;
                        existingProduct.UpdatedAt = DateTime.UtcNow;
                        existingProduct.Currency = product.Currency;
                        existingProduct.Keywords = product.Keywords;
                        
                        // Копіюємо специфічні дані маркетплейсу, якщо вони є
                        if (product.MarketplaceSpecificData != null && product.MarketplaceSpecificData.Count > 0)
                        {
                            existingProduct.MarketplaceSpecificData = product.MarketplaceSpecificData;
                        }
                        
                        await _productRepository.UpdateAsync(existingProduct);
                    }
                    else
                    {
                        // Створюємо детермінований Guid на основі ID маркетплейсу
                        if (product.Id == Guid.Empty)
                        {
                            Guid productId;
                            if (!Guid.TryParse(product.MarketplaceId, out productId))
                            {
                                productId = CreateDeterministicGuid(product.MarketplaceId);
                            }
                            product.Id = productId;
                        }
                        
                        // Встановити дату створення і оновлення
                        product.CreatedAt = DateTime.UtcNow;
                        product.UpdatedAt = DateTime.UtcNow;
                        
                        // Перевірити і встановити тип маркетплейсу, якщо не вказаний
                        if (string.IsNullOrEmpty(product.MarketplaceType))
                        {
                            product.MarketplaceType = "Prom.ua";
                        }
                        
                        // Перевірити і створити маппінги маркетплейсу, якщо відсутні
                        if (product.MarketplaceMappings == null)
                        {
                            product.MarketplaceMappings = new Dictionary<string, string>();
                        }
                        
                        if (!product.MarketplaceMappings.ContainsKey(product.MarketplaceType))
                        {
                            product.MarketplaceMappings[product.MarketplaceType] = product.MarketplaceId;
                        }
                        
                        await _productRepository.CreateAsync(product);
                    }
                    
                    savedCount++;
                }

                _logger.LogInformation("Saved {SavedCount} products to database", savedCount);
                return savedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving products to database");
                return 0;
            }
        }

        /// <summary>
        /// Зберігає продукти з файлу в базу даних
        /// </summary>
        /// <param name="filePath">Шлях до файлу з продуктами (CSV, Excel, тощо)</param>
        /// <returns>Кількість збережених продуктів</returns>
        public async Task<int> SaveProductsFromFileAsync(string filePath)
        {
            try
            {
                _logger.LogInformation("Saving products from file: {FilePath}", filePath);
                
                // Тут буде код для парсингу файлу і конвертації даних у продукти
                // Це приклад - потрібно реалізувати відповідно до формату файлу
                List<Product> products = new List<Product>();
                
                // Перевірка типу файлу
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                switch (extension)
                {
                    case ".csv":
                        products = ParseCsvFile(filePath);
                        break;
                    case ".xlsx":
                    case ".xls":
                        products = ParseExcelFile(filePath);
                        break;
                    case ".json":
                        products = ParseJsonFile(filePath);
                        break;
                    default:
                        _logger.LogWarning("Unsupported file format: {Extension}", extension);
                        return 0;
                }
                
                // Зберегти продукти в базу даних
                return await SaveProductsToDatabaseAsync(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving products from file: {FilePath}", filePath);
                return 0;
            }
        }

        // Заглушки для парсингу файлів - потрібно реалізувати
        private List<Product> ParseCsvFile(string filePath)
        {
            // TODO: Реалізувати парсинг CSV файлу
            throw new NotImplementedException("CSV parsing is not implemented yet");
        }

        private List<Product> ParseExcelFile(string filePath)
        {
            // TODO: Реалізувати парсинг Excel файлу
            throw new NotImplementedException("Excel parsing is not implemented yet");
        }

        private List<Product> ParseJsonFile(string filePath)
        {
            // TODO: Реалізувати парсинг JSON файлу
            throw new NotImplementedException("JSON parsing is not implemented yet");
        }

        #endregion

        #region Order Methods

        /// <inheritdoc />
        public async Task<IEnumerable<Order>> GetOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Getting orders from Prom.ua");
                
                // Get raw orders
                var promOrders = await GetRawOrdersAsync(startDate, endDate);
                if (!promOrders.Any())
                {
                    return Enumerable.Empty<Order>();
                }

                var orders = new List<Order>();
                
                foreach (var promOrder in promOrders)
                {
                    try
                    {
                        // Convert to domain Order model
                        var order = MapToOrder(promOrder);
                        
                        // Асинхронно зберігаємо в базу даних
                        _ = Task.Run(async () => {
                            try
                            {
                                // Створення клієнта, якщо потрібно
                                var customer = await EnsureCustomerExistsAsync(order);
                                if (customer != null)
                                {
                                    order.CustomerId = customer.Id;
                                }
                                
                                // Перевірка існування замовлення
                                var existingOrder = await _orderRepository.GetByMarketplaceIdAsync(order.MarketplaceId, "Prom.ua");
                                if (existingOrder != null)
                                {
                                    await _orderRepository.UpdateAsync(order);
                                    _logger.LogInformation("Updated existing order: {OrderId}", order.MarketplaceId);
                                }
                                else
                                {
                                    await _orderRepository.AddAsync(order);
                                    _logger.LogInformation("Added new order: {OrderId}", order.MarketplaceId);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error saving order {OrderId} to database", order.MarketplaceId);
                            }
                        });
                        
                        orders.Add(order);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing order {OrderId} from Prom.ua", promOrder.Id);
                    }
                }
                
                _logger.LogInformation("Retrieved {Count} orders from Prom.ua", orders.Count);
                return orders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders from Prom.ua");
                return Enumerable.Empty<Order>();
            }
        }

        /// <summary>
        /// Gets a specific order from Prom.ua by its ID
        /// </summary>
        /// <param name="orderId">The Prom.ua order ID</param>
        /// <returns>The order if found, null otherwise</returns>
        public async Task<Order> GetOrderByIdAsync(string orderId)
        {
            try
            {
                _logger.LogInformation("Getting order from Prom.ua with ID: {OrderId}", orderId);
                
                // First try to get from database
                var existingOrder = await _orderRepository.GetByMarketplaceIdAsync(orderId, "Prom.ua");
                if (existingOrder != null)
                {
                    _logger.LogInformation("Order {OrderId} found in database", orderId);
                    return existingOrder;
                }
                
                // If not in database, get from API
                var response = await _httpClient.GetAsync($"orders/{orderId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get order from Prom.ua. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received response from Prom API: {Length} bytes", content.Length);
                
                // Parse response
                using var document = JsonDocument.Parse(content);
                
                if (!document.RootElement.TryGetProperty("order", out var orderElement) || 
                    orderElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("No 'order' element found in response or it's not an object");
                    return null;
                }
                
                try
                {
                    // Try to deserialize to our model
                    var promOrder = JsonSerializer.Deserialize<Models.Prom.PromOrder>(orderElement.GetRawText(), _jsonOptions);
                    
                    if (promOrder == null)
                    {
                        _logger.LogWarning("Failed to deserialize order from Prom.ua API");
                        return null;
                    }
                    
                    // Convert to domain Order model
                    var order = MapToOrder(promOrder);
                    
                    // Асинхронно зберігаємо в базу даних
                    _ = Task.Run(async () => {
                        try
                        {
                            // Створення клієнта, якщо потрібно
                            var customer = await EnsureCustomerExistsAsync(order);
                            if (customer != null)
                            {
                                order.CustomerId = customer.Id;
                            }
                            
                            await _orderRepository.AddAsync(order);
                            _logger.LogInformation("Added new order from API: {OrderId}", order.MarketplaceId);
            }
            catch (Exception ex)
            {
                            _logger.LogError(ex, "Error saving order {OrderId} to database", order.MarketplaceId);
                        }
                    });
                    
                    return order;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Error deserializing Prom.ua API response. Attempting manual JSON parsing.");
                    
                    // If standard deserialization failed, try manual parsing
                    try
                    {
                        var promOrder = ParsePromOrderFromJson(orderElement);
                        if (promOrder != null)
                        {
                            var order = MapToOrder(promOrder);
                            
                            // Асинхронно зберігаємо в базу даних
                            _ = Task.Run(async () => {
                                try
                                {
                                    // Створення клієнта, якщо потрібно
                                    var customer = await EnsureCustomerExistsAsync(order);
                                    if (customer != null)
                                    {
                                        order.CustomerId = customer.Id;
                                    }
                                    
                                    await _orderRepository.AddAsync(order);
                                    _logger.LogInformation("Added manually parsed order: {OrderId}", order.MarketplaceId);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error saving order {OrderId} to database", order.MarketplaceId);
                                }
                            });
                            
                            return order;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error manually parsing order from Prom.ua API");
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order from Prom.ua with ID: {OrderId}", orderId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<int> ImportOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Importing orders from Prom.ua");
                
                // Get orders from Prom.ua
                var promOrders = await GetOrdersAsync(startDate, endDate);
                int importCount = 0;

                foreach (var promOrder in promOrders)
                {
                    // Check if the order already exists in the database by marketplace ID
                    var existingOrder = await _orderRepository.GetByMarketplaceIdAsync(promOrder.MarketplaceId, "Prom.ua");
                    
                    if (existingOrder != null)
                    {
                        // Update existing order but keep ID unchanged
                        existingOrder.Status = promOrder.Status;
                        existingOrder.UpdatedAt = DateTime.UtcNow;
                        existingOrder.ShippingAddress = promOrder.ShippingAddress;
                        existingOrder.ShippingMethod = promOrder.ShippingMethod;
                        existingOrder.PaymentMethod = promOrder.PaymentMethod;
                        
                        await _orderRepository.UpdateAsync(existingOrder);
                    }
                    else
                    {
                        // Create customer if it doesn't exist
                        var customer = await EnsureCustomerExistsAsync(promOrder);
                        
                        // Create new order - ID вже встановлений через детермінований Guid в MapToOrder()
                        promOrder.CustomerId = customer.Id;
                        promOrder.CreatedAt = DateTime.UtcNow;
                        promOrder.UpdatedAt = DateTime.UtcNow;
                        promOrder.Source = "Prom.ua";
                        
                        await _orderRepository.AddAsync(promOrder);
                    }
                    
                    importCount++;
                }

                _logger.LogInformation("Imported {ImportCount} orders from Prom.ua", importCount);
                return importCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing orders from Prom.ua");
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateOrderStatusAsync(string marketplaceOrderId, OrderStatus status)
        {
            try
            {
                _logger.LogInformation("Updating order status in Prom.ua for order ID: {MarketplaceOrderId}", marketplaceOrderId);
                
                string promStatus = MapStatusToProm(status);
                
                // Create status update request
                var statusUpdateRequest = new Models.Prom.PromStatusUpdateRequest
                {
                    Status = promStatus
                };
                
                // If the status is cancelled, add cancellation reason
                if (status == OrderStatus.Cancelled)
                {
                    statusUpdateRequest.CancellationReason = "Order cancelled by merchant";
                }
                
                var json = JsonSerializer.Serialize(statusUpdateRequest, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"orders/{marketplaceOrderId}/status", content);
                response.EnsureSuccessStatusCode();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status in Prom.ua for order ID: {MarketplaceOrderId}", marketplaceOrderId);
                return false;
            }
        }

        #endregion

        #region Group Methods

        /// <inheritdoc />
        public async Task<IEnumerable<Prom.PromGroup>> GetGroupsAsync()
        {
            try
            {
                _logger.LogInformation("Getting groups from Prom.ua");
                
                // Спроба отримати з бази даних
                try
                {
                    var allGroups = await _promRepository.GetAllGroupsAsync();
                    if (allGroups != null && allGroups.Any())
                    {
                        _logger.LogInformation("Retrieved {Count} groups from database", allGroups.Count());
                        var groupsFromDb = new List<Prom.PromGroup>();
                        
                        foreach (dynamic groupDynamic in allGroups)
                        {
                            try
                            {
                                var group = new Prom.PromGroup
                                {
                                    Id = groupDynamic.Id,
                                    Name = groupDynamic.Name,
                                    Description = groupDynamic.Description,
                                    Image = groupDynamic.Image,
                                    ParentGroupId = groupDynamic.ParentGroupId,
                                    NameMultilang = groupDynamic.NameMultilang,
                                    DescriptionMultilang = groupDynamic.DescriptionMultilang
                                };
                                groupsFromDb.Add(group);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error mapping group from database");
                            }
                        }
                        
                        return groupsFromDb;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving groups from database, will try API");
                }
                
                // Якщо немає в базі даних, отримуємо з API
                _logger.LogInformation("Calling Prom.ua API for groups");
                var response = await _httpClient.GetAsync("groups/list");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get groups from Prom.ua. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<Prom.PromGroup>();
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received response from Prom API: {Length} bytes", content.Length);
                
                // Парсимо через JsonDocument для безпечної обробки різних типів
                using var document = JsonDocument.Parse(content);
                
                if (!document.RootElement.TryGetProperty("groups", out var groupsElement) || 
                    groupsElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("No 'groups' element found in response or it's not an array");
                    return Enumerable.Empty<Prom.PromGroup>();
                }
                
                var groups = new List<Prom.PromGroup>();
                
                foreach (var groupElement in groupsElement.EnumerateArray())
                {
                    try
                    {
                        // Безпечне отримання полів групи
                        long groupId = 0;
                        if (groupElement.TryGetProperty("id", out var idElement) && 
                            idElement.ValueKind == JsonValueKind.Number)
                        {
                            groupId = idElement.GetInt64();
                        }
                        
                        string name = string.Empty;
                        if (groupElement.TryGetProperty("name", out var nameElement) && 
                            nameElement.ValueKind == JsonValueKind.String)
                        {
                            name = nameElement.GetString();
                        }
                        
                        string description = string.Empty;
                        if (groupElement.TryGetProperty("description", out var descElement) && 
                            descElement.ValueKind == JsonValueKind.String)
                        {
                            description = descElement.GetString();
                        }
                        
                        string image = string.Empty;
                        if (groupElement.TryGetProperty("image", out var imageElement) && 
                            imageElement.ValueKind == JsonValueKind.String)
                        {
                            image = imageElement.GetString();
                        }
                        
                        long? parentGroupId = null;
                        if (groupElement.TryGetProperty("parent_group_id", out var parentElement) && 
                            parentElement.ValueKind == JsonValueKind.Number)
                        {
                            parentGroupId = parentElement.GetInt64();
                        }
                        
                        // Парсинг багатомовних полів
                        Dictionary<string, string> nameMultilang = new Dictionary<string, string>();
                        if (groupElement.TryGetProperty("name_multilang", out var nameMultilangElement) && 
                            nameMultilangElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var langProperty in nameMultilangElement.EnumerateObject())
                            {
                                if (langProperty.Value.ValueKind == JsonValueKind.String)
                                {
                                    nameMultilang[langProperty.Name] = langProperty.Value.GetString();
                                }
                            }
                        }
                        
                        Dictionary<string, string> descriptionMultilang = new Dictionary<string, string>();
                        if (groupElement.TryGetProperty("description_multilang", out var descMultilangElement) && 
                            descMultilangElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var langProperty in descMultilangElement.EnumerateObject())
                            {
                                if (langProperty.Value.ValueKind == JsonValueKind.String)
                                {
                                    descriptionMultilang[langProperty.Name] = langProperty.Value.GetString();
                                }
                            }
                        }
                        
                        // Створюємо об'єкт групи
                        var group = new Prom.PromGroup
                        {
                            Id = groupId,
                            Name = name,
                            Description = description,
                            Image = image,
                            ParentGroupId = parentGroupId.HasValue ? parentGroupId.Value : 0,
                            NameMultilang = nameMultilang,
                            DescriptionMultilang = descriptionMultilang
                        };
                        
                        groups.Add(group);
                        
                        // Асинхронно зберігаємо групу в базу
                        _ = Task.Run(async () => {
                            try 
                            {
                                // Перевіряємо наявність таблиць перед збереженням
                                try 
                                {
                                    await _promRepository.SaveGroupAsync(group);
                                    _logger.LogInformation("Saved group '{Name}' to database", group.Name);
                                }
                                catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "42P01")
                                {
                                    _logger.LogWarning("Tables for Prom groups don't exist. Skip saving: {Error}", pgEx.Message);
                                }
                            } 
                            catch (Exception ex) 
                            {
                                _logger.LogError(ex, "Error saving group '{Name}' to database", group.Name);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing group element from JSON");
                    }
                }
                
                _logger.LogInformation("Retrieved {Count} groups from Prom.ua API", groups.Count);
                return groups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups from Prom.ua");
                return Enumerable.Empty<Prom.PromGroup>();
            }
        }

        /// <inheritdoc />
        public async Task<Prom.PromGroup> GetGroupByIdAsync(string groupId, string language = null)
        {
            try
            {
                _logger.LogInformation("Getting group from Prom.ua with ID: {GroupId}", groupId);
                
                // Try to get from local database first if we have a numeric ID
                bool retrievedFromDb = false;
                if (long.TryParse(groupId, out long groupIdLong))
                {
                    try
                    {
                        var group = await _promRepository.GetGroupByIdAsync(groupIdLong);
                        if (group != null)
                        {
                            // Convert dynamic object to PromGroup
                            dynamic groupDynamic = group;
                            return new Prom.PromGroup
                            {
                                Id = groupDynamic.Id,
                                Name = groupDynamic.Name,
                                Description = groupDynamic.Description,
                                Image = groupDynamic.Image,
                                ParentGroupId = groupDynamic.ParentGroupId,
                                NameMultilang = groupDynamic.NameMultilang,
                                DescriptionMultilang = groupDynamic.DescriptionMultilang
                            };
                        }
                        retrievedFromDb = true;
                    }
                    catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "42P01")
                    {
                        // Table doesn't exist, skip DB lookup and go straight to API
                        _logger.LogWarning("Tables for Prom groups don't exist. Skipping database lookup: {Error}", pgEx.Message);
                    }
                    catch (Exception ex)
                    {
                        // Log other errors but continue with API as fallback
                        _logger.LogWarning(ex, "Error retrieving group from database, will try API");
                    }
                }

                if (!retrievedFromDb || !string.IsNullOrEmpty(language))
                {
                    // If not in database or language is specified, get from API
                    string url;
                    
                    // Use translation endpoint if language is specified
                    if (!string.IsNullOrEmpty(language))
                    {
                        url = $"groups/translation/{groupId}?lang={language}";
                    }
                    else
                    {
                        url = $"groups/{groupId}";
                    }
                    
                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(content);
                    
                    // Different property names based on whether we are getting a group or translation
                    var rootProperty = string.IsNullOrEmpty(language) ? "group" : string.Empty;
                    var groupElement = rootProperty.Length > 0 
                        ? (document.RootElement.TryGetProperty(rootProperty, out var element) ? element : document.RootElement)
                        : document.RootElement;
                    
                    if (groupElement.ValueKind != JsonValueKind.Object)
                    {
                        _logger.LogWarning("No valid group data found in response");
                        return null;
                    }
                    
                    var promGroup = JsonSerializer.Deserialize<Prom.PromGroup>(groupElement.GetRawText(), _jsonOptions);
                    
                    // Save to database only if tables exist and we have data
                    if (promGroup != null && retrievedFromDb)
                    {
                        try
                        {
                            await _promRepository.SaveGroupAsync(promGroup);
                        }
                        catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "42P01")
                        {
                            _logger.LogWarning("Tables for Prom groups don't exist. Skip saving: {Error}", pgEx.Message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error saving group '{Name}' to database", promGroup.Name);
                        }
                    }
                    
                    return promGroup;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group from Prom.ua with ID: {GroupId}", groupId);
                return null;
            }
        }

        #endregion

        #region Helper Methods

        private Product MapToProduct(Prom.PromProduct promProduct)
        {
            // Getting the marketplace ID
            string marketplaceId = promProduct.Id.ToString();
            
            // Creating a deterministic GUID
            Guid productId;
            if (!Guid.TryParse(marketplaceId, out productId))
            {
                productId = CreateDeterministicGuid(marketplaceId);
            }
            
            var product = new Product
            {
                Id = productId,
                Name = promProduct.Name,
                Price = promProduct.Price,
                Description = promProduct.Description,
                Sku = promProduct.Sku,
                MarketplaceId = marketplaceId,
                MarketplaceType = "Prom.ua",
                Currency = promProduct.Currency,
                Keywords = promProduct.Keywords,
                MainImage = promProduct.MainImage,
                Images = promProduct.Images?.Select(img => img.Url).ToList() ?? new List<string>(),
                DateModified = promProduct.DateModified,
                InStock = promProduct.InStock,
                NameMultilang = promProduct.NameMultilang,
                DescriptionMultilang = promProduct.DescriptionMultilang,
                MarketplaceMappings = new Dictionary<string, string> { { "Prom.ua", marketplaceId } },
                MarketplaceSpecificData = new Dictionary<string, object>
                {
                    { "external_id", promProduct.ExternalId },
                    { "presence", promProduct.Presence },
                    { "minimum_order_quantity", promProduct.MinimumOrderQuantity },
                    { "discount", promProduct.Discount },
                    { "prices", promProduct.Prices },
                    { "selling_type", promProduct.SellingType },
                    { "status", promProduct.Status },
                    { "quantity_in_stock", promProduct.QuantityInStock },
                    { "measure_unit", promProduct.MeasureUnit },
                    { "is_variation", promProduct.IsVariation },
                    { "variation_base_id", promProduct.VariationBaseId },
                    { "variation_group_id", promProduct.VariationGroupId },
                    { "regions", promProduct.Regions }
                }
            };

            // Serialize multilang dictionaries if they exist
            if (promProduct.NameMultilang != null && promProduct.NameMultilang.Count > 0)
            {
                product.MarketplaceSpecificData["name_multilang_json"] = JsonSerializer.Serialize(promProduct.NameMultilang);
            }
            
            if (promProduct.DescriptionMultilang != null && promProduct.DescriptionMultilang.Count > 0)
            {
                product.MarketplaceSpecificData["description_multilang_json"] = JsonSerializer.Serialize(promProduct.DescriptionMultilang);
            }
            
            // Add group data
            if (promProduct.Group != null)
            {
                product.MarketplaceSpecificData["group_id"] = promProduct.Group.Id.ToString();
                product.MarketplaceSpecificData["group_name"] = promProduct.Group.Name;
                product.MarketplaceSpecificData["group_description"] = promProduct.Group.Description;
                product.MarketplaceSpecificData["group_image"] = promProduct.Group.Image;
                
                if (promProduct.Group.ParentGroupId != null)
                {
                    product.MarketplaceSpecificData["group_parent_id"] = promProduct.Group.ParentGroupId.ToString();
                }
                
                // Serialize group multilang data
                if (promProduct.Group.NameMultilang != null && promProduct.Group.NameMultilang.Count > 0)
                {
                    product.MarketplaceSpecificData["group_name_multilang_json"] = JsonSerializer.Serialize(promProduct.Group.NameMultilang);
                }
                
                if (promProduct.Group.DescriptionMultilang != null && promProduct.Group.DescriptionMultilang.Count > 0)
                {
                    product.MarketplaceSpecificData["group_description_multilang_json"] = JsonSerializer.Serialize(promProduct.Group.DescriptionMultilang);
                }
            }
            
            // Add category data
            if (promProduct.Category != null)
            {
                product.MarketplaceSpecificData["category_id"] = promProduct.Category.Id.ToString();
                product.MarketplaceSpecificData["category_name"] = promProduct.Category.Caption;
            }

            // Handle quantity if it's a string or number
            if (promProduct.QuantityInStock != null)
            {
                if (promProduct.QuantityInStock is int quantity)
            {
                product.QuantityInStock = quantity;
                }
                else if (promProduct.QuantityInStock is string quantityStr && int.TryParse(quantityStr, out int parsedQuantity))
                {
                    product.QuantityInStock = parsedQuantity;
                }
            }

            return product;
        }

        private Order MapToOrder(Models.Prom.PromOrder promOrder)
        {
            // Get the marketplace ID
            string marketplaceId = promOrder.Id.ToString();
            
            // Create deterministic GUID
            Guid orderId;
            if (!Guid.TryParse(marketplaceId, out orderId))
            {
                orderId = CreateDeterministicGuid(marketplaceId);
            }
            
            // Конвертуємо ціну, перевіряючи чи це числове значення
            decimal totalAmount = 0;
            if (!string.IsNullOrEmpty(promOrder.Price))
            {
                string priceStr = promOrder.Price;
                // Remove currency symbol and non-numeric characters
                priceStr = new string(priceStr.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
                priceStr = priceStr.Replace(',', '.');
                
                if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPrice))
                {
                    totalAmount = parsedPrice;
                }
            }
            
            var order = new Order
            {
                Id = orderId,
                MarketplaceId = marketplaceId,
                MarketplaceOrderId = marketplaceId,
                MarketplaceType = "Prom.ua",
                MarketplaceName = "Prom.ua",
                CreatedAt = promOrder.DateCreated,
                UpdatedAt = promOrder.DateModified,
                TotalAmount = totalAmount,
                Status = MapStatusFromProm(promOrder.Status),
                ShippingAddress = promOrder.DeliveryAddress,
                ShippingMethod = promOrder.DeliveryOption?.Name,
                PaymentMethod = promOrder.PaymentOption?.Name,
                Source = promOrder.Source ?? "Prom.ua",
                // Set shipping info from deliveryProviderData if available
                ShippingCity = promOrder.DeliveryProviderData?.RecipientAddress?.CityName ?? "",
                ShippingCountry = "Україна", // Default for Prom.ua orders
                TrackingNumber = promOrder.DeliveryProviderData?.DeclarationNumber,
                // Зберігаємо додаткові дані у MarketplaceSpecificData як JSON
                MarketplaceSpecificData = new Dictionary<string, object>
                {
                    { "client_first_name", promOrder.ClientFirstName },
                    { "client_second_name", promOrder.ClientSecondName },
                    { "client_last_name", promOrder.ClientLastName },
                    { "client_id", promOrder.ClientId },
                    { "client_notes", promOrder.ClientNotes },
                    { "email", promOrder.Email },
                    { "phone", promOrder.Phone },
                    { "full_price", promOrder.FullPrice },
                    { "delivery_cost", promOrder.DeliveryCost },
                    { "has_order_promo_free_delivery", promOrder.HasOrderPromoFreeDelivery },
                    { "dont_call_customer_back", promOrder.DontCallCustomerBack },
                    { "status_name", promOrder.StatusName }
                }
            };
            
            // Додаємо інформацію про клієнта
            if (promOrder.Client != null)
            {
                var clientData = new Dictionary<string, object>
                {
                    { "first_name", promOrder.Client.FirstName },
                    { "last_name", promOrder.Client.LastName },
                    { "second_name", promOrder.Client.SecondName },
                    { "phone", promOrder.Client.Phone },
                    { "id", promOrder.Client.Id }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["client_info_json"] = JsonSerializer.Serialize(clientData);
            }
            
            // Додаємо інформацію про отримувача доставки
            if (promOrder.DeliveryRecipient != null)
            {
                var recipientData = new Dictionary<string, object>
                {
                    { "first_name", promOrder.DeliveryRecipient.FirstName },
                    { "last_name", promOrder.DeliveryRecipient.LastName },
                    { "second_name", promOrder.DeliveryRecipient.SecondName },
                    { "phone", promOrder.DeliveryRecipient.Phone }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["delivery_recipient_json"] = JsonSerializer.Serialize(recipientData);
            }
            
            // Додаємо інформацію про доставку, якщо вона є
            if (promOrder.DeliveryOption != null)
            {
                var deliveryOption = new Dictionary<string, object>
                {
                    { "id", promOrder.DeliveryOption.Id },
                    { "name", promOrder.DeliveryOption.Name },
                    { "shipping_service", promOrder.DeliveryOption.ShippingService }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["delivery_option_json"] = JsonSerializer.Serialize(deliveryOption);
            }
            
            // Додаємо інформацію про провайдера доставки, якщо вона є
            if (promOrder.DeliveryProviderData != null)
            {
                var deliveryProviderData = new Dictionary<string, object>
                {
                    { "provider", promOrder.DeliveryProviderData.Provider },
                    { "type", promOrder.DeliveryProviderData.Type },
                    { "sender_warehouse_id", promOrder.DeliveryProviderData.SenderWarehouseId },
                    { "recipient_warehouse_id", promOrder.DeliveryProviderData.RecipientWarehouseId },
                    { "declaration_number", promOrder.DeliveryProviderData.DeclarationNumber },
                    { "unified_status", promOrder.DeliveryProviderData.UnifiedStatus }
                };
                
                // Додаємо адресу отримувача, якщо вона є
                if (promOrder.DeliveryProviderData.RecipientAddress != null)
                {
                    var recipientAddress = new Dictionary<string, object>
                    {
                        { "city_id", promOrder.DeliveryProviderData.RecipientAddress.CityId },
                        { "city_name", promOrder.DeliveryProviderData.RecipientAddress.CityName },
                        { "city_katottg", promOrder.DeliveryProviderData.RecipientAddress.CityKatottg },
                        { "warehouse_id", promOrder.DeliveryProviderData.RecipientAddress.WarehouseId },
                        { "street_id", promOrder.DeliveryProviderData.RecipientAddress.StreetId },
                        { "street_name", promOrder.DeliveryProviderData.RecipientAddress.StreetName },
                        { "building_number", promOrder.DeliveryProviderData.RecipientAddress.BuildingNumber },
                        { "apartment_number", promOrder.DeliveryProviderData.RecipientAddress.ApartmentNumber }
                    };
                    
                    // Serialize to JSON and store
                    deliveryProviderData["recipient_address_json"] = JsonSerializer.Serialize(recipientAddress);
                }
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["delivery_provider_data_json"] = JsonSerializer.Serialize(deliveryProviderData);
            }
            
            // Set DeliveryService based on provider
            if (promOrder.DeliveryProviderData?.Provider == "nova_poshta")
            {
                order.DeliveryService = "Нова Пошта";
            }
            
            // Додаємо інформацію про оплату, якщо вона є
            if (promOrder.PaymentOption != null)
            {
                var paymentOption = new Dictionary<string, object>
                {
                    { "id", promOrder.PaymentOption.Id },
                    { "name", promOrder.PaymentOption.Name }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["payment_option_json"] = JsonSerializer.Serialize(paymentOption);
            }
            
            if (promOrder.PaymentData != null)
            {
                var paymentData = new Dictionary<string, object>
                {
                    { "type", promOrder.PaymentData.Type },
                    { "status", promOrder.PaymentData.Status },
                    { "status_modified", promOrder.PaymentData.StatusModified }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["payment_data_json"] = JsonSerializer.Serialize(paymentData);
            }
            
            // Add price related fields
            if (!string.IsNullOrEmpty(promOrder.PriceWithSpecialOffer))
            {
                order.MarketplaceSpecificData["price_with_special_offer"] = promOrder.PriceWithSpecialOffer;
            }
            
            if (!string.IsNullOrEmpty(promOrder.SpecialOfferDiscount))
            {
                order.MarketplaceSpecificData["special_offer_discount"] = promOrder.SpecialOfferDiscount;
            }
            
            if (!string.IsNullOrEmpty(promOrder.SpecialOfferPromocode))
            {
                order.MarketplaceSpecificData["special_offer_promocode"] = promOrder.SpecialOfferPromocode;
            }
            
            // Додаємо інформацію про комісію, якщо вона є
            if (promOrder.CpaCommission != null)
            {
                var cpaCommission = new Dictionary<string, object>
                {
                    { "amount", promOrder.CpaCommission.Amount },
                    { "is_refunded", promOrder.CpaCommission.IsRefunded }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["cpa_commission_json"] = JsonSerializer.Serialize(cpaCommission);
            }
            
            // Додаємо UTM-мітки, якщо вони є
            if (promOrder.Utm != null)
            {
                var utm = new Dictionary<string, object>
                {
                    { "medium", promOrder.Utm.Medium },
                    { "source", promOrder.Utm.Source },
                    { "campaign", promOrder.Utm.Campaign }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["utm_json"] = JsonSerializer.Serialize(utm);
            }
            
            // Додаємо інформацію про промоакції, якщо вона є
            if (promOrder.PsPromotion != null)
            {
                var psPromotion = new Dictionary<string, object>
                {
                    { "name", promOrder.PsPromotion.Name },
                    { "conditions", promOrder.PsPromotion.Conditions }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["ps_promotion_json"] = JsonSerializer.Serialize(psPromotion);
            }
            
            // Додаємо інформацію про скасування, якщо вона є
            if (promOrder.Cancellation != null)
            {
                var cancellation = new Dictionary<string, object>
                {
                    { "title", promOrder.Cancellation.Title },
                    { "initiator", promOrder.Cancellation.Initiator }
                };
                
                // Serialize to JSON and store
                order.MarketplaceSpecificData["cancellation_json"] = JsonSerializer.Serialize(cancellation);
            }
            
            // Створюємо список для позицій замовлення
            order.Items = new List<Tsintra.Domain.Models.OrderItem>();

            // Додаємо товари замовлення
            if (promOrder.Products != null && promOrder.Products.Any())
            {
                foreach (var product in promOrder.Products)
                {
                    // Конвертуємо ціни, очищаючи від символів валюти
                    decimal unitPrice = 0;
                    if (product.Price != 0)
                    {
                        unitPrice = product.Price;
                    }
                    
                    decimal totalPrice = 0;
                    if (product.TotalPrice != 0)
                    {
                        totalPrice = product.TotalPrice;
                    }
                    
                    // Створюємо детермінований GUID для позиції замовлення
                    Guid itemId = CreateDeterministicGuid($"{marketplaceId}_{product.Id}_{product.Sku}");
                    
                    // Create the order item object
                    var orderItem = new Tsintra.Domain.Models.OrderItem
                    {
                        Id = itemId,
                        OrderId = order.Id,
                        ProductId = product.Id.ToString(),
                        ProductSku = product.Sku ?? "",
                        ExternalId = product.ExternalId,
                        ProductName = product.Name,
                        Quantity = product.Quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = totalPrice,
                        Currency = "UAH",
                        ImageUrl = product.Image,
                        ProductUrl = product.Url,
                        SpecificData = new Dictionary<string, object>()
                    };
                    
                    // Add the original quantity string
                    orderItem.SpecificData["quantity_original"] = product.QuantityStr;
                    
                    // Serialize any nested dictionaries to JSON
                    if (product.NameMultilang != null && product.NameMultilang.Count > 0)
                    {
                        orderItem.SpecificData["name_multilang_json"] = JsonSerializer.Serialize(product.NameMultilang);
                    }
                    
                    if (product.MeasureUnit != null)
                    {
                        orderItem.SpecificData["measure_unit"] = product.MeasureUnit;
                    }
                    
                    if (product.CpaCommission != null)
                    {
                        var cpaCommission = new Dictionary<string, object>
                        {
                            { "amount", product.CpaCommission.Amount }
                        };
                        
                        orderItem.SpecificData["cpa_commission_json"] = JsonSerializer.Serialize(cpaCommission);
                    }
                    
                    order.Items.Add(orderItem);
                }
            }

            return order;
        }

        private async Task<Customer> EnsureCustomerExistsAsync(Order order)
        {
            // Get customer information from order data
            string email = string.Empty;
            string phone = string.Empty;
            string name = string.Empty;
            
            if (order.MarketplaceSpecificData != null)
            {
                if (order.MarketplaceSpecificData.TryGetValue("client_email", out object emailObj))
                {
                    email = emailObj?.ToString() ?? string.Empty;
                }
                
                if (order.MarketplaceSpecificData.TryGetValue("client_phone", out object phoneObj))
                {
                    phone = phoneObj?.ToString() ?? string.Empty;
                }
                
                if (order.MarketplaceSpecificData.TryGetValue("client_first_name", out object firstNameObj) &&
                    order.MarketplaceSpecificData.TryGetValue("client_last_name", out object lastNameObj))
                {
                    name = $"{firstNameObj} {lastNameObj}".Trim();
                }
            }

            // Try to find customer by email first
            Customer customer = null;
            if (!string.IsNullOrEmpty(email))
            {
                customer = await _customerRepository.GetByEmailAsync(email);
            }

            // If not found by email, try to find by phone
            if (customer == null && !string.IsNullOrEmpty(phone))
            {
                customer = await _customerRepository.GetByPhoneAsync(phone);
            }

            // If customer not found, create a new one
            if (customer == null)
            {
                // Створюємо стабільний ідентифікатор для клієнта
                string customerKey = !string.IsNullOrEmpty(email) ? email : phone;
                
                // Якщо є і email, і телефон, використовуємо обидва для більшої унікальності
                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(phone))
                {
                    customerKey = $"{email}_{phone}";
                }
                
                // Створюємо детермінований Guid
                Guid customerId = CreateDeterministicGuid(customerKey);
                
                customer = new Customer
                {
                    Id = customerId,
                    Email = email,
                    Phone = phone,
                    Name = name,
                    CreatedAt = DateTime.UtcNow,
                    MarketplaceType = order.MarketplaceType,
                    MarketplaceId = order.MarketplaceId,
                    // Копіюємо MarketplaceSpecificData з замовлення в клієнта
                    MarketplaceSpecificData = order.MarketplaceSpecificData
                };

                await _customerRepository.AddAsync(customer);
            }

            return customer;
        }

        private OrderStatus MapStatusFromProm(string promStatus)
        {
            switch (promStatus.ToLower())
            {
                case "pending":
                    return OrderStatus.Pending;
                case "received":
                    return OrderStatus.Processing;
                case "delivered":
                    return OrderStatus.Delivered;
                case "canceled":
                    return OrderStatus.Cancelled;
                case "draft":
                    return OrderStatus.Processing; // Changed from Draft to Processing
                default:
                    return OrderStatus.Pending; // Changed from New to Pending
            }
        }

        private string MapStatusToProm(OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Pending:
                    return "pending";
                case OrderStatus.Processing:
                    return "received";
                case OrderStatus.Shipped:
                    return "delivering";
                case OrderStatus.Delivered:
                    return "delivered";
                case OrderStatus.Cancelled:
                    return "canceled";
                case OrderStatus.OnHold:
                    return "canceled";
                default:
                    return "pending";
            }
        }

        // Метод для створення детермінованого Guid на основі рядка
        private Guid CreateDeterministicGuid(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return Guid.NewGuid();
            }
            
            // Використовуємо MD5 для створення хешу з рядка
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);
                
                // Перетворюємо байти хешу в Guid
                return new Guid(hashBytes);
            }
        }

        // Метод для екранування спеціальних символів у JSON
        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
                
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Синхронізує товари між Prom.ua і базою даних
        /// </summary>
        /// <param name="syncDirection">Напрямок синхронізації: Import (з Prom.ua в базу), Export (з бази в Prom.ua), Both (двостороння)</param>
        /// <param name="productIds">Опціональний список ID товарів для синхронізації</param>
        /// <returns>Інформація про результат синхронізації</returns>
        public async Task<(int Imported, int Exported, int Failed)> SyncProductsWithDatabaseAsync(
            SyncDirection syncDirection = SyncDirection.Both, 
            IEnumerable<Guid> productIds = null)
        {
            try
            {
                _logger.LogInformation("Syncing products with database, direction: {SyncDirection}", syncDirection);
                
                int imported = 0;
                int exported = 0;
                int failed = 0;
                
                // Імпорт з Prom.ua в базу даних
                if (syncDirection == SyncDirection.Import || syncDirection == SyncDirection.Both)
                {
                    try
                    {
                        imported = await ImportProductsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error importing products from Prom.ua");
                        failed++;
                    }
                }
                
                // Експорт з бази даних на Prom.ua
                if (syncDirection == SyncDirection.Export || syncDirection == SyncDirection.Both)
                {
                    try
                    {
                        exported = await ExportProductsAsync(productIds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error exporting products to Prom.ua");
                        failed++;
                    }
                }
                
                _logger.LogInformation("Sync completed: Imported {Imported}, Exported {Exported}, Failed {Failed}", 
                    imported, exported, failed);
                    
                return (imported, exported, failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing products with database");
                return (0, 0, 1);
            }
        }

        #endregion

        /// <inheritdoc />
        public async Task<IEnumerable<Models.Prom.PromOrder>> GetRawOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Getting raw orders from Prom.ua API");
                
                // Побудова параметрів запиту
                var queryParams = new List<string>();
                if (startDate.HasValue)
                {
                    queryParams.Add($"date_from={startDate.Value:yyyy-MM-dd}");
                }
                
                if (endDate.HasValue)
                {
                    queryParams.Add($"date_to={endDate.Value:yyyy-MM-dd}");
                }
                
                string url = "orders/list";
                if (queryParams.Any())
                {
                    url += "?" + string.Join("&", queryParams);
                }
                
                _logger.LogInformation("Calling Prom API: {Url}", url);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received response from Prom API: {Length} bytes", content.Length);
                
                // Налаштування опцій десеріалізації для кращої обробки помилок
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                
                try
                {
                    // Спочатку спробуємо десеріалізувати за допомогою нашої власної моделі
                    var ordersResponse = JsonSerializer.Deserialize<Models.Prom.PromOrdersResponse>(content, options);
                    
                    if (ordersResponse?.Orders == null || !ordersResponse.Orders.Any())
                    {
                        _logger.LogWarning("No orders found in Prom.ua API response");
                        return Enumerable.Empty<Models.Prom.PromOrder>();
                    }

                    _logger.LogInformation("Retrieved {Count} raw orders from Prom.ua", ordersResponse.Orders.Count);
                    return ordersResponse.Orders;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing Prom.ua API response. Attempting manual JSON parsing.");
                    
                    // Якщо стандартна десеріалізація не вдалася, спробуємо аналіз JSON вручну
                    try
                    {
                        using var document = JsonDocument.Parse(content);
                        
                        if (document.RootElement.TryGetProperty("orders", out var ordersElement) && 
                            ordersElement.ValueKind == JsonValueKind.Array)
                        {
                            var orders = new List<Models.Prom.PromOrder>();
                            
                            foreach (var orderElement in ordersElement.EnumerateArray())
                            {
                                try
                                {
                                    var order = ParsePromOrderFromJson(orderElement);
                                    if (order != null)
                                    {
                                        orders.Add(order);
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogWarning(parseEx, "Error parsing individual order from JSON. Skipping this order.");
                                }
                            }
                            
                            _logger.LogInformation("Manually parsed {Count} raw orders from Prom.ua", orders.Count);
                            return orders;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogError(jsonEx, "Error parsing Prom.ua API response JSON manually.");
                    }
                    
                    // Якщо всі спроби не вдалися, повертаємо порожній список
                    return Enumerable.Empty<Models.Prom.PromOrder>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving raw orders from Prom.ua API");
                return Enumerable.Empty<Models.Prom.PromOrder>();
            }
        }

        /// <inheritdoc />
        public async Task<Models.Prom.PromOrder> GetRawOrderByIdAsync(string orderId)
        {
            try
            {
                _logger.LogInformation("Getting raw order from Prom.ua with ID: {OrderId}", orderId);
                
                var response = await _httpClient.GetAsync($"orders/{orderId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get order from Prom.ua. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received response from Prom API: {Length} bytes", content.Length);
                
                // Parse response
                using var document = JsonDocument.Parse(content);
                
                if (!document.RootElement.TryGetProperty("order", out var orderElement) || 
                    orderElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("No 'order' element found in response or it's not an object");
                    return null;
                }
                
                try
                {
                    // Try to deserialize to our model
                    var promOrder = JsonSerializer.Deserialize<Models.Prom.PromOrder>(orderElement.GetRawText(), _jsonOptions);
                    
                    if (promOrder == null)
                    {
                        _logger.LogWarning("Failed to deserialize order from Prom.ua API");
                        return null;
                    }
                    
                    return promOrder;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Error deserializing Prom.ua API response. Attempting manual JSON parsing.");
                    
                    // If standard deserialization failed, try manual parsing
                    try
                    {
                        var promOrder = ParsePromOrderFromJson(orderElement);
                        return promOrder;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error manually parsing order from Prom.ua API");
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting raw order from Prom.ua with ID: {OrderId}", orderId);
                return null;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Deserializes a JSON string from the database back to a Dictionary<string, string>
        /// </summary>
        private Dictionary<string, string> DeserializeJsonToDictionary(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing JSON string to dictionary: {JsonString}", jsonString);
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Extracts a Dictionary<string, string> from MarketplaceSpecificData by key
        /// </summary>
        private Dictionary<string, string> ExtractDictionaryFromMarketplaceData(Dictionary<string, object> marketplaceData, string key)
        {
            if (marketplaceData == null || !marketplaceData.ContainsKey(key))
            {
                return new Dictionary<string, string>();
            }

            if (marketplaceData[key] is string jsonString)
            {
                return DeserializeJsonToDictionary(jsonString);
            }
            
            return new Dictionary<string, string>();
        }

        /// <summary>
        /// Safely retrieves a dictionary from the product's MarketplaceSpecificData by key
        /// </summary>
        /// <param name="product">The product to extract dictionary data from</param>
        /// <param name="key">The key in MarketplaceSpecificData that holds the serialized dictionary</param>
        /// <returns>A dictionary of string keys/values</returns>
        private Dictionary<string, string> GetDictionaryFromProduct(Product product, string key)
        {
            if (product?.MarketplaceSpecificData == null || !product.MarketplaceSpecificData.ContainsKey(key))
            {
                return new Dictionary<string, string>();
            }

            if (product.MarketplaceSpecificData[key] is string jsonString)
            {
                return DeserializeJsonToDictionary(jsonString);
            }
            
            return new Dictionary<string, string>();
        }

        private Product MapPromProductToProduct(dynamic promProduct)
        {
            try
            {
                // Create Product from Prom product data
                string marketplaceId = promProduct.Id.ToString();
                
                // Create a deterministic GUID
                Guid productId;
                if (!Guid.TryParse(marketplaceId, out productId))
                {
                    productId = CreateDeterministicGuid(marketplaceId);
                }
                
                var product = new Product
                {
                    Id = productId,
                    Name = promProduct.Name,
                    Description = promProduct.Description,
                    Sku = promProduct.Sku,
                    Price = promProduct.Price,
                    Currency = promProduct.Currency,
                    MarketplaceId = marketplaceId,
                    MarketplaceType = "Prom.ua"
                };

                // Handle multilang data
                if (promProduct.MarketplaceSpecificData?.ContainsKey("name_multilang_json") == true)
                {
                    product.NameMultilang = GetDictionaryFromProduct(product, "name_multilang_json");
                }
                
                if (promProduct.MarketplaceSpecificData?.ContainsKey("description_multilang_json") == true)
                {
                    product.DescriptionMultilang = GetDictionaryFromProduct(product, "description_multilang_json");
                }
                
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping Prom product to Product");
                throw;
            }
        }

        #endregion

        /// <summary>
        /// Розбирає елемент JsonElement у об'єкт PromOrder
        /// </summary>
        private Models.Prom.PromOrder ParsePromOrderFromJson(JsonElement orderElement)
        {
            var order = new Models.Prom.PromOrder();
            
            // Основні властивості замовлення
            if (orderElement.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number)
            {
                order.Id = idElement.GetInt64();
            }
            
            if (orderElement.TryGetProperty("date_created", out var dateElement) && dateElement.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(dateElement.GetString(), out var date))
                {
                    order.DateCreated = date;
                }
            }
            
            if (orderElement.TryGetProperty("client_first_name", out var firstNameElement) && firstNameElement.ValueKind == JsonValueKind.String)
            {
                order.ClientFirstName = firstNameElement.GetString();
            }
            
            if (orderElement.TryGetProperty("client_second_name", out var secondNameElement) && secondNameElement.ValueKind == JsonValueKind.String)
            {
                order.ClientSecondName = secondNameElement.GetString();
            }
            
            if (orderElement.TryGetProperty("client_last_name", out var lastNameElement) && lastNameElement.ValueKind == JsonValueKind.String)
            {
                order.ClientLastName = lastNameElement.GetString();
            }
            
            if (orderElement.TryGetProperty("client_id", out var clientIdElement) && clientIdElement.ValueKind == JsonValueKind.Number)
            {
                order.ClientId = clientIdElement.GetInt64();
            }
            
            if (orderElement.TryGetProperty("client_notes", out var notesElement) && notesElement.ValueKind == JsonValueKind.String)
            {
                order.ClientNotes = notesElement.GetString();
            }
            
            if (orderElement.TryGetProperty("phone", out var phoneElement) && phoneElement.ValueKind == JsonValueKind.String)
            {
                order.Phone = phoneElement.GetString();
            }
            
            if (orderElement.TryGetProperty("email", out var emailElement) && emailElement.ValueKind == JsonValueKind.String)
            {
                order.Email = emailElement.GetString();
            }
            
            if (orderElement.TryGetProperty("price", out var priceElement) && priceElement.ValueKind == JsonValueKind.String)
            {
                order.Price = priceElement.GetString();
            }
            else if (orderElement.TryGetProperty("price", out var priceNumElement) && priceNumElement.ValueKind == JsonValueKind.Number)
            {
                order.Price = priceNumElement.ToString();
            }
            
            if (orderElement.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String)
            {
                order.Status = statusElement.GetString();
            }
            
            if (orderElement.TryGetProperty("status_name", out var statusNameElement) && statusNameElement.ValueKind == JsonValueKind.String)
            {
                order.StatusName = statusNameElement.GetString();
            }
            
            if (orderElement.TryGetProperty("source", out var sourceElement) && sourceElement.ValueKind == JsonValueKind.String)
            {
                order.Source = sourceElement.GetString();
            }
            
            // Розбір товарів
            if (orderElement.TryGetProperty("products", out var productsElement) && productsElement.ValueKind == JsonValueKind.Array)
            {
                order.Products = new List<Models.Prom.PromOrderProduct>();
                
                foreach (var productElement in productsElement.EnumerateArray())
                {
                    var product = new Models.Prom.PromOrderProduct();
                    
                    if (productElement.TryGetProperty("id", out var productIdElement) && productIdElement.ValueKind == JsonValueKind.Number)
                    {
                        product.Id = productIdElement.GetInt64();
                    }
                    
                    if (productElement.TryGetProperty("external_id", out var externalIdElement) && externalIdElement.ValueKind == JsonValueKind.String)
                    {
                        product.ExternalId = externalIdElement.GetString();
                    }
                    
                    if (productElement.TryGetProperty("image", out var imageElement) && imageElement.ValueKind == JsonValueKind.String)
                    {
                        product.Image = imageElement.GetString();
                    }
                    
                    // Обробка поля quantity, яке може бути як числом, так і рядком
                    if (productElement.TryGetProperty("quantity", out var quantityElement))
                    {
                        if (quantityElement.ValueKind == JsonValueKind.Number)
                        {
                            product.QuantityStr = quantityElement.ToString();
                        }
                        else if (quantityElement.ValueKind == JsonValueKind.String)
                        {
                            product.QuantityStr = quantityElement.GetString();
                        }
                    }
                    
                    decimal price = 0;
                    if (productElement.TryGetProperty("price", out var productPriceElement))
                    {
                        if (productPriceElement.ValueKind == JsonValueKind.String)
                        {
                            string priceStr = productPriceElement.GetString() ?? "0";
                            decimal.TryParse(priceStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out price);
                            product.Price = price;
                        }
                        else if (productPriceElement.ValueKind == JsonValueKind.Number)
                        {
                            if (productPriceElement.TryGetDecimal(out decimal decimalPrice))
                            {
                                product.Price = decimalPrice;
                            }
                        }
                    }
                    
                    if (productElement.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String)
                    {
                        product.Url = urlElement.GetString();
                    }
                    
                    if (productElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                    {
                        product.Name = nameElement.GetString();
                    }
                    
                    decimal totalPrice = 0;
                    if (productElement.TryGetProperty("total_price", out var totalPriceElement))
                    {
                        if (totalPriceElement.ValueKind == JsonValueKind.String)
                        {
                            string totalPriceStr = totalPriceElement.GetString() ?? "0";
                            decimal.TryParse(totalPriceStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out totalPrice);
                            product.TotalPrice = totalPrice;
                        }
                        else if (totalPriceElement.ValueKind == JsonValueKind.Number)
                        {
                            if (totalPriceElement.TryGetDecimal(out decimal decimalTotalPrice))
                            {
                                product.TotalPrice = decimalTotalPrice;
                            }
                        }
                    }
                    
                    if (productElement.TryGetProperty("measure_unit", out var measureUnitElement) && measureUnitElement.ValueKind == JsonValueKind.String)
                    {
                        product.MeasureUnit = measureUnitElement.GetString();
                    }
                    
                    if (productElement.TryGetProperty("sku", out var skuElement) && skuElement.ValueKind == JsonValueKind.String)
                    {
                        product.Sku = skuElement.GetString();
                    }
                    
                    order.Products.Add(product);
                }
            }
            
            return order;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateProductAsync(Product product)
        {
            try
            {
                _logger.LogInformation("Updating product in Prom.ua with ID: {MarketplaceProductId}", product.MarketplaceId);
                
                // Валідація обов'язкових полів
                if (string.IsNullOrEmpty(product.Name))
                {
                    _logger.LogError("Product name is required for Prom.ua API");
                    return false;
                }

                if (product.Price <= 0)
                {
                    _logger.LogError("Product price must be greater than 0 for Prom.ua API");
                    return false;
                }
                
                if (string.IsNullOrEmpty(product.MarketplaceId))
                {
                    _logger.LogError("MarketplaceId is required for updating product in Prom.ua");
                    return false;
                }

                // Створюємо об'єкт запиту на основі моделі PromProductRequest
                var promProductRequest = new Prom.PromProductRequest
                {
                    Product = new Prom.PromProductData
                    {
                        Id = long.TryParse(product.MarketplaceId, out long idValue) ? idValue : 0,
                        Name = product.Name,
                        Price = product.Price,
                        Sku = product.Sku ?? string.Empty,
                        Description = product.Description ?? string.Empty,
                        Currency = product.Currency ?? "UAH",
                        Keywords = product.Keywords ?? string.Empty,
                        QuantityInStock = product.QuantityInStock ?? 0,
                        Status = product.Status ?? "on_display",
                        Presence = "available" // За замовчуванням товар доступний для покупки
                    }
                };

                // Додаємо багатомовні поля, якщо вони є
                if (product.NameMultilang != null && product.NameMultilang.Count > 0)
                {
                    promProductRequest.Product.NameMultilang = product.NameMultilang;
                }

                if (product.DescriptionMultilang != null && product.DescriptionMultilang.Count > 0)
                {
                    promProductRequest.Product.DescriptionMultilang = product.DescriptionMultilang;
                }

                // Додаємо зображення, якщо вони є
                if (product.Images != null && product.Images.Count > 0)
                {
                    promProductRequest.Product.Images = product.Images;
                    promProductRequest.Product.MainImage = product.MainImage ?? product.Images.FirstOrDefault();
                }
                
                // Додаємо дані групи, якщо вони є
                if (!string.IsNullOrEmpty(product.GroupId) && long.TryParse(product.GroupId, out long groupId))
                {
                    promProductRequest.Product.GroupId = groupId;
                }
                
                // Додаємо зовнішній ID, якщо він є
                if (!string.IsNullOrEmpty(product.ExternalId))
                {
                    promProductRequest.Product.ExternalId = product.ExternalId;
                }
                
                // Додаємо додаткові дані, якщо вони є в MarketplaceSpecificData
                if (product.MarketplaceSpecificData != null)
                {
                    // MeasureUnit
                    if (product.MarketplaceSpecificData.TryGetValue("measure_unit", out var measureUnit) && measureUnit != null)
                    {
                        promProductRequest.Product.MeasureUnit = measureUnit.ToString();
                    }
                    
                    // Presence (наявність)
                    if (product.MarketplaceSpecificData.TryGetValue("presence", out var presence) && presence != null)
                    {
                        promProductRequest.Product.Presence = presence.ToString();
                    }
                    
                    // Знижка
                    if (product.MarketplaceSpecificData.TryGetValue("discount", out var discount) && discount != null 
                        && decimal.TryParse(discount.ToString(), out decimal discountValue))
                    {
                        promProductRequest.Product.Discount = discountValue;
                    }
                    
                    // Мінімальна кількість для замовлення
                    if (product.MarketplaceSpecificData.TryGetValue("minimum_order_quantity", out var minOrderQty) && minOrderQty != null 
                        && int.TryParse(minOrderQty.ToString(), out int minQty))
                    {
                        promProductRequest.Product.MinimumOrderQuantity = minQty;
                    }
                    
                    // Категорія
                    if (product.MarketplaceSpecificData.TryGetValue("category_id", out var categoryId) && categoryId != null 
                        && long.TryParse(categoryId.ToString(), out long catId))
                    {
                        promProductRequest.Product.CategoryId = catId;
                    }
                    
                    // Варіації
                    if (product.MarketplaceSpecificData.TryGetValue("is_variation", out var isVariation) && isVariation != null 
                        && bool.TryParse(isVariation.ToString(), out bool isVar))
                    {
                        promProductRequest.Product.IsVariation = isVar;
                    }
                    
                    if (product.MarketplaceSpecificData.TryGetValue("variation_base_id", out var variationBaseId) && variationBaseId != null 
                        && long.TryParse(variationBaseId.ToString(), out long varBaseId))
                    {
                        promProductRequest.Product.VariationBaseId = varBaseId;
                    }
                    
                    if (product.MarketplaceSpecificData.TryGetValue("variation_group_id", out var variationGroupId) && variationGroupId != null 
                        && long.TryParse(variationGroupId.ToString(), out long varGroupId))
                    {
                        promProductRequest.Product.VariationGroupId = varGroupId;
                    }
                }
                
                // Серіалізуємо об'єкт запиту в JSON
                var jsonContent = JsonSerializer.Serialize(promProductRequest, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Sending request to Prom.ua API: {JsonContent}", jsonContent);

                // Використовуємо правильний ендпоінт Prom API для оновлення товару
                var response = await _httpClient.PostAsync("products/edit", content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Update product response: {Response}", responseContent);
                
                // Перевіряємо на наявність помилки у відповіді
                if (responseContent.Contains("error"))
                {
                    _logger.LogError("Error response from Prom.ua API: {Response}", responseContent);
                    return false;
                }
                
                response.EnsureSuccessStatusCode();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product in Prom.ua with ID: {MarketplaceProductId}. Error: {ErrorMessage}", 
                    product.MarketplaceId, ex.Message);
                return false;
            }
        }
    }
}