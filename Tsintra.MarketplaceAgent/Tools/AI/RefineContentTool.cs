using System.Text.Json;
// Corrected using statements
using Microsoft.Extensions.Logging;
using System.Text;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Models.AI;
using Tsintra.MarketplaceAgent.Models.Core; // Added for StringBuilder

namespace Tsintra.MarketplaceAgent.Tools.AI
{
    // TODO: Move RefineContentInput to DTOs
    // public record RefineContentInput(string OriginalTitle, string MarketAnalysisJson, List<string> EditedImages, string TargetCurrency);

    // Класи для багатомовних полів
    public class MultiLanguageText
    {
        public string Uk { get; set; } = string.Empty;
        public string Ru { get; set; } = string.Empty;
    }

    public class MultiLanguageKeywords
    {
        public List<string> Uk { get; set; } = new List<string>();
        public List<string> Ru { get; set; } = new List<string>();
    }

    // Updated to implement ITool<RefineContentInput, string>
    public class RefineContentTool : ITool<RefineContentInput, string>
    {
        private readonly ILogger<RefineContentTool> _logger;
        private readonly IAiChatCompletionService _aiChatService;

        // --- Prompt Templates --- 
        private static readonly string SystemPromptTemplate = 
            "You are an expert marketing copywriter specializing in creating compelling product listings for online marketplaces.\n"
            + "Your task is to refine the provided product title and description based on the original idea, market analysis insights, and target audience information.\n"
            + "Focus on highlighting key benefits and features relevant to the target audience.\n"
            + "Generate relevant keywords for search optimization.\n"
            + "IMPORTANT: You MUST generate content in both Ukrainian and Russian languages for the marketplace listing.\n"
            + "The final output MUST be a valid JSON object containing ONLY the following fields:\n"
            + "  - \"refinedProductName\": string (Compelling and concise product title in Ukrainian)\n"
            + "  - \"refinedDescription\": string (Detailed, benefit-oriented product description in Ukrainian)\n"
            + "  - \"nameUk\": string (Product title in Ukrainian)\n"
            + "  - \"nameRu\": string (Product title in Russian)\n"
            + "  - \"descriptionUk\": string (Product description in Ukrainian)\n"
            + "  - \"descriptionRu\": string (Product description in Russian)\n"
            + "  - \"keywords\": List<string> (List of relevant search keywords in Ukrainian)\n"
            + "  - \"keywordsUk\": List<string> (List of relevant search keywords in Ukrainian)\n"
            + "  - \"keywordsRu\": List<string> (List of relevant search keywords in Russian)\n"
            + "  - \"benefits\": List<string> (List of key customer benefits in Ukrainian)\n"
            + "  - \"metaTitle\": string (SEO meta title in Ukrainian, max 60 characters)\n"
            + "  - \"metaDescription\": string (SEO meta description in Ukrainian, max 160 characters)\n"
            + "  - \"recommendedPrice\": decimal (The recommended price, based on market analysis)\n"
            + "Ensure the tone is appropriate for the target audience identified in the market analysis.\n"
            + "Use the target currency '{0}' and the determined price '{1}' ONLY as context for understanding the product's market position.\n"
            + "Make the Ukrainian and Russian text different and natural, don't just use transliteration.";

        private static readonly string UserInputTemplate = 
            "Refine the content for the following product:\n"
            + "Original Title Idea: {0}\n"
            + "\n--- Market Analysis & Audience Insights ---\n{1}"
            + "\n--- Context ---\n"
            + "Target Currency for Pricing Context: {2}\n"
            + "Price Point for Context: {3}\n"
            + "IMPORTANT: Remember to provide all content in both Ukrainian and Russian languages, making sure the translations sound natural in each language.";
            //+ "\nVisual Context (from images): [Provide image descriptions here if available]"; // Placeholder for optional image context

        public RefineContentTool(ILogger<RefineContentTool> logger, IAiChatCompletionService aiChatService)
        {
            _logger = logger; _aiChatService = aiChatService;
            _logger.LogInformation("[{ToolName}] Initialized.", Name);
        }
        public string Name => "RefineContent";
        public string Description => "Refines product title and description using AI based on market analysis and images.";

        // Updated RunAsync signature
        public async Task<string> RunAsync(RefineContentInput input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[{ToolName}] Refining content...", Name);
            
            if (input == null)
            {
                _logger.LogWarning("[{ToolName}] Input RefineContentInput is null.", Name);
                return "{}";
            }
            
            // --- Price and Currency Logic (remains largely the same) ---
            decimal recommendedPrice = 0; 
            string originalCurrency = "UAH";
            try 
            { 
                using var marketDoc = JsonDocument.Parse(input.MarketAnalysisJson);
                if (marketDoc.RootElement.TryGetProperty("recommendedPrice", out var priceEl) && priceEl.TryGetDecimal(out var price)) { recommendedPrice = price; }
                if (marketDoc.RootElement.TryGetProperty("currency", out var currencyEl) && currencyEl.ValueKind == JsonValueKind.String) { originalCurrency = currencyEl.GetString() ?? "UAH"; }
            } 
            catch (JsonException ex) { _logger.LogWarning(ex, "[{ToolName}] Could not parse recommendedPrice or currency from MarketAnalysisJson.", Name); }

            decimal priceToUse = recommendedPrice; 
            string currencyToUse = input.TargetCurrency; // TODO: Add currency conversion if needed
            // --- End Price and Currency Logic ---

            try
            {
                cancellationToken.ThrowIfCancellationRequested(); // Check before heavy processing

                // --- Format Prompts (remains the same) ---
                string systemPrompt = string.Format(SystemPromptTemplate, currencyToUse, priceToUse);
                string formattedMarketAnalysis = "";
                 try { /* ... (market analysis formatting logic) ... */ 
                    using var marketDoc = JsonDocument.Parse(input.MarketAnalysisJson);
                    var analysisBuilder = new StringBuilder();
                    if (marketDoc.RootElement.TryGetProperty("summary", out var summaryEl)) analysisBuilder.AppendLine($"Market Summary: {summaryEl.ToString()}");
                    if (marketDoc.RootElement.TryGetProperty("targetAudience", out var audienceEl)) analysisBuilder.AppendLine($"Target Audience: {audienceEl.ToString()}");
                    if (marketDoc.RootElement.TryGetProperty("competitorAnalysis", out var competitorEl)) analysisBuilder.AppendLine($"Competitor Insights: {competitorEl.ToString()}");
                    formattedMarketAnalysis = analysisBuilder.ToString();
                    if(string.IsNullOrWhiteSpace(formattedMarketAnalysis)) { formattedMarketAnalysis = input.MarketAnalysisJson; }
                 } catch (JsonException) { formattedMarketAnalysis = input.MarketAnalysisJson; }
                string userInput = string.Format(UserInputTemplate, input.OriginalTitle, formattedMarketAnalysis, currencyToUse, priceToUse);
                // --- End Format Prompts ---
                
                var messages = new List<AiChatMessage> { AiChatMessage.Create(ChatMessageRole.System, systemPrompt), AiChatMessage.Create(ChatMessageRole.User, userInput) };
                var options = new AiCompletionOptions { Temperature = 0.5f, MaxTokens = 1000, ResponseFormat = ChatResponseFormatType.JsonObject };
                
                _logger.LogInformation("[{ToolName}] Sending request to AI service for content refinement...", Name);
                string? jsonResponse = await _aiChatService.GetCompletionAsync(messages, options, cancellationToken);

                 if (cancellationToken.IsCancellationRequested)
                {
                     _logger.LogInformation("[{ToolName}] Content refinement cancelled.", Name);
                     return "{\"error\": \"Operation cancelled\"}";
                }

                if (jsonResponse == null) 
                { 
                    _logger.LogError("[{ToolName}] AI service returned null response for content refinement.", Name);
                    return "{\"error\": \"Error communicating with AI API for content refinement\"}"; 
                }
                
                try
                { // Process JSON response (add Images, Price, Currency)
                    // JSON validation and processing logic remains the same
                    if (jsonResponse.StartsWith("```json")) jsonResponse = jsonResponse.Substring(7);
                    if (jsonResponse.EndsWith("```")) jsonResponse = jsonResponse.Substring(0, jsonResponse.Length - 3);
                    jsonResponse = jsonResponse.Trim();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var resultObj = doc.RootElement.Deserialize<Dictionary<string, JsonElement>>() ?? new Dictionary<string, JsonElement>();
                    
                    // IMPORTANT: Додаємо поля, які можуть бути відсутні або потребують доповнення
                    resultObj["Images"] = JsonSerializer.SerializeToElement(input.ImageCacheKeys ?? new List<string>()); // Add image cache keys
                    
                    // Перевіряємо, чи є поле recommendedPrice, якщо немає - додаємо
                    if (!resultObj.ContainsKey("recommendedPrice") || resultObj["recommendedPrice"].ValueKind == JsonValueKind.Undefined)
                    {
                        resultObj["recommendedPrice"] = JsonSerializer.SerializeToElement(priceToUse);
                    }
                    
                    // Завжди додаємо поле валюти
                    resultObj["currency"] = JsonSerializer.SerializeToElement(currencyToUse);

                    // Формування полів для багатомовної підтримки
                    string defaultTitle = "";
                    if (resultObj.TryGetValue("refinedProductName", out var titleElem) && titleElem.ValueKind == JsonValueKind.String)
                    {
                        defaultTitle = titleElem.GetString() ?? "";
                    }
                    
                    // Створюємо об'єкт для nameMultilang
                    var nameMultilang = new MultiLanguageText();
                    
                    // Українська назва
                    if (resultObj.TryGetValue("nameUk", out var ukTitleElem) && ukTitleElem.ValueKind == JsonValueKind.String)
                    {
                        nameMultilang.Uk = ukTitleElem.GetString() ?? defaultTitle;
                    }
                    else
                    {
                        nameMultilang.Uk = defaultTitle;
                    }
                    
                    // Російська назва
                    if (resultObj.TryGetValue("nameRu", out var ruTitleElem) && ruTitleElem.ValueKind == JsonValueKind.String)
                    {
                        nameMultilang.Ru = ruTitleElem.GetString() ?? TransliterateCyrillicToRussian(defaultTitle);
                    }
                    else
                    {
                        nameMultilang.Ru = TransliterateCyrillicToRussian(defaultTitle);
                    }
                    
                    // Додаємо nameMultilang до результатів
                    resultObj["nameMultilang"] = JsonSerializer.SerializeToElement(nameMultilang);
                    
                    // Формування для поля descriptionMultilang
                    var descriptionMultilang = new MultiLanguageText();
                    
                    // Українське опис
                    if (resultObj.TryGetValue("descriptionUk", out var ukDescElem) && ukDescElem.ValueKind == JsonValueKind.String)
                    {
                        descriptionMultilang.Uk = ukDescElem.GetString() ?? "";
                    }
                    else if (resultObj.TryGetValue("refinedDescription", out var descElem) && descElem.ValueKind == JsonValueKind.String)
                    {
                        descriptionMultilang.Uk = descElem.GetString() ?? "";
                    }
                    
                    // Російське опис
                    if (resultObj.TryGetValue("descriptionRu", out var ruDescElem) && ruDescElem.ValueKind == JsonValueKind.String)
                    {
                        descriptionMultilang.Ru = ruDescElem.GetString() ?? "";
                    }
                    else
                    {
                        descriptionMultilang.Ru = TransliterateCyrillicToRussian(descriptionMultilang.Uk);
                    }
                    
                    // Додаємо descriptionMultilang до результатів
                    resultObj["descriptionMultilang"] = JsonSerializer.SerializeToElement(descriptionMultilang);
                    
                    // Формування для поля keywordsMultilang
                    var keywordsMultilang = new MultiLanguageKeywords();
                    
                    // Українські ключові слова
                    if (resultObj.TryGetValue("keywordsUk", out var ukKeywordsElem) && ukKeywordsElem.ValueKind == JsonValueKind.Array)
                    {
                        keywordsMultilang.Uk = ukKeywordsElem.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString() ?? "")
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }
                    else if (resultObj.TryGetValue("keywords", out var keywordsElem) && keywordsElem.ValueKind == JsonValueKind.Array)
                    {
                        keywordsMultilang.Uk = keywordsElem.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString() ?? "")
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }
                    
                    // Російські ключові слова
                    if (resultObj.TryGetValue("keywordsRu", out var ruKeywordsElem) && ruKeywordsElem.ValueKind == JsonValueKind.Array)
                    {
                        keywordsMultilang.Ru = ruKeywordsElem.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString() ?? "")
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }
                    else
                    {
                        // Транслітерація ключових слів на російську
                        keywordsMultilang.Ru = keywordsMultilang.Uk.Select(kw => TransliterateCyrillicToRussian(kw)).ToList();
                    }
                    
                    // Додаємо keywordsMultilang до результатів
                    resultObj["keywordsMultilang"] = JsonSerializer.SerializeToElement(keywordsMultilang);
                    
                    // Формування та додавання полів SEO
                    string metaTitle = "";
                    if (resultObj.TryGetValue("metaTitle", out var metaTitleElem) && metaTitleElem.ValueKind == JsonValueKind.String)
                    {
                        metaTitle = metaTitleElem.GetString() ?? "";
                    }
                    else 
                    {
                        // Якщо metaTitle відсутній, використовуємо українську назву (обмежено до 60 символів)
                        metaTitle = nameMultilang.Uk.Length > 60 ? nameMultilang.Uk.Substring(0, 57) + "..." : nameMultilang.Uk;
                    }
                    resultObj["metaTitle"] = JsonSerializer.SerializeToElement(metaTitle);
                    
                    string metaDescription = "";
                    if (resultObj.TryGetValue("metaDescription", out var metaDescElem) && metaDescElem.ValueKind == JsonValueKind.String)
                    {
                        metaDescription = metaDescElem.GetString() ?? "";
                    }
                    else
                    {
                        // Якщо metaDescription відсутній, використовуємо початок українського опису (обмежено до 160 символів)
                        metaDescription = descriptionMultilang.Uk.Length > 160 ? descriptionMultilang.Uk.Substring(0, 157) + "..." : descriptionMultilang.Uk;
                    }
                    resultObj["metaDescription"] = JsonSerializer.SerializeToElement(metaDescription);
                    
                    // Генерація SEO-URL на основі української назви
                    string seoUrl = GenerateSeoUrl(nameMultilang.Uk);
                    resultObj["seoUrl"] = JsonSerializer.SerializeToElement(seoUrl);
                    
                    string finalJsonResponse = JsonSerializer.Serialize(resultObj);
                    _logger.LogInformation("[{ToolName}] Content refined successfully and final JSON constructed with multilingual support.", Name);
                    return finalJsonResponse;
                }
                catch (JsonException jex) 
                { 
                    _logger.LogError(jex, "[{ToolName}] Failed to process refined content JSON. Raw Response: {RawResponse}", Name, jsonResponse); 
                    return JsonSerializer.Serialize(new { error = "Failed to process refined content JSON", rawResponse = jsonResponse }); 
                }
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInformation("[{ToolName}] Content refinement operation cancelled.", Name);
                 return "{\"error\": \"Operation cancelled\"}";
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "[{ToolName}] Internal tool error occurred during content refinement.", Name);
                return "{\"error\": \"Internal tool error occurred during content refinement execution.\"}"; 
            }
        }

        /// <summary>
        /// Виконує спрощену транслітерацію українського тексту на російський
        /// </summary>
        private string TransliterateCyrillicToRussian(string ukrainianText)
        {
            if (string.IsNullOrEmpty(ukrainianText))
                return string.Empty;

            // Основні заміни українських символів на російські
            var ukrainianToRussian = new Dictionary<string, string>
            {
                {"і", "и"}, {"є", "е"}, {"ї", "и"}, {"и", "ы"}, {"г", "г"}, {"ґ", "г"},
                {"І", "И"}, {"Є", "Е"}, {"Ї", "И"}, {"И", "Ы"}, {"Г", "Г"}, {"Ґ", "Г"}
            };

            // Заміна символів
            foreach (var kvp in ukrainianToRussian)
            {
                ukrainianText = ukrainianText.Replace(kvp.Key, kvp.Value);
            }

            return ukrainianText;
        }

        /// <summary>
        /// Генерує SEO-URL з назви товару
        /// </summary>
        private string GenerateSeoUrl(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "";

            // Словник транслітерації українських символів
            var ukrainianToLatin = new Dictionary<char, string>
            {
                {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "h"}, {'ґ', "g"}, {'д', "d"},
                {'е', "e"}, {'є', "ie"}, {'ж', "zh"}, {'з', "z"}, {'и', "y"}, {'і', "i"},
                {'ї', "i"}, {'й', "i"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"},
                {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"},
                {'ф', "f"}, {'х', "kh"}, {'ц', "ts"}, {'ч', "ch"}, {'ш', "sh"}, {'щ', "shch"},
                {'ь', ""}, {'ю', "iu"}, {'я', "ia"},
                {'А', "a"}, {'Б', "b"}, {'В', "v"}, {'Г', "h"}, {'Ґ', "g"}, {'Д', "d"},
                {'Е', "e"}, {'Є', "ie"}, {'Ж', "zh"}, {'З', "z"}, {'И', "y"}, {'І', "i"},
                {'Ї', "i"}, {'Й', "i"}, {'К', "k"}, {'Л', "l"}, {'М', "m"}, {'Н', "n"},
                {'О', "o"}, {'П', "p"}, {'Р', "r"}, {'С', "s"}, {'Т', "t"}, {'У', "u"},
                {'Ф', "f"}, {'Х', "kh"}, {'Ц', "ts"}, {'Ч', "ch"}, {'Ш', "sh"}, {'Щ', "shch"},
                {'Ь', ""}, {'Ю', "iu"}, {'Я', "ia"}
            };

            // Транслітерація
            var transliterated = new StringBuilder();
            foreach (char c in title.ToLower())
            {
                if (ukrainianToLatin.TryGetValue(c, out string? latinChar))
                {
                    transliterated.Append(latinChar);
                }
                else if (c == ' ')
                {
                    transliterated.Append('-');
                }
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')
                {
                    transliterated.Append(c);
                }
                // інші символи ігноруємо
            }

            // Конвертуємо до рядка
            string seoUrl = transliterated.ToString();
            
            // Видаляємо подвійні дефіси
            while (seoUrl.Contains("--"))
            {
                seoUrl = seoUrl.Replace("--", "-");
            }
            
            // Обрізаємо дефіси на початку і в кінці
            return seoUrl.Trim('-');
        }
    }
} 