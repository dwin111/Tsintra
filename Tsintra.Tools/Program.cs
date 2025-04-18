using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Diagnostics;
using System.IO;
using Tsintra.Persistence;

namespace Tsintra.Tools
{
    // Add NovaPoshtaApiRequest class
    public class NovaPoshtaApiRequest
    {
        public string? apiKey { get; set; }
        public string? modelName { get; set; }
        public string? calledMethod { get; set; }
        public object? methodProperties { get; set; }
    }

    // Add NovaPoshtaApiResponse class
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

    class Program
    {
        // Add static ApiKey and ApiUrl fields
        private static string? ApiKey;
        private static string? ApiUrl;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            
            // Перевірка на команду запуску міграцій
            if (args.Length > 0 && args[0] == "run-migrations")
            {
                Console.WriteLine("=== Запуск міграцій бази даних ===\n");
                
                string configPath = "appsettings.json";
                if (args.Length > 1)
                {
                    configPath = args[1];
                }
                
                Console.WriteLine($"Використовую конфігурацію: {configPath}");
                bool result = RunDbMigrations.RunMigrations(configPath);
                
                if (result)
                {
                    Console.WriteLine("Міграції успішно виконані");
                    return;
                }
                else
                {
                    Console.WriteLine("Помилка при виконанні міграцій");
                    return;
                }
            }
            
            Console.WriteLine("=== Перевірка API Нової Пошти з детальним логуванням ===\n");

            // Створення файлу логу
            string logFileName = $"novaposhta_api_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            using var logFile = File.CreateText(logFileName);
            
            // Метод для логування
            void Log(string message)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logMessage = $"[{timestamp}] {message}";
                Console.WriteLine(logMessage);
                logFile.WriteLine(logMessage);
                logFile.Flush();
            }
            
            Log("Запуск тестування API Нової Пошти");

            // Опрацювання аргументів командного рядка
            string cityName = "Київ";  // Місто пошуку за замовчуванням
            string streetName = "Хрещатик";  // Вулиця пошуку за замовчуванням
            
            if (args.Length >= 1)
            {
                cityName = args[0];
            }
            
            if (args.Length >= 2)
            {
                streetName = args[1];
            }
            
            Log($"Місто для пошуку: {cityName}");
            Log($"Вулиця для пошуку: {streetName}");

            // Завантаження конфігурації
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            ApiKey = configuration["NovaPoshta:ApiKey"];
            ApiUrl = configuration["NovaPoshta:ApiUrl"];

            if (string.IsNullOrEmpty(ApiKey) || string.IsNullOrEmpty(ApiUrl))
            {
                Log("Помилка: Не знайдено API ключ або URL в конфігурації.");
                return;
            }

            Log($"Використовую API URL: {ApiUrl}");
            Log($"API Key: {ApiKey.Substring(0, 5)}... (прихований для безпеки)\n");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30); // Встановлюємо таймаут 30 секунд
            
            // Додаємо хедери для відстеження
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Tsintra-Debug-Tool/1.0");
            Log("Налаштовано HTTP клієнт з User-Agent: Tsintra-Debug-Tool/1.0");

            try
            {
                // Тест 1: Пошук міст - для отримання Ref
                Log("Починаємо тест пошуку міст");
                string cityRef = await TestSearchCity(httpClient, ApiUrl, ApiKey, cityName, Log);
                
                if (!string.IsNullOrEmpty(cityRef))
                {
                    // Тест 2: Пошук вулиць у знайденому місті
                    Log("Починаємо тест пошуку вулиць");
                    await TestSearchStreets(httpClient, ApiUrl, ApiKey, cityRef, streetName, Log);
                }
                else
                {
                    Log("\nНе вдалося отримати Ref для міста, тест пошуку вулиць пропущено.");
                }
                
                // Додаємо виклик StreetSearch
                Log("\n====== ТЕСТ 3: Тестування StreetSearch з використанням окремого HTTP клієнта ======");
                StreetSearch(cityName, streetName, Log);
                
                Log("Тестування завершено");
                Log($"Файл логу збережено: {Path.GetFullPath(logFileName)}");
            }
            catch (Exception ex)
            {
                Log($"Критична помилка під час виконання тесту: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log($"Внутрішня помилка: {ex.InnerException.Message}");
                }
                Log($"Stack Trace: {ex.StackTrace}");
            }
        }

        static async Task<string> TestSearchCity(HttpClient httpClient, string apiUrl, string apiKey, string cityName, Action<string> log)
        {
            log($"\n====== ТЕСТ 1: Пошук міста '{cityName}' ======");
            string cityRef = null;

            var requestData = new
            {
                apiKey = apiKey,
                modelName = "Address",
                calledMethod = "searchSettlements",
                methodProperties = new
                {
                    CityName = cityName,
                    Limit = 10
                }
            };

            string requestJson = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
            log($"Відправляємо запит: {requestJson}");
            
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            log($"Content-Type: {content.Headers.ContentType}");
            log($"Content-Length: {content.Headers.ContentLength} bytes");
            
            var stopwatch = Stopwatch.StartNew();
            
            // Спочатку виконаємо запит через простий PostAsync і вручну обробимо відповідь
            log("Виконуємо запит через HttpClient.PostAsync...");
            var response = await httpClient.PostAsync(apiUrl, content);
            stopwatch.Stop();
            
            log($"Час виконання запиту: {stopwatch.ElapsedMilliseconds} мс");
            log($"Статус відповіді: {(int)response.StatusCode} {response.StatusCode}");
            log($"Заголовки відповіді:");
            foreach (var header in response.Headers)
            {
                log($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            
            string jsonResponse = await response.Content.ReadAsStringAsync();
            log($"Content-Length відповіді: {jsonResponse.Length} символів");
            
            if (!response.IsSuccessStatusCode)
            {
                log("Помилка при запиті до API Нової Пошти.");
                log($"Повний текст відповіді: {jsonResponse}");
                return null;
            }

            log($"Отримана відповідь: {jsonResponse}\n");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                var responseObject = JsonSerializer.Deserialize<JsonElement>(jsonResponse, options);
                
                if (responseObject.TryGetProperty("success", out var successProperty) && successProperty.GetBoolean())
                {
                    log("Успішний запит до API Нової Пошти!");
                    
                    if (responseObject.TryGetProperty("data", out var dataProperty) && 
                        dataProperty.ValueKind == JsonValueKind.Array && 
                        dataProperty.GetArrayLength() > 0)
                    {
                        var firstItem = dataProperty[0];
                        
                        // Перевіримо, чи є в першому елементі масив Addresses
                        if (firstItem.TryGetProperty("Addresses", out var addressesProperty) && 
                            addressesProperty.ValueKind == JsonValueKind.Array &&
                            addressesProperty.GetArrayLength() > 0)
                        {
                            log($"Знайдено {addressesProperty.GetArrayLength()} населених пунктів:");
                            
                            foreach (var address in addressesProperty.EnumerateArray())
                            {
                                string description = "";
                                if (address.TryGetProperty("Present", out var presentProperty))
                                    description = presentProperty.GetString();
                                    
                                log($"- {description}");
                                
                                // Зберігаємо Ref першого знайденого міста для подальших тестів
                                if (cityRef == null && address.TryGetProperty("Ref", out var refProperty))
                                {
                                    cityRef = refProperty.GetString();
                                    log($"  Ref: {cityRef} (буде використано для наступного тесту)");
                                }
                            }
                        }
                        else
                        {
                            log("У відповіді відсутня інформація про населені пункти.");
                            // Виведемо структуру відповіді для аналізу
                            log("Структура відповіді (дані):");
                            LogJsonStructure(firstItem, 1, log);
                        }
                    }
                    else
                    {
                        log("Не знайдено результатів.");
                    }
                }
                else
                {
                    log("Помилка у відповіді API:");
                    if (responseObject.TryGetProperty("errors", out var errorsProperty))
                    {
                        foreach (var error in errorsProperty.EnumerateArray())
                        {
                            log($"- {error.GetString()}");
                        }
                    }
                }
            }
            catch (JsonException jex)
            {
                log($"Помилка десеріалізації JSON: {jex.Message}");
                log($"Позиція в JSON: {jex.BytePositionInLine}");
                log("Перші 100 символів відповіді з проблемою:");
                log(jsonResponse.Substring(0, Math.Min(100, jsonResponse.Length)));
            }
            
            return cityRef;
        }
        
        static async Task TestSearchStreets(HttpClient httpClient, string apiUrl, string apiKey, string settlementRef, string streetName, Action<string> log)
        {
            log($"\n====== ТЕСТ 2: Пошук вулиць в населеному пункті для '{streetName}' ======");
            log($"Використовую Ref населеного пункту: {settlementRef}");

            var requestData = new
            {
                apiKey = apiKey,
                modelName = "Address",
                calledMethod = "searchSettlementStreets",
                methodProperties = new
                {
                    SettlementRef = settlementRef,
                    StreetName = streetName,
                    Limit = 10
                }
            };

            string requestJson = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
            log($"Відправляємо запит: {requestJson}");
            
            var stopwatch = Stopwatch.StartNew();
            var response = await httpClient.PostAsJsonAsync(apiUrl, requestData);
            stopwatch.Stop();
            
            log($"Час виконання запиту: {stopwatch.ElapsedMilliseconds} мс");
            log($"Статус відповіді: {(int)response.StatusCode} {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                log("Помилка при запиті до API Нової Пошти.");
                string errorContent = await response.Content.ReadAsStringAsync();
                log($"Відповідь: {errorContent}");
                return;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            log($"Отримана відповідь (перших 500 символів): {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}...\n");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            try 
            {
                var responseObject = JsonSerializer.Deserialize<JsonElement>(jsonResponse, options);
                
                if (responseObject.TryGetProperty("success", out var successProperty) && successProperty.GetBoolean())
                {
                    log("Успішний запит до API Нової Пошти!");
                    
                    if (responseObject.TryGetProperty("data", out var dataProperty) && 
                        dataProperty.ValueKind == JsonValueKind.Array)
                    {
                        int count = dataProperty.GetArrayLength();
                        log($"Знайдено {count} вулиць.");
                        
                        // Показуємо перші 5 вулиць
                        int showCount = Math.Min(5, count);
                        for (int i = 0; i < showCount; i++)
                        {
                            var item = dataProperty[i];
                            string description = "";
                            if (item.TryGetProperty("Description", out var descProperty))
                                description = descProperty.GetString();
                            
                            string descriptionRu = "";
                            if (item.TryGetProperty("DescriptionRu", out var descRuProperty))
                                descriptionRu = descRuProperty.GetString();
                            
                            log($"- {description} / {descriptionRu}");
                        }
                        
                        if (count > showCount)
                        {
                            log($"... та ще {count - showCount} вулиць");
                        }
                        
                        // Перевіряємо структуру поля info (це основна причина проблем з десеріалізацією)
                        if (responseObject.TryGetProperty("info", out var infoProperty))
                        {
                            log("\nCтруктура поля 'info':");
                            LogJsonStructure(infoProperty, 0, log);
                        }
                    }
                    else
                    {
                        log("Не знайдено вулиць.");
                    }
                }
                else
                {
                    log("Помилка у відповіді API:");
                    if (responseObject.TryGetProperty("errors", out var errorsProperty))
                    {
                        foreach (var error in errorsProperty.EnumerateArray())
                        {
                            log($"- {error.GetString()}");
                        }
                    }
                }
            }
            catch (JsonException jex)
            {
                log($"Помилка десеріалізації JSON: {jex.Message}");
                log($"Позиція в JSON: {jex.BytePositionInLine}");
            }
        }
        
        // Метод для рекурсивного виведення структури JSON
        static void LogJsonStructure(JsonElement element, int indentLevel, Action<string> log)
        {
            string indent = new string(' ', indentLevel * 2);
            
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    log($"{indent}{{");
                    foreach (var property in element.EnumerateObject())
                    {
                        log($"{indent}  \"{property.Name}\": ");
                        LogJsonStructure(property.Value, indentLevel + 1, log);
                    }
                    log($"{indent}}}");
                    break;
                
                case JsonValueKind.Array:
                    log($"{indent}[");
                    int arrayLength = element.GetArrayLength();
                    if (arrayLength > 0)
                    {
                        log($"{indent}  Масив з {arrayLength} елементами. Перший елемент:");
                        LogJsonStructure(element[0], indentLevel + 1, log);
                        
                        if (arrayLength > 1)
                            log($"{indent}  ... та ще {arrayLength - 1} елементів");
                    }
                    else
                    {
                        log($"{indent}  Порожній масив");
                    }
                    log($"{indent}]");
                    break;
                
                case JsonValueKind.String:
                    string value = element.GetString();
                    log($"{indent}\"{value}\"");
                    break;
                
                case JsonValueKind.Number:
                    log($"{indent}{element.GetRawText()}");
                    break;
                
                case JsonValueKind.True:
                case JsonValueKind.False:
                    log($"{indent}{element.GetBoolean()}");
                    break;
                
                case JsonValueKind.Null:
                    log($"{indent}null");
                    break;
                
                default:
                    log($"{indent}(невідомий тип JSON)");
                    break;
            }
        }

        // Add CallApi method
        static NovaPoshtaApiResponse CallApi(NovaPoshtaApiRequest request, Action<string>? log = null)
        {
            void LogMessage(string message)
            {
                if (log != null)
                    log(message);
                else
                    Console.WriteLine(message);
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent", "Tsintra-Debug-Tool/1.0");
                    
                    var jsonContent = new StringContent(
                        JsonSerializer.Serialize(request), 
                        Encoding.UTF8, 
                        "application/json");
                    
                    var stopwatch = Stopwatch.StartNew();
                    LogMessage("Виконання запиту до API...");
                    var response = client.PostAsync(ApiUrl, jsonContent).Result;
                    stopwatch.Stop();
                    
                    LogMessage($"Час виконання запиту: {stopwatch.ElapsedMilliseconds} мс");
                    LogMessage($"Статус відповіді: {(int)response.StatusCode} {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = response.Content.ReadAsStringAsync().Result;
                        LogMessage($"Отримано відповідь довжиною {content.Length} символів");
                        
                        try
                        {
                            var apiResponse = JsonSerializer.Deserialize<NovaPoshtaApiResponse>(content, 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new NovaPoshtaApiResponse();
                                
                            if (!apiResponse.success)
                            {
                                LogMessage("API повернуло помилку:");
                                if (apiResponse.errors != null)
                                {
                                    foreach (var error in apiResponse.errors)
                                    {
                                        LogMessage($"- {error}");
                                    }
                                }
                            }
                            
                            return apiResponse;
                        }
                        catch (JsonException jex)
                        {
                            LogMessage($"Помилка десеріалізації JSON: {jex.Message}");
                            return new NovaPoshtaApiResponse { success = false, errors = new[] { "Error deserializing response" } };
                        }
                    }
                    else
                    {
                        LogMessage($"Помилка API: {response.StatusCode}");
                        var errorContent = response.Content.ReadAsStringAsync().Result;
                        LogMessage($"Текст помилки: {errorContent}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Виникло виключення в CallApi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    LogMessage($"Внутрішня помилка: {ex.InnerException.Message}");
                }
                return null;
            }
        }

        static void StreetSearch(string cityName, string streetName, Action<string> log)
        {
            // First, find a city
            log($"Пошук міста: {cityName}");
            
            var citySearchRequest = new NovaPoshtaApiRequest
            {
                apiKey = ApiKey,
                modelName = "Address",
                calledMethod = "searchSettlements",
                methodProperties = new
                {
                    CityName = cityName,
                    Limit = 20
                }
            };

            var cityJson = JsonSerializer.Serialize(citySearchRequest, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            log($"Запит пошуку міста: {cityJson}");

            var cityResponse = CallApi(citySearchRequest, log);
            log($"Відповідь пошуку міста: {JsonSerializer.Serialize(cityResponse, new JsonSerializerOptions { WriteIndented = true })}");

            if (cityResponse != null && cityResponse.data != null && cityResponse.data.Length > 0)
            {
                try
                {
                    // Convert to JsonElement to safely navigate the dynamic JSON structure
                    var dataJson = JsonSerializer.Serialize(cityResponse.data[0]);
                    var firstDataElement = JsonSerializer.Deserialize<JsonElement>(dataJson);
                    
                    if (firstDataElement.TryGetProperty("Addresses", out JsonElement addresses) && 
                        addresses.ValueKind == JsonValueKind.Array && 
                        addresses.GetArrayLength() > 0)
                    {
                        var firstAddress = addresses[0];
                        string settlementRef = "";
                        
                        if (firstAddress.TryGetProperty("Ref", out JsonElement refElement))
                        {
                            settlementRef = refElement.GetString();
                            log($"Знайдено Ref населеного пункту: {settlementRef}");
                            
                            // Now search for streets
                            var streetRequest = new NovaPoshtaApiRequest
                            {
                                apiKey = ApiKey,
                                modelName = "Address",
                                calledMethod = "searchSettlementStreets",
                                methodProperties = new
                                {
                                    SettlementRef = settlementRef,
                                    StreetName = streetName,
                                    Limit = 20
                                }
                            };

                            var streetJson = JsonSerializer.Serialize(streetRequest, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            });
                            log($"Запит пошуку вулиці '{streetName}': {streetJson}");

                            var streetResponse = CallApi(streetRequest, log);
                            log($"Відповідь пошуку вулиці: {JsonSerializer.Serialize(streetResponse, new JsonSerializerOptions { WriteIndented = true })}");

                            if (streetResponse != null && streetResponse.data != null && streetResponse.data.Length > 0)
                            {
                                try
                                {
                                    // Convert to JsonElement to safely navigate the JSON structure
                                    var streetDataJson = JsonSerializer.Serialize(streetResponse.data[0]);
                                    var streetDataElement = JsonSerializer.Deserialize<JsonElement>(streetDataJson);
                                    
                                    if (streetDataElement.TryGetProperty("Addresses", out JsonElement streets) && 
                                        streets.ValueKind == JsonValueKind.Array && 
                                        streets.GetArrayLength() > 0)
                                    {
                                        log($"Знайдено {streets.GetArrayLength()} вулиць:");
                                        
                                        foreach (var street in streets.EnumerateArray())
                                        {
                                            string description = "Невідомо";
                                            if (street.TryGetProperty("SettlementStreetDescription", out JsonElement descElement))
                                            {
                                                description = descElement.GetString() ?? "Невідомо";
                                            }
                                            
                                            string descriptionRu = "";
                                            if (street.TryGetProperty("SettlementStreetDescriptionRu", out JsonElement descRuElement))
                                            {
                                                descriptionRu = descRuElement.GetString() ?? "";
                                            }
                                            
                                            string presentName = "";
                                            if (street.TryGetProperty("Present", out JsonElement presentElement))
                                            {
                                                presentName = presentElement.GetString() ?? "";
                                            }
                                            
                                            log($"Вулиця: {description} / {descriptionRu} ({presentName})");
                                        }
                                    }
                                    else
                                    {
                                        log("Структура відповіді не містить вулиць.");
                                        log("Виведення структури відповіді:");
                                        var responseJson = JsonSerializer.Serialize(streetResponse.data[0], new JsonSerializerOptions { WriteIndented = true });
                                        log(responseJson);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    log($"Помилка при обробці інформації про вулиці: {ex.Message}");
                                }
                            }
                            else
                            {
                                log("Вулиць не знайдено або помилка у відповіді.");
                            }
                        }
                        else
                        {
                            log("Не вдалося знайти Ref у першій адресі.");
                        }
                    }
                    else
                    {
                        log("Інформацію про адреси не знайдено у відповіді міста.");
                    }
                }
                catch (Exception ex)
                {
                    log($"Помилка обробки відповіді: {ex.Message}");
                }
            }
            else
            {
                log("Місто не знайдено або помилка у відповіді.");
            }
        }
    }
}
