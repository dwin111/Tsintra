using Microsoft.AspNetCore.Mvc;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tsintra.Application.Interfaces;

namespace Tsintra.Api.Controllers
{
    // NovaPoshtaApiRequest клас для тестування
    public class NovaPoshtaApiRequest
    {
        public string? apiKey { get; set; }
        public string? modelName { get; set; }
        public string? calledMethod { get; set; }
        public object? methodProperties { get; set; }
    }

    // NovaPoshtaApiResponse клас для тестування
    public class NovaPoshtaApiResponse
    {
        public bool success { get; set; }
        public dynamic[]? data { get; set; }
        public dynamic? info { get; set; }
        public string[]? errors { get; set; }
        public string[]? warnings { get; set; }
        public object? messageCodes { get; set; }
        public object? errorCodes { get; set; }
        public object? warningCodes { get; set; }
        public object? infoCodes { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILLMService _llmService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TestController> _logger;
        private readonly string _novaPoshtaApiKey;
        private readonly string _novaPoshtaApiUrl;

        public TestController(ILLMService llmService, IConfiguration configuration, ILogger<TestController> logger)
        {
            _llmService = llmService;
            _configuration = configuration;
            _logger = logger;
            _novaPoshtaApiKey = _configuration["NovaPoshta:ApiKey"];
            _novaPoshtaApiUrl = _configuration["NovaPoshta:ApiUrl"];
        }

        [HttpPost("describe")]
        [RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Describe([FromForm] string prompt, [FromForm] List<IFormFile> files, [FromForm] List<string> urls)
        {
            if (string.IsNullOrWhiteSpace(prompt) && (files == null || !files.Any()) && (urls == null || !urls.Any()))
            {
                return BadRequest("Будь ласка, надайте опис або хоча б одне зображення.");
            }
            try
            {
                var description = await _llmService.DescribeImagesAsync(prompt, files, urls);
                return Ok(description);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Помилка опису зображень: {ex.Message}");
            }
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] ImageOptions options)
        {
            try
            {
                var imageUrl = await _llmService.GenerateImageAsync(options);
                return Ok(imageUrl);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Image Generation Error: {ex}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Сталася помилка під час генерації зображення: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { Status = "API працює", Timestamp = DateTime.Now });
        }

        /// <summary>
        /// Тестовий ендпоінт для перевірки API Нової Пошти
        /// </summary>
        [HttpGet("novaposhta/city/{cityName}")]
        public async Task<IActionResult> TestNovaPoshtaCity(string cityName)
        {
            try
            {
                var result = await TestSearchCity(cityName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при тестуванні API Нової Пошти");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Тестовий ендпоінт для пошуку вулиць
        /// </summary>
        [HttpGet("novaposhta/streets")]
        public async Task<IActionResult> TestNovaPoshtaStreets(
            [FromQuery] string cityName = "Київ",
            [FromQuery] string streetName = "Хрещатик")
        {
            try
            {
                var cityRef = await GetCityRef(cityName);
                if (string.IsNullOrEmpty(cityRef))
                {
                    return NotFound(new { Error = $"Місто {cityName} не знайдено" });
                }

                var streets = await SearchStreets(cityRef, streetName);
                return Ok(streets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при пошуку вулиць");
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Тестовий метод пошуку міста
        /// </summary>
        private async Task<object> TestSearchCity(string cityName)
        {
            _logger.LogInformation($"Пошук міста: {cityName}");

            var requestData = new NovaPoshtaApiRequest
            {
                apiKey = _novaPoshtaApiKey,
                modelName = "Address",
                calledMethod = "searchSettlements",
                methodProperties = new
                {
                    CityName = cityName,
                    Limit = 10
                }
            };

            string requestJson = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogDebug($"Запит: {requestJson}");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Tsintra-API/1.0");

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var stopwatch = Stopwatch.StartNew();

            var response = await httpClient.PostAsync(_novaPoshtaApiUrl, content);
            stopwatch.Stop();

            _logger.LogInformation($"Час виконання запиту: {stopwatch.ElapsedMilliseconds} мс");
            _logger.LogInformation($"Статус відповіді: {(int)response.StatusCode} {response.StatusCode}");

            string jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Помилка при запиті до API Нової Пошти: {jsonResponse}");
                throw new Exception("Помилка при запиті до API Нової Пошти");
            }

            _logger.LogDebug($"Відповідь: {jsonResponse}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var responseObject = JsonSerializer.Deserialize<JsonElement>(jsonResponse, options);
            
            if (responseObject.TryGetProperty("success", out var successProperty) && successProperty.GetBoolean())
            {
                if (responseObject.TryGetProperty("data", out var dataProperty) && dataProperty.ValueKind == JsonValueKind.Array)
                {
                    // Формуємо спрощену відповідь
                    var settlements = new List<object>();
                    foreach (var item in dataProperty.EnumerateArray())
                    {
                        if (item.TryGetProperty("Addresses", out var addressesProperty) && 
                            addressesProperty.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var address in addressesProperty.EnumerateArray())
                            {
                                // Додаємо потрібні поля у відповідь
                                var presentName = address.TryGetProperty("Present", out var presentProperty) 
                                    ? presentProperty.GetString() : null;
                                var mainDescriptionName = address.TryGetProperty("MainDescription", out var descProperty) 
                                    ? descProperty.GetString() : null;
                                var area = address.TryGetProperty("Area", out var areaProperty) 
                                    ? areaProperty.GetString() : null;
                                var settlementRef = address.TryGetProperty("DeliveryCity", out var deliveryCityProperty) 
                                    ? deliveryCityProperty.GetString() : null;

                                if (!string.IsNullOrEmpty(presentName) && !string.IsNullOrEmpty(settlementRef))
                                {
                                    settlements.Add(new 
                                    { 
                                        Name = presentName,
                                        Description = mainDescriptionName,
                                        Area = area,
                                        Ref = settlementRef
                                    });
                                }
                            }
                        }
                    }
                    
                    return new { Success = true, Cities = settlements };
                }
            }
            
            return new { Success = false, Error = "Не вдалося отримати дані про міста" };
        }

        /// <summary>
        /// Отримує Ref міста для подальшого пошуку
        /// </summary>
        private async Task<string> GetCityRef(string cityName)
        {
            var result = await TestSearchCity(cityName);
            
            if (result is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.TryGetProperty("Success", out var successProperty) && 
                    successProperty.GetBoolean() &&
                    jsonElement.TryGetProperty("Cities", out var citiesProperty))
                {
                    foreach (var city in citiesProperty.EnumerateArray())
                    {
                        if (city.TryGetProperty("Ref", out var refProperty))
                        {
                            return refProperty.GetString();
                        }
                    }
                }
            }
            else if (result is object objResult)
            {
                // Конвертуємо в тип, який можна серіалізувати
                var json = JsonSerializer.Serialize(objResult);
                var parsedResult = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (parsedResult.TryGetProperty("Success", out var successProperty) && 
                    successProperty.GetBoolean() &&
                    parsedResult.TryGetProperty("Cities", out var citiesProperty) &&
                    citiesProperty.ValueKind == JsonValueKind.Array)
                {
                    var cityArray = citiesProperty.EnumerateArray();
                    if (cityArray.Any())
                    {
                        var firstCity = cityArray.First();
                        if (firstCity.TryGetProperty("Ref", out var refProperty))
                        {
                            return refProperty.GetString();
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Пошук вулиць за Ref міста
        /// </summary>
        private async Task<object> SearchStreets(string cityRef, string streetName)
        {
            _logger.LogInformation($"Пошук вулиць у місті з Ref: {cityRef}, назва вулиці: {streetName}");

            var requestData = new NovaPoshtaApiRequest
            {
                apiKey = _novaPoshtaApiKey,
                modelName = "Address",
                calledMethod = "searchSettlementStreets",
                methodProperties = new
                {
                    SettlementRef = cityRef,
                    StreetName = streetName,
                    Limit = 20
                }
            };

            string requestJson = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogDebug($"Запит: {requestJson}");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Tsintra-API/1.0");

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(_novaPoshtaApiUrl, content);

            string jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Помилка при запиті до API Нової Пошти: {jsonResponse}");
                throw new Exception("Помилка при запиті до API Нової Пошти");
            }

            _logger.LogDebug($"Відповідь: {jsonResponse}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var responseObject = JsonSerializer.Deserialize<NovaPoshtaApiResponse>(jsonResponse, options);
            
            if (responseObject.success && responseObject.data != null)
            {
                var streets = new List<object>();
                foreach (var item in responseObject.data)
                {
                    streets.Add(new 
                    { 
                        Ref = item.Ref,
                        Present = item.Present,
                        Location = item.Location,
                        StreetsType = item.StreetsType,
                        StreetsTypeDescription = item.StreetsTypeDescription
                    });
                }
                
                return new { Success = true, Streets = streets };
            }
            
            return new { Success = false, Error = "Не вдалося отримати дані про вулиці", Errors = responseObject.errors };
        }
    }
}