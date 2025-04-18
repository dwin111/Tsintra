using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsintra.Application.Configuration;
using Tsintra.Application.Interfaces;
using Tsintra.Domain.Models.NovaPost;

namespace Tsintra.Application.Services
{
    public class NovaPoshtaService : INovaPoshtaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly ILogger<NovaPoshtaService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public NovaPoshtaService(
            IOptions<NovaPoshtaConfig> novaPoshtaOptions,
            ILogger<NovaPoshtaService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("NovaPoshtaApi");
            var novaPoshtaConfig = novaPoshtaOptions.Value;
            _apiKey = novaPoshtaConfig.ApiKey ?? 
                throw new ArgumentNullException(nameof(novaPoshtaConfig.ApiKey), "Nova Poshta API key is not configured");
            _apiUrl = novaPoshtaConfig.ApiUrl;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        /// <summary>
        /// Загальний метод для виконання запитів до API Нової Пошти
        /// </summary>
        private async Task<NovaPoshtaApiResponse<T>> ExecuteApiRequestAsync<T>(string modelName, string calledMethod, object methodProperties)
        {
            try
            {
                var request = new NovaPoshtaApiRequest
                {
                    ApiKey = _apiKey,
                    ModelName = modelName,
                    CalledMethod = calledMethod,
                    MethodProperties = methodProperties
                };

                var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
                _logger.LogDebug("Nova Poshta API Request: {RequestJson}", requestJson);

                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_apiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Nova Poshta API error: {StatusCode} {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return new NovaPoshtaApiResponse<T>
                    {
                        Success = false,
                        Errors = new List<string> { $"HTTP error: {response.StatusCode} {response.ReasonPhrase}" }
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Nova Poshta API Response: {ResponseJson}", responseJson);

                var apiResponse = JsonSerializer.Deserialize<NovaPoshtaApiResponse<T>>(responseJson, _jsonOptions);
                if (apiResponse == null)
                {
                    return new NovaPoshtaApiResponse<T>
                    {
                        Success = false,
                        Errors = new List<string> { "Failed to deserialize API response" }
                    };
                }

                return apiResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Nova Poshta API request: {Message}", ex.Message);
                return new NovaPoshtaApiResponse<T>
                {
                    Success = false,
                    Errors = new List<string> { $"API request failed: {ex.Message}" }
                };
            }
        }

        public async Task<List<City>> GetCitiesAsync(string findByString, int limit = 20)
        {
            var methodProperties = new
            {
                FindByString = findByString,
                Limit = limit
            };

            var response = await ExecuteApiRequestAsync<City>(
                "Address", "searchSettlements", methodProperties);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get cities. Errors: {Errors}", 
                    response.Errors != null ? string.Join(", ", response.Errors) : "No error details");
                return new List<City>();
            }

            return response.Data;
        }

        public async Task<City> GetCityAsync(string cityName, string regionName)
        {
            var cities = await GetCitiesAsync(cityName);

            if (cities == null || !cities.Any())
            {
                _logger.LogWarning("No cities found for name: {CityName}", cityName);
                return null;
            }

            // Пошук міста з відповідною областю, якщо вказано область
            if (!string.IsNullOrEmpty(regionName))
            {
                return cities.FirstOrDefault(c => 
                    c.Description.Contains(cityName, StringComparison.OrdinalIgnoreCase) && 
                    c.Area.Contains(regionName, StringComparison.OrdinalIgnoreCase));
            }

            // Якщо область не вказана, повертаємо перше знайдене місто
            return cities.FirstOrDefault(c => 
                c.Description.Contains(cityName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<Warehouse>> GetWarehousesAsync(string cityRef, string findByString = null)
        {
            var methodProperties = new
            {
                CityRef = cityRef,
                FindByString = findByString
            };

            var response = await ExecuteApiRequestAsync<Warehouse>(
                "AddressGeneral", "getWarehouses", methodProperties);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get warehouses. Errors: {Errors}", 
                    response.Errors != null ? string.Join(", ", response.Errors) : "No error details");
                return new List<Warehouse>();
            }

            return response.Data;
        }

        public async Task<List<Warehouse>> GetWarehousesByCityNameAsync(string cityName, string regionName = null, string findByString = null)
        {
            _logger.LogInformation("Getting warehouses for city: {CityName}, region: {RegionName}", cityName, regionName);
            
            // Спочатку знаходимо місто за назвою
            var city = await GetCityAsync(cityName, regionName);
            if (city == null)
            {
                _logger.LogWarning("City not found: {CityName}, region: {RegionName}", cityName, regionName);
                return new List<Warehouse>();
            }

            _logger.LogInformation("Found city: {CityName}, Ref: {CityRef}", city.Description, city.Ref);
            
            // Використовуємо знайдений cityRef для отримання списку відділень
            return await GetWarehousesAsync(city.Ref, findByString);
        }

        public async Task<TrackingDocument> TrackDocumentAsync(string trackingNumber)
        {
            var methodProperties = new
            {
                Documents = new[]
                {
                    new { DocumentNumber = trackingNumber }
                }
            };

            var response = await ExecuteApiRequestAsync<TrackingDocument>(
                "TrackingDocument", "getStatusDocuments", methodProperties);

            if (!response.Success || response.Data == null || !response.Data.Any())
            {
                _logger.LogWarning("Failed to track document. Errors: {Errors}", 
                    response.Errors != null ? string.Join(", ", response.Errors) : "No error details");
                return null;
            }

            return response.Data.First();
        }

        public async Task<InternetDocument> CreateInternetDocumentAsync(InternetDocumentRequest requestData)
        {
            var response = await ExecuteApiRequestAsync<InternetDocument>(
                "InternetDocument", "save", requestData);

            if (!response.Success || response.Data == null || !response.Data.Any())
            {
                _logger.LogWarning("Failed to create internet document. Errors: {Errors}", 
                    response.Errors != null ? string.Join(", ", response.Errors) : "No error details");
                return null;
            }

            return response.Data.First();
        }

        public async Task<decimal> GetDocumentPriceAsync(string citySender, string cityRecipient, string serviceType, decimal weight, decimal cost)
        {
            var methodProperties = new
            {
                CitySender = citySender,
                CityRecipient = cityRecipient,
                Weight = weight,
                ServiceType = serviceType,
                Cost = cost
            };

            var response = await ExecuteApiRequestAsync<dynamic>(
                "InternetDocument", "getDocumentPrice", methodProperties);

            if (!response.Success || response.Data == null || !response.Data.Any())
            {
                _logger.LogWarning("Failed to get document price. Errors: {Errors}", 
                    response.Errors != null ? string.Join(", ", response.Errors) : "No error details");
                return 0;
            }

            var priceData = response.Data.First();
            var priceProperty = priceData.GetProperty("Cost");
            if (priceProperty.ValueKind == JsonValueKind.Undefined)
            {
                return 0;
            }

            return priceProperty.GetDecimal();
        }

        public async Task<string> GetDocumentDeliveryDateAsync(string citySender, string cityRecipient, string serviceType, string date)
        {
            var methodProperties = new
            {
                CitySender = citySender,
                CityRecipient = cityRecipient,
                ServiceType = serviceType,
                DateTime = date
            };

            var response = await ExecuteApiRequestAsync<dynamic>(
                "InternetDocument", "getDocumentDeliveryDate", methodProperties);

            if (!response.Success || response.Data == null || !response.Data.Any())
            {
                _logger.LogWarning("Failed to get document delivery date. Errors: {Errors}", 
                    response.Errors != null ? string.Join(", ", response.Errors) : "No error details");
                return null;
            }

            var deliveryData = response.Data.First();
            var dateProperty = deliveryData.GetProperty("DeliveryDate");
            if (dateProperty.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            return dateProperty.GetProperty("date").GetString();
        }
    }
} 