using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Tsintra.Domain.Interfaces;
using Tsintra.Core.Models;
using Tsintra.Domain.DTOs;
using Tsintra.Integrations.Prom.Models;

namespace Tsintra.Integrations.Prom;

public class PromUAClient : IMarketplaceClient
{
    private readonly HttpClient _httpClient;
    private readonly PromUaOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<PromUAClient> _logger;

    public PromUAClient(HttpClient httpClient, IOptions<PromUaOptions> options, ILogger<PromUAClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options), "PromUA options cannot be null.");
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ArgumentException("PromUA API Key is missing in configuration.", nameof(options));
        }
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
             throw new ArgumentException("PromUA BaseUrl is missing in configuration.", nameof(options));
        }

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        
        // Змінити формат заголовка на той, який вимагає Prom API
        _logger.LogInformation("Configuring PromUAClient with BaseUrl: {BaseUrl}", _options.BaseUrl);
        _logger.LogInformation("API Key (first 4 chars): {ApiKeyPrefix}...", _options.ApiKey.Substring(0, 4));
        
        // Якщо Prom API очікує Bearer токен:
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _logger.LogInformation("Set Authorization header: Bearer {ApiKeyPrefix}...", _options.ApiKey.Substring(0, 4));
        
        // Якщо Prom API очікує просто Authorization без "Bearer":
        // _httpClient.DefaultRequestHeaders.Add("Authorization", _options.ApiKey);
        // _logger.LogInformation("Set Authorization header: {ApiKeyPrefix}...", _options.ApiKey.Substring(0, 4));
        
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _logger.LogInformation("Added Accept header: application/json");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
            // Add other necessary JSON serialization options here
        };
    }

    public async Task<IEnumerable<MarketplaceProduct>> GetProductsAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("GetProductsAsync: Sending request to '{BaseAddress}products/list'", _httpClient.BaseAddress);
            _logger.LogInformation("Request headers:");
            foreach (var header in _httpClient.DefaultRequestHeaders)
            {
                _logger.LogInformation("  {HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
            }
            
            var response = await _httpClient.GetAsync($"products/list", ct);
            
            // Логування для діагностики відповіді
            _logger.LogInformation("Response status code: {StatusCode}", response.StatusCode);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Error response from Prom API: {StatusCode} {Content}", response.StatusCode, errorContent);
                return Enumerable.Empty<MarketplaceProduct>();
            }
            
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("Response content length: {Length} characters", content.Length);
            
            var productListResponse = JsonSerializer.Deserialize<PromUAProductListResponse>(content);
            if (productListResponse?.Products != null)
            {
                var products = new List<MarketplaceProduct>();
                foreach (var product in productListResponse.Products)
                {
                    products.Add(MapToMarketplaceProduct(product));
                }
                return products;
            }
            return Enumerable.Empty<MarketplaceProduct>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetProductsAsync");
            return Enumerable.Empty<MarketplaceProduct>();
        }
    }

    public async Task<MarketplaceProduct?> GetProductByIdAsync(string id)
    {
        try
        {
            // Adjust the endpoint path as needed according to Prom.ua API documentation
            var response = await _httpClient.GetAsync($"products/{id}");
            response.EnsureSuccessStatusCode(); // Throw if not a success code

            var productData = await response.Content.ReadFromJsonAsync<PromProductResponse>(_jsonOptions);

            // Map the response to your domain model (simple manual mapping here)
            if (productData?.Product != null)
            {
                return new MarketplaceProduct(
                    Id: productData.Product.Id.ToString(), // Assuming Id is long in response
                    Name: productData.Product.Name,
                    Price: productData.Product.Price,
                    Description: productData.Product.Description ?? string.Empty,
                    SpecificAttributes: new Dictionary<string, object>() // Add specific attributes if available
                );
            }
            return null;
        }
        catch (HttpRequestException ex)
        {
            // Log the error (consider injecting ILogger)
            Console.WriteLine($"HTTP request error fetching product {id}: {ex.Message}"); 
            return null;
        }
        catch (JsonException ex)
        {
             // Log the error
            Console.WriteLine($"JSON parsing error fetching product {id}: {ex.Message}");
            return null;
        }
         catch (Exception ex) // Catch other potential errors
        {
            Console.WriteLine($"Generic error fetching product {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<MarketplaceProduct> AddProductAsync(MarketplaceProduct product, CancellationToken ct = default)
    {
        var promUAProductCreateRequest = new
        {
            name = product.Name,
            price = product.Price,
            description = product.Description,
            // Додайте інші необхідні поля з документації Prom.ua
        };

        var json = JsonSerializer.Serialize(promUAProductCreateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"products", content, ct);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        try
        {
            var createdProductResponse = JsonSerializer.Deserialize<PromUACreatedProductResponse>(responseContent);
            if (createdProductResponse?.Id != null)
            {
                return product with { Id = createdProductResponse.Id.ToString() };
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("Не вдалося отримати ID створеного продукту з відповіді.");
            return product;
        }

        return product;
    }

    public async Task<MarketplaceProduct> UpdateProductAsync(MarketplaceProduct product, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(product.Id))
        {
            throw new ArgumentException("ID продукту не може бути порожнім для оновлення.");
        }

        var promUAProductUpdateRequest = new
        {
            name = product.Name,
            price = product.Price,
            description = product.Description,
            // Додайте інші поля, які ви хочете оновити
        };

        var json = JsonSerializer.Serialize(promUAProductUpdateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync($"products/{product.Id}", content, ct);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        try
        {
            var updatedProductResponse = JsonSerializer.Deserialize<PromProduct>(responseContent);
            if (updatedProductResponse?.Id.ToString() == product.Id)
            {
                return MapToMarketplaceProduct(updatedProductResponse);
            }
            else
            {
                return product;
            }
        }
        catch (JsonException)
        {
            return product;
        }
    }

    public Task DeleteProductAsync(string productId, CancellationToken ct = default)
    {
        throw new NotImplementedException("Видалення продукту не реалізовано.");
    }

    private Core.Models.MarketplaceProduct MapToMarketplaceProduct(PromProduct promProduct)
    {
        return new MarketplaceProduct(
            Id: promProduct.Id.ToString(),
            Name: promProduct.Name,
            Price: promProduct.Price,
            Description: promProduct.Description ?? string.Empty,
            SpecificAttributes: new Dictionary<string, object>()
            {
                { "external_id", promProduct.ExternalId ?? null },
                { "sku", promProduct.Sku ?? string.Empty },
                { "keywords", promProduct.Keywords ?? string.Empty },
                { "presence", promProduct.Presence ?? "available" },
                { "minimum_order_quantity", promProduct.MinimumOrderQuantity },
                { "discount", promProduct.Discount },
                { "prices", promProduct.Prices ?? new List<object>() },
                { "currency", promProduct.Currency ?? "UAH" },
                { "group_id", promProduct.Group?.Id.ToString() ?? string.Empty },
                { "group_name", promProduct.Group?.Name ?? string.Empty },
                { "group_name_multilang", promProduct.Group?.NameMultilang ?? new Dictionary<string, string>() },
                { "category_id", promProduct.Category?.Id.ToString() ?? string.Empty },
                { "category_caption", promProduct.Category?.Caption ?? string.Empty },
                { "main_image", promProduct.MainImage ?? string.Empty },
                { "images", promProduct.Images != null ? System.Text.Json.JsonSerializer.Serialize(promProduct.Images) : "[]" },
                { "selling_type", promProduct.SellingType ?? "retail" },
                { "status", promProduct.Status ?? "on_display" },
                { "quantity_in_stock", promProduct.QuantityInStock },
                { "measure_unit", promProduct.MeasureUnit ?? "шт." },
                { "is_variation", promProduct.IsVariation },
                { "variation_base_id", promProduct.VariationBaseId },
                { "variation_group_id", promProduct.VariationGroupId },
                { "date_modified", promProduct.DateModified },
                { "in_stock", promProduct.InStock },
                { "regions", promProduct.Regions ?? new List<object>() },
                { "name_multilang", promProduct.NameMultilang ?? new Dictionary<string, string>() },
                { "description_multilang", promProduct.DescriptionMultilang ?? new Dictionary<string, string>() }
            }
        );
    }

    // Placeholder for the response structure from Prom.ua API
    // Adjust this based on the actual API response
    private class PromProductResponse
    {
        public PromProduct? Product { get; set; }
    }

    private class PromProduct
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
        // Add other fields returned by the API
        public string? ExternalId { get; set; }
        public string? Sku { get; set; }
        public string? Keywords { get; set; }
        public string? Presence { get; set; }
        public int? MinimumOrderQuantity { get; set; }
        public object? Discount { get; set; } // Type might need adjustment
        public List<object>? Prices { get; set; } // Додано поле Prices
        public string? Currency { get; set; }
        public PromGroup? Group { get; set; }
        public PromCategory? Category { get; set; }
        public string? MainImage { get; set; }
        public List<PromImage>? Images { get; set; }
        public string? SellingType { get; set; }
        public string? Status { get; set; } 
        public string? QuantityInStock { get; set; }
        public string? MeasureUnit { get; set; }
        public bool IsVariation { get; set; }
        public string? VariationBaseId { get; set; }
        public string? VariationGroupId { get; set; }
        public DateTime? DateModified { get; set; }
        public bool InStock { get; set; }
        public List<object>? Regions { get; set; } // Додано поле Regions
        public Dictionary<string, string>? NameMultilang { get; set; }
        public Dictionary<string, string>? DescriptionMultilang { get; set; }
    }

    // Need definitions for these inner classes if used in PromProduct
    private class PromGroup { 
        public long Id { get; set; } 
        public string Name { get; set; } = string.Empty; 
        public Dictionary<string, string>? NameMultilang { get; set; } // Додано поле NameMultilang
    }
    private class PromCategory { public long Id { get; set; } public string Caption { get; set; } = string.Empty; }
    private class PromImage { 
        public long Id { get; set; } // Додано поле Id
        public string Url { get; set; } = string.Empty; 
        public string ThumbnailUrl { get; set; } = string.Empty; // Додано поле ThumbnailUrl
    }
    
    // Define other helper classes like PromUAProductListResponse and PromUACreatedProductResponse
    // based on actual API responses
    private class PromUAProductListResponse { public List<PromProduct>? Products { get; set; } }
    private class PromUACreatedProductResponse { public long? Id { get; set; } }

    public async Task<Tsintra.Domain.DTOs.PublishResultDto> PublishProductAsync(ProductDetailsDto productDetails, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to publish product '{ProductName}' to Prom.ua", productDetails.RefinedTitle);
        
        try
        {
            // Використовуємо reflection для отримання додаткових властивостей
            var productType = productDetails.GetType();
            var hasNameMultilang = productType.GetProperty("NameMultilang") != null;
            var hasDescriptionMultilang = productType.GetProperty("DescriptionMultilang") != null;
            var hasMetaTitle = productType.GetProperty("MetaTitle") != null;
            var hasMetaDescription = productType.GetProperty("MetaDescription") != null;
            var hasSeoUrl = productType.GetProperty("SeoUrl") != null;

            // Створюємо запит для API Prom.ua
            var productPayload = new Dictionary<string, object>
            {
                ["name"] = productDetails.RefinedTitle ?? "Generated Product",
                ["description"] = productDetails.Description ?? "No description",
                ["price"] = productDetails.Price ?? 0m,
                ["keywords"] = productDetails.Tags != null ? string.Join(", ", productDetails.Tags) : string.Empty,
                ["status"] = "on_display",
                ["currency"] = "UAH",
                ["presence"] = "available"
            };

            // Додаємо зображення, якщо вони є
            if (productDetails.Images?.Any() == true)
            {
                productPayload["images"] = productDetails.Images.Select(url => url).ToList();
                productPayload["main_image"] = productDetails.Images.FirstOrDefault() ?? string.Empty;
            }

            // Додаємо багатомовну підтримку, якщо вона доступна
            if (hasNameMultilang)
            {
                var nameMultilangProp = productType.GetProperty("NameMultilang");
                var nameMultilangValue = nameMultilangProp?.GetValue(productDetails) as Dictionary<string, string>;
                
                if (nameMultilangValue?.Any() == true)
                {
                    productPayload["name_multilang"] = nameMultilangValue;
                }
            }
            
            if (hasDescriptionMultilang)
            {
                var descMultilangProp = productType.GetProperty("DescriptionMultilang");
                var descMultilangValue = descMultilangProp?.GetValue(productDetails) as Dictionary<string, string>;
                
                if (descMultilangValue?.Any() == true)
                {
                    productPayload["description_multilang"] = descMultilangValue;
                }
            }

            // Додаємо SEO-оптимізацію, якщо вона доступна
            if (hasMetaTitle)
            {
                var metaTitleProp = productType.GetProperty("MetaTitle");
                var metaTitleValue = metaTitleProp?.GetValue(productDetails) as string;
                
                if (!string.IsNullOrEmpty(metaTitleValue))
                {
                    productPayload["meta_title"] = metaTitleValue;
                }
            }
            
            if (hasMetaDescription)
            {
                var metaDescProp = productType.GetProperty("MetaDescription");
                var metaDescValue = metaDescProp?.GetValue(productDetails) as string;
                
                if (!string.IsNullOrEmpty(metaDescValue))
                {
                    productPayload["meta_description"] = metaDescValue;
                }
            }
            
            if (hasSeoUrl)
            {
                var seoUrlProp = productType.GetProperty("SeoUrl");
                var seoUrlValue = seoUrlProp?.GetValue(productDetails) as string;
                
                if (!string.IsNullOrEmpty(seoUrlValue))
                {
                    productPayload["seo_url"] = seoUrlValue;
                }
            }

            // Додаємо всі атрибути, якщо вони доступні
            if (productDetails.Attributes?.Any() == true)
            {
                productPayload["attributes"] = productDetails.Attributes;
            }

            // Додавання категорії, якщо вона вказана
            if (!string.IsNullOrEmpty(productDetails.Category))
            {
                productPayload["category"] = productDetails.Category;
            }

            // Створюємо повний запит для Prom.ua API - вони очікують об'єкт з полем "product"
            var promProductRequest = new Dictionary<string, object>
            {
                ["product"] = productPayload
            };

            // Серіалізуємо запит в JSON
            var json = JsonSerializer.Serialize(promProductRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending product data to Prom.ua: {Json}", json);

            // Використовуємо правильний ендпоінт для створення товару в Prom.ua
            var response = await _httpClient.PostAsync("products/edit", content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Received response from Prom.ua: {Response}", responseContent);
                
                try
                {
                    // Парсимо відповідь від Prom.ua API
                    using var jsonDocument = JsonDocument.Parse(responseContent);
                    
                    // Шукаємо ID продукту в різних полях відповіді
                    string? productId = null;
                    
                    // Спроба знайти ID товару в різних можливих полях
                    if (jsonDocument.RootElement.TryGetProperty("id", out var idElement))
                    {
                        productId = idElement.ToString();
                    }
                    else if (jsonDocument.RootElement.TryGetProperty("product_id", out var productIdElement))
                    {
                        productId = productIdElement.ToString();
                    }
                    else if (jsonDocument.RootElement.TryGetProperty("product", out var productElement) &&
                            productElement.TryGetProperty("id", out var productInnerIdElement))
                    {
                        productId = productInnerIdElement.ToString();
                    }
                    
                    if (!string.IsNullOrEmpty(productId))
                    {
                        return new Tsintra.Domain.DTOs.PublishResultDto
                        {
                            Success = true,
                            Message = "Successfully published product to Prom.ua",
                            MarketplaceProductId = productId
                        };
                    }
                    else
                    {
                        _logger.LogWarning("Product was published but no ID was returned from Prom.ua");
                        return new Tsintra.Domain.DTOs.PublishResultDto
                        {
                            Success = true,
                            Message = "Product published, but no product ID was returned"
                        };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse Prom.ua response: {Response}", responseContent);
                    return new Tsintra.Domain.DTOs.PublishResultDto
                    {
                        Success = true, // Припускаємо успіх, оскільки статус відповіді 200
                        Message = "Product likely published, but unable to parse response"
                    };
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to publish product to Prom.ua. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                
                return new Tsintra.Domain.DTOs.PublishResultDto
                {
                    Success = false,
                    Message = $"Failed to publish to Prom.ua: {response.StatusCode} - {errorContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while publishing product to Prom.ua");
            return new Tsintra.Domain.DTOs.PublishResultDto
            {
                Success = false,
                Message = $"Exception during publishing: {ex.Message}"
            };
        }
    }

    private async Task<TResponse?> SendRequestAsync<TResponse>(HttpMethod method, string relativeUrl, object? requestData = null, CancellationToken cancellationToken = default)
        where TResponse : class
    {
        var requestUri = new Uri(_httpClient.BaseAddress!, relativeUrl);
        using var request = new HttpRequestMessage(method, requestUri);

        if (requestData != null)
        {
            var jsonPayload = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            _logger.LogDebug("Sending {Method} request to {Url} with payload: {Payload}", method, relativeUrl, jsonPayload);
        }
        else
        {
            _logger.LogDebug("Sending {Method} request to {Url}", method, relativeUrl);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Received response {StatusCode} from {Url}: {Response}", response.StatusCode, relativeUrl, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Prom.ua API request failed ({StatusCode}) to {Url}: {Response}", response.StatusCode, relativeUrl, responseContent);
                // Consider throwing a specific exception or returning null/default
                return null;
            }
            
            if (string.IsNullOrWhiteSpace(responseContent)){
                return null; // Or handle empty successful response
            }

            // Prom.ua API often wraps results in {"status": "success", "data": ...} or {"errors": ...}
            // Need robust parsing here.
            try
            {
                 // Example parsing - adjust based on actual Prom.ua responses
                 using var jsonDoc = JsonDocument.Parse(responseContent);
                 if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement)) // Adjust "data" if needed
                 {
                     return dataElement.Deserialize<TResponse>(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                 }
                 // Maybe the root itself is the object?
                 return jsonDoc.RootElement.Deserialize<TResponse>(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to deserialize Prom.ua response from {Url}. Response: {Response}", relativeUrl, responseContent);
                return null;
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP request error calling Prom.ua API at {Url}", relativeUrl);
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Prom.ua API request cancelled for {Url}", relativeUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Prom.ua API at {Url}", relativeUrl);
            return null;
        }
    }
}