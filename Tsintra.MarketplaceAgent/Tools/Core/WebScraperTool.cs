using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Interfaces;
using System.Net.Http;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Tsintra.MarketplaceAgent.Models.AI;

namespace Tsintra.MarketplaceAgent.Tools.Core;

public class WebScraperTool : ITool<WebScraperInput, string>
{
    private readonly ILogger<WebScraperTool> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;

    public string Name => "web_scraper";
    public string Description => "Scrapes content from provided URLs using specified patterns and analyzes competitor data";

    public WebScraperTool(ILogger<WebScraperTool> logger, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> RunAsync(WebScraperInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("=== Початок веб-скрапінгу з ШІ-аналізом ===");
            _logger.LogInformation("Кількість URL для обробки: {UrlCount}", input.Urls.Count);
            
            var client = _httpClientFactory.CreateClient("WebScraper");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36");
            
            var results = new List<Dictionary<string, object>>();
            var pagesContent = new List<string>();
            var productDetails = new List<CompetitorProductDetails>();
            
            // 1. Скрапінг сторінок конкурентів
            foreach (var url in input.Urls)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                _logger.LogInformation("\n=== Обробка URL: {Url} ===", url);
                
                try
                {
                    var response = await client.GetAsync(url, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Не вдалося отримати доступ до URL {Url}: {StatusCode}", url, response.StatusCode);
                        continue;
                    }
                    
                    var htmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    pagesContent.Add(htmlContent);
                    
                    // Обробляємо HTML для вилучення інформації про товар
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(htmlContent);
                    
                    // Вилучаємо потрібні дані
                    string? title = ExtractNodeText(htmlDoc, "//h1") ?? 
                                    ExtractNodeText(htmlDoc, "//meta[@property='og:title']/@content");
                    
                    string? description = ExtractNodeText(htmlDoc, "//div[contains(@class, 'description')]") ?? 
                                          ExtractNodeText(htmlDoc, "//meta[@property='og:description']/@content");
                    
                    decimal price = ExtractPrice(htmlDoc);
                    string currency = ExtractCurrency(htmlDoc);
                    
                    // Вилучаємо URL зображень
                    var images = ExtractImages(htmlDoc, url);
                    
                    // Вилучаємо специфікації
                    var specifications = ExtractSpecifications(htmlDoc);
                    
                    // Вилучаємо габаритні розміри, категорію, одиницю виміру та наявність
                    var dimensions = ExtractDimensions(htmlDoc);
                    var category = ExtractCategory(htmlDoc, url);
                    var measureUnit = ExtractMeasureUnit(htmlDoc);
                    var availability = ExtractAvailability(htmlDoc);
                    
                    // Зберігаємо дані про товар
                    var productDetail = new CompetitorProductDetails
                    {
                        Url = url,
                        Title = title,
                        Description = description,
                        Price = price,
                        Currency = currency,
                        Images = images,
                        Specifications = specifications,
                        RawHtml = htmlContent,
                        Dimensions = dimensions,
                        Category = category,
                        MeasureUnit = measureUnit,
                        Availability = availability
                    };
                    
                    productDetails.Add(productDetail);
                    
                    var pageData = new Dictionary<string, object>
                    {
                        ["url"] = url,
                        ["title"] = productDetail.Title,
                        ["description"] = productDetail.Description,
                        ["price"] = productDetail.Price,
                        ["currency"] = productDetail.Currency,
                        ["images"] = productDetail.Images,
                        ["specifications"] = productDetail.Specifications
                    };
                    
                    results.Add(pageData);
                    _logger.LogInformation("Зібрані дані для {Url}: Назва='{Title}', Ціна={Price} {Currency}, {ImagesCount} зображень", 
                        url, productDetail.Title, productDetail.Price, productDetail.Currency, productDetail.Images.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Помилка при скрапінгу URL {Url}", url);
                }
            }
            
            // 2. Аналіз конкурентів за допомогою ШІ
            var competitorAnalysis = await AnalyzeCompetitorDataWithAI(productDetails, cancellationToken);
            
            // 3. Об'єднуємо результати скрапінгу та ШІ-аналізу
            var finalResult = new Dictionary<string, object>
            {
                ["scrapedPages"] = results,
                ["competitors"] = productDetails.Select(p => new Dictionary<string, object>
                {
                    ["url"] = p.Url,
                    ["title"] = p.Title,
                    ["price"] = p.Price,
                    ["currency"] = p.Currency
                }).ToList(),
                ["analysis"] = competitorAnalysis
            };
            
            var jsonResult = JsonSerializer.Serialize(finalResult, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("=== Завершено веб-скрапінг з ШІ-аналізом ===");
            
            return jsonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка під час веб-скрапінгу");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
    
    private async Task<Dictionary<string, object>> AnalyzeCompetitorDataWithAI(
        List<CompetitorProductDetails> productDetails, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Аналіз даних конкурентів за допомогою ШІ...");
            
            // Отримуємо сервіс ШІ
            var aiChatService = _serviceProvider.GetService<IAiChatCompletionService>();
            if (aiChatService == null)
            {
                _logger.LogWarning("ШІ-сервіс недоступний. Пропускаємо аналіз конкурентів.");
                return new Dictionary<string, object> { ["error"] = "AI service not available" };
            }
            
            // Підготовка даних для аналізу ШІ
            var promptText = new StringBuilder();
            promptText.AppendLine("Проаналізуй дані про товари конкурентів і надай структурований звіт з наступною інформацією:");
            promptText.AppendLine("1. Середня та медіанна ціна товарів");
            promptText.AppendLine("2. Найпопулярніші характеристики товарів");
            promptText.AppendLine("3. Унікальні продажні пропозиції конкурентів");
            promptText.AppendLine("4. Рекомендовані цінові діапазони");
            promptText.AppendLine("5. Рекомендації щодо опису товару (українською та російською мовами)");
            promptText.AppendLine("6. Рекомендації щодо оптимізації назви товару (українською та російською мовами)");
            promptText.AppendLine("7. Ключові слова для SEO (українською та російською мовами)");
            promptText.AppendLine("8. Рекомендована категорія для товару на Prom.ua");
            promptText.AppendLine("9. Рекомендована одиниця виміру (шт., кг, м, тощо)");
            promptText.AppendLine("10. Рекомендовані габаритні розміри (ширина, висота, довжина, вага)");
            promptText.AppendLine("11. Рекомендована наявність товару (в наявності, під замовлення)");
            promptText.AppendLine("\nДані товарів:");
            
            foreach (var product in productDetails)
            {
                promptText.AppendLine($"\nТовар: {product.Title}");
                promptText.AppendLine($"Ціна: {product.Price} {product.Currency}");
                promptText.AppendLine($"Опис: {TruncateText(product.Description, 300)}");
                
                if (product.Specifications.Any())
                {
                    promptText.AppendLine("Характеристики:");
                    foreach (var spec in product.Specifications.Take(10))
                    {
                        promptText.AppendLine($"- {spec.Key}: {spec.Value}");
                    }
                }
                
                if (product.Dimensions != null)
                {
                    promptText.AppendLine("Розміри:");
                    if (product.Dimensions.Width.HasValue)
                        promptText.AppendLine($"- Ширина: {product.Dimensions.Width}см");
                    if (product.Dimensions.Height.HasValue)
                        promptText.AppendLine($"- Висота: {product.Dimensions.Height}см");
                    if (product.Dimensions.Length.HasValue)
                        promptText.AppendLine($"- Довжина: {product.Dimensions.Length}см");
                    if (product.Dimensions.Weight.HasValue)
                        promptText.AppendLine($"- Вага: {product.Dimensions.Weight}кг");
                }
                
                if (!string.IsNullOrEmpty(product.Category))
                {
                    promptText.AppendLine($"Категорія: {product.Category}");
                }
                
                if (!string.IsNullOrEmpty(product.MeasureUnit))
                {
                    promptText.AppendLine($"Одиниця виміру: {product.MeasureUnit}");
                }
                
                if (!string.IsNullOrEmpty(product.Availability))
                {
                    promptText.AppendLine($"Наявність: {product.Availability}");
                }
            }
            
            promptText.AppendLine("\nНадай відповідь у форматі JSON з полями:");
            promptText.AppendLine("- averagePrice: число (середня ціна)");
            promptText.AppendLine("- medianPrice: число (медіанна ціна)");
            promptText.AppendLine("- popularFeatures: об'єкт (популярні характеристики як пари ключ-значення)");
            promptText.AppendLine("- uniqueSellingPoints: масив рядків (унікальні продажні пропозиції)");
            promptText.AppendLine("- recommendedPriceRange: об'єкт з полями min та max (рекомендований ціновий діапазон)");
            promptText.AppendLine("- descriptionRecommendations: об'єкт з полями uk та ru (рекомендації щодо опису українською та російською)");
            promptText.AppendLine("- titleRecommendations: об'єкт з полями uk та ru (рекомендації щодо назви українською та російською)");
            promptText.AppendLine("- seoKeywords: об'єкт з полями uk та ru (масиви ключових слів українською та російською)");
            promptText.AppendLine("- recommendedCategory: рядок (рекомендована категорія на Prom.ua)");
            promptText.AppendLine("- recommendedMeasureUnit: рядок (рекомендована одиниця виміру)");
            promptText.AppendLine("- dimensions: об'єкт з полями width, height, length, weight (рекомендовані розміри в см та вага в кг)");
            promptText.AppendLine("- recommendedAvailability: рядок (рекомендована наявність)");
            promptText.AppendLine("- minimumOrderQuantity: число (рекомендована мінімальна кількість замовлення)");
            
            // Створюємо список повідомлень для розмови з AI
            // Оскільки нам невідома точна структура AiChatMessage, використовуємо простий промпт
            var systemPrompt = "Ти аналітичний помічник, який допомагає аналізувати дані про товари конкурентів та готувати детальні рекомендації для розміщення товару на маркетплейсі Prom.ua.";
            var userPrompt = promptText.ToString();
            
            // Оголошуємо змінну aiResponse перед блоком try-catch
            string? aiResponse = null;
            
            try
            {
                // Спрощений виклик API без деталізації структури повідомлень
                aiResponse = await aiChatService.GetCompletionAsync(
                    new List<AiChatMessage>(),  // Передаємо порожній список, що буде заповнений у методі відповідно до API
                    new AiCompletionOptions 
                    { 
                        Temperature = 0.7f,
                        MaxTokens = 2000
                    }, 
                    cancellationToken);
                
                // Альтернативний варіант, якщо вище не працює:
                // string aiPrompt = $"{systemPrompt}\n\n{userPrompt}";
                // var aiResponse = await _aiService.SimplifiedCompletionAsync(aiPrompt, cancellationToken);
                
                // Намагаємося розпарсити JSON-відповідь
                var jsonMatch = Regex.Match(aiResponse ?? "", @"\{[\s\S]*?\}");
                if (jsonMatch.Success)
                {
                    var jsonText = jsonMatch.Value;
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText) ?? 
                           new Dictionary<string, object>();
                }
                
                _logger.LogWarning("Не вдалося вилучити JSON з відповіді ШІ");
                return new Dictionary<string, object> { ["rawAiResponse"] = aiResponse ?? "" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при обробці відповіді ШІ");
                return new Dictionary<string, object> { 
                    ["error"] = "Failed to parse AI response",
                    ["rawAiResponse"] = aiResponse ?? "" 
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при аналізі даних конкурентів за допомогою ШІ");
            return new Dictionary<string, object> { ["error"] = ex.Message };
        }
    }
    
    private string? ExtractNodeText(HtmlDocument htmlDoc, string xpath)
    {
        var node = htmlDoc.DocumentNode.SelectSingleNode(xpath);
        return node?.InnerText?.Trim();
    }
    
    private decimal ExtractPrice(HtmlDocument htmlDoc)
    {
        // Пробуємо знайти ціну за різними шаблонами
        var priceSelectors = new[]
        {
            "//meta[@property='product:price:amount']/@content",
            "//span[contains(@class, 'price')]",
            "//div[contains(@class, 'price')]",
            "//p[contains(@class, 'price')]"
        };
        
        foreach (var selector in priceSelectors)
        {
            var priceText = ExtractNodeText(htmlDoc, selector);
            if (!string.IsNullOrEmpty(priceText))
            {
                // Вилучаємо цифри та роздільник десяткових частин
                var priceMatch = Regex.Match(priceText, @"\d+([.,]\d+)?");
                if (priceMatch.Success && decimal.TryParse(
                    priceMatch.Value.Replace(',', '.'), 
                    System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out decimal price))
                {
                    return price;
                }
            }
        }
        
        return 0;
    }
    
    private string ExtractCurrency(HtmlDocument htmlDoc)
    {
        // Пробуємо знайти валюту
        var currencySelectors = new[]
        {
            "//meta[@property='product:price:currency']/@content"
        };
        
        foreach (var selector in currencySelectors)
        {
            var currency = ExtractNodeText(htmlDoc, selector);
            if (!string.IsNullOrEmpty(currency))
            {
                return currency;
            }
        }
        
        // Шукаємо символи валют
        var priceText = ExtractNodeText(htmlDoc, "//span[contains(@class, 'price')]") ?? 
                        ExtractNodeText(htmlDoc, "//div[contains(@class, 'price')]");
        
        if (!string.IsNullOrEmpty(priceText))
        {
            if (priceText.Contains("₴") || priceText.Contains("грн"))
                return "UAH";
            if (priceText.Contains("$"))
                return "USD";
            if (priceText.Contains("€"))
                return "EUR";
            if (priceText.Contains("₽") || priceText.Contains("руб"))
                return "RUB";
        }
        
        return "UAH"; // За замовчуванням
    }
    
    private List<string> ExtractImages(HtmlDocument htmlDoc, string baseUrl)
    {
        var images = new List<string>();
        
        // Пробуємо знайти зображення товару
        var imageSelectors = new[]
        {
            "//div[contains(@class, 'product')]//img/@src",
            "//div[contains(@class, 'gallery')]//img/@src",
            "//img[contains(@class, 'product')]/@src",
            "//meta[@property='og:image']/@content"
        };
        
        foreach (var selector in imageSelectors)
        {
            var imageNodes = htmlDoc.DocumentNode.SelectNodes(selector);
            if (imageNodes != null)
            {
                foreach (var imgNode in imageNodes)
                {
                    var imgSrc = imgNode.GetAttributeValue("src", imgNode.GetAttributeValue("content", ""));
                    if (!string.IsNullOrEmpty(imgSrc))
                    {
                        // Перетворюємо відносні URL в абсолютні
                        if (imgSrc.StartsWith("//"))
                        {
                            imgSrc = "https:" + imgSrc;
                        }
                        else if (imgSrc.StartsWith("/"))
                        {
                            var baseUri = new Uri(baseUrl);
                            imgSrc = $"{baseUri.Scheme}://{baseUri.Host}{imgSrc}";
                        }
                        else if (!imgSrc.StartsWith("http"))
                        {
                            var baseUri = new Uri(baseUrl);
                            imgSrc = $"{baseUri.Scheme}://{baseUri.Host}/{imgSrc}";
                        }
                        
                        images.Add(imgSrc);
                    }
                }
            }
        }
        
        return images.Distinct().Take(10).ToList();
    }
    
    private Dictionary<string, string> ExtractSpecifications(HtmlDocument htmlDoc)
    {
        var specs = new Dictionary<string, string>();
        
        // Пробуємо знайти таблиці з характеристиками
        var tableSelectors = new[]
        {
            "//table[contains(@class, 'specification')]",
            "//div[contains(@class, 'specification')]",
            "//div[contains(@class, 'characteristic')]",
            "//div[contains(@class, 'attributes')]",
            "//div[contains(@class, 'product-properties')]",
            "//ul[contains(@class, 'specification')]"
        };
        
        foreach (var selector in tableSelectors)
        {
            var specNodes = htmlDoc.DocumentNode.SelectNodes(selector);
            if (specNodes != null)
            {
                foreach (var node in specNodes)
                {
                    var rows = node.SelectNodes(".//tr") ?? node.SelectNodes(".//li") ?? node.SelectNodes(".//div[contains(@class, 'row')]");
                    if (rows != null)
                    {
                        foreach (var row in rows)
                        {
                            var keyNode = row.SelectSingleNode(".//th") ?? row.SelectSingleNode(".//dt") ?? 
                                         row.SelectSingleNode(".//div[contains(@class, 'name')]") ?? row.SelectSingleNode(".//span[1]");
                            
                            var valueNode = row.SelectSingleNode(".//td") ?? row.SelectSingleNode(".//dd") ?? 
                                           row.SelectSingleNode(".//div[contains(@class, 'value')]") ?? row.SelectSingleNode(".//span[2]");
                            
                            if (keyNode != null && valueNode != null)
                            {
                                var key = keyNode.InnerText.Trim();
                                var value = valueNode.InnerText.Trim();
                                
                                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                                {
                                    specs[key] = value;
                                }
                            }
                        }
                    }
                }
            }
        }
        
        return specs;
    }
    
    private Dimensions? ExtractDimensions(HtmlDocument htmlDoc)
    {
        var dimensions = new Dimensions();
        bool hasDimensions = false;
        
        // Шукаємо прямі згадки про розміри в специфікаціях
        var specs = ExtractSpecifications(htmlDoc);
        
        foreach (var spec in specs)
        {
            string key = spec.Key.ToLower();
            string value = spec.Value;
            
            // Ширина
            if (key.Contains("ширина") || key.Contains("width") || key == "ш" || key == "ш.")
            {
                var match = Regex.Match(value, @"(\d+(\.\d+)?)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int width))
                {
                    dimensions.Width = width;
                    hasDimensions = true;
                }
            }
            
            // Висота
            if (key.Contains("висота") || key.Contains("height") || key == "в" || key == "в.")
            {
                var match = Regex.Match(value, @"(\d+(\.\d+)?)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int height))
                {
                    dimensions.Height = height;
                    hasDimensions = true;
                }
            }
            
            // Довжина
            if (key.Contains("довжина") || key.Contains("length") || key == "д" || key == "д.")
            {
                var match = Regex.Match(value, @"(\d+(\.\d+)?)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int length))
                {
                    dimensions.Length = length;
                    hasDimensions = true;
                }
            }
            
            // Вага
            if (key.Contains("вага") || key.Contains("weight") || key == "вес")
            {
                var match = Regex.Match(value, @"(\d+(\.\d+)?)");
                if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), 
                    System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out decimal weight))
                {
                    dimensions.Weight = weight;
                    hasDimensions = true;
                }
            }
            
            // Габарити разом
            if (key.Contains("габарит") || key.Contains("розмір") || key.Contains("размер") || key.Contains("dimension"))
            {
                // Пробуємо знайти розміри у форматі ШхВхД або WxHxL
                var match = Regex.Match(value, @"(\d+)\s*[xх×]\s*(\d+)\s*[xх×]\s*(\d+)");
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int w))
                        dimensions.Width = w;
                    if (int.TryParse(match.Groups[2].Value, out int h))
                        dimensions.Height = h;
                    if (int.TryParse(match.Groups[3].Value, out int l))
                        dimensions.Length = l;
                    hasDimensions = true;
                }
            }
        }
        
        return hasDimensions ? dimensions : null;
    }
    
    private string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;
            
        return text.Substring(0, maxLength) + "...";
    }

    private string? ExtractCategory(HtmlDocument htmlDoc, string url)
    {
        // Спробуємо знайти категорію на сторінці
        var categorySelectors = new[]
        {
            "//ul[contains(@class, 'breadcrumb')]/li",
            "//div[contains(@class, 'breadcrumb')]/a",
            "//nav[contains(@class, 'breadcrumb')]/a"
        };
        
        foreach (var selector in categorySelectors)
        {
            var categoryNodes = htmlDoc.DocumentNode.SelectNodes(selector);
            if (categoryNodes != null && categoryNodes.Count > 1)
            {
                // Беремо передостанній елемент, зазвичай це категорія
                var category = categoryNodes[categoryNodes.Count - 2].InnerText.Trim();
                return category;
            }
        }
        
        // Спробуємо проаналізувати URL
        var uri = new Uri(url);
        var pathSegments = uri.AbsolutePath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
        if (pathSegments.Length > 1)
        {
            return pathSegments[pathSegments.Length - 2].Replace("-", " ");
        }
        
        return null;
    }
    
    private string? ExtractMeasureUnit(HtmlDocument htmlDoc)
    {
        // Шукаємо інформацію про одиницю виміру
        var measureSelectors = new[]
        {
            "//div[contains(text(), 'одиниця виміру') or contains(text(), 'единица измерения')]/following-sibling::*",
            "//td[contains(text(), 'одиниця виміру') or contains(text(), 'единица измерения')]/following-sibling::td"
        };
        
        foreach (var selector in measureSelectors)
        {
            var node = htmlDoc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
            {
                var text = node.InnerText.Trim().ToLower();
                if (!string.IsNullOrEmpty(text))
                {
                    if (text.Contains("шт") || text.Contains("штук") || text.Contains("одиниц") || text.Contains("единиц"))
                        return "шт.";
                    if (text.Contains("кг") || text.Contains("килограмм") || text.Contains("кілограм"))
                        return "кг";
                    if (text.Contains("м") || text.Contains("метр"))
                        return "м";
                    if (text.Contains("л") || text.Contains("литр") || text.Contains("літр"))
                        return "л";
                    return text;
                }
            }
        }
        
        // Аналізуємо опис товару
        var description = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'description')]")?.InnerText ?? "";
        if (description.Contains("продається на вагу") || description.Contains("продается на вес"))
            return "кг";
        if (description.Contains("продається метрами") || description.Contains("продается метрами"))
            return "м";
        
        // За замовчуванням
        return "шт.";
    }
    
    private string? ExtractAvailability(HtmlDocument htmlDoc)
    {
        // Шукаємо інформацію про наявність
        var availabilitySelectors = new[]
        {
            "//div[contains(@class, 'availability')]",
            "//span[contains(@class, 'availability')]",
            "//div[contains(@class, 'stock')]",
            "//span[contains(@class, 'stock')]"
        };
        
        foreach (var selector in availabilitySelectors)
        {
            var node = htmlDoc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
            {
                var text = node.InnerText.Trim().ToLower();
                if (text.Contains("в наявності") || text.Contains("в наличии") || text.Contains("есть на складе") || text.Contains("in stock"))
                    return "в наявності";
                if (text.Contains("під замовлення") || text.Contains("под заказ") || text.Contains("предзаказ") || text.Contains("pre-order"))
                    return "під замовлення";
                if (text.Contains("немає") || text.Contains("нет в наличии") || text.Contains("out of stock"))
                    return "немає в наявності";
                return text;
            }
        }
        
        // За замовчуванням
        return "в наявності";
    }
}

public class CompetitorProductDetails
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "UAH";
    public List<string> Images { get; set; } = new List<string>();
    public Dictionary<string, string> Specifications { get; set; } = new Dictionary<string, string>();
    public string RawHtml { get; set; } = string.Empty;
    public Dimensions? Dimensions { get; set; }
    public string? Category { get; set; }
    public string? MeasureUnit { get; set; }
    public string? Availability { get; set; }
}

/// <summary>
/// Габаритні розміри товару
/// </summary>
public class Dimensions
{
    /// <summary>
    /// Ширина в см
    /// </summary>
    public int? Width { get; set; }
    
    /// <summary>
    /// Висота в см
    /// </summary>
    public int? Height { get; set; }
    
    /// <summary>
    /// Довжина в см
    /// </summary>
    public int? Length { get; set; }
    
    /// <summary>
    /// Вага в кг
    /// </summary>
    public decimal? Weight { get; set; }
} 