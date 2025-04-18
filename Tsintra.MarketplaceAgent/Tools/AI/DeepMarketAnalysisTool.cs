using System.Net.Http.Json;
using System.Text.Json;
// Corrected using statements
// using ConsoleApp3.AI.Configuration; // No longer needed directly
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// using Microsoft.Playwright; // Removed Playwright dependency
using System.Text;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Models.AI;
using Tsintra.MarketplaceAgent.Models.Core; // For StringBuilder

namespace Tsintra.MarketplaceAgent.Tools.AI
{

    // Assuming MarketAnalysisInput is defined in MarketplaceAgentApp.DTOs
    // public record MarketAnalysisInput(string Title, string SceneDescription, string ImageSearchResultsJson, string Language, string TargetCurrency);
    public record GoogleSearchResultItem(string Title, string Link, string Snippet);
    public record GoogleSearchResponse(List<GoogleSearchResultItem> Items);

    // Updated to implement ITool<MarketAnalysisInput, string>
    public class DeepMarketAnalysisTool : ITool<MarketAnalysisInput, string>
    {
        private readonly ILogger<DeepMarketAnalysisTool> _logger;
        private readonly IAiChatCompletionService _aiChatService;
        private readonly GoogleSearchConfig _searchConfig;
        private readonly HttpClient _httpClient;
        // private readonly IBrowser? _browser; // Removed Playwright dependency

        // Update constructor signature - removed IBrowser
        public DeepMarketAnalysisTool(
            ILogger<DeepMarketAnalysisTool> logger,
            IAiChatCompletionService aiChatService, 
            IOptions<GoogleSearchConfig> searchConfig,
            HttpClient httpClient)
        {
             _logger = logger; 
             _aiChatService = aiChatService; 
             _searchConfig = searchConfig.Value; 
             _httpClient = httpClient; 
             // _browser = browser; // Removed assignment
            _logger.LogInformation("[{ToolName}] Initialized.", Name);
        }

        public string Name => "DeepMarketAnalysis";
        public string Description => "Performs deep market analysis using Google Search, web scraping data, and AI synthesis.";

        // Updated RunAsync signature
        public async Task<string> RunAsync(MarketAnalysisInput input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[{ToolName}] Running market analysis...", Name);
            
            if (input == null)
            {
                _logger.LogWarning("[{ToolName}] Input MarketAnalysisInput is null.", Name);
                return "{}"; 
            }

            string searchTerm = $"{input.Title} price market analysis competitors trends {input.TargetCurrency}"; // More specific search term
            _logger.LogDebug("[{ToolName}] Performing Google Search for: {SearchTerm}", Name, searchTerm);
            List<GoogleSearchResultItem> searchResults = await PerformGoogleSearchAsync(searchTerm, cancellationToken);
            string searchResultsSummary = searchResults.Any() 
                ? string.Join("\n", searchResults.Select(r => $"- Title: {r.Title}\n  Link: {r.Link}\n  Snippet: {r.Snippet}")) 
                : "No relevant Google Search results found.";
            
            _logger.LogDebug("[{ToolName}] Google Search Results Summary:\n{Summary}", Name, searchResultsSummary);

            // Placeholder for web scraping - potentially call another tool or implement here
            string scrapedDataSummary = "Web scraping data not available.";
            // if (_browser != null && searchResults.Any()) 
            // {
            //     // Implement scraping logic here or call a scraping tool
            // }

            _logger.LogInformation("[{ToolName}] Synthesizing market analysis via AI service...", Name);
            try
            {
                // --- Prompt Generation (Uses input.ScrapedWebDataJson) ---
                var systemPromptBuilder = new StringBuilder();
                systemPromptBuilder.AppendLine("You are a highly skilled market analyst. Your task is to synthesize information from various sources (product description, image search context, Google Search results, scraped web data) to provide a concise market analysis.");
                systemPromptBuilder.AppendLine("Focus on identifying target audience, potential competitors, pricing strategies, market trends, and overall market positioning.");
                systemPromptBuilder.AppendLine($"The analysis should be relevant to the product '{input.Title}' described as '{input.SceneDescription}'.");
                systemPromptBuilder.AppendLine($"The target currency for pricing context is {input.TargetCurrency}.");
                systemPromptBuilder.AppendLine($"The analysis language is {input.Language}, and we need to support both Ukrainian and Russian languages.");
                systemPromptBuilder.AppendLine("Respond ONLY with a valid JSON object containing the following fields:");
                systemPromptBuilder.AppendLine("  - \"summary\": string (A brief overall market summary)");
                systemPromptBuilder.AppendLine("  - \"targetAudience\": string (Description of the likely target audience)");
                systemPromptBuilder.AppendLine("  - \"competitorAnalysis\": string (Analysis of potential competitors and their strategies)");
                systemPromptBuilder.AppendLine("  - \"pricingInsights\": string (Insights on pricing based on search results and trends, considering the target currency)");
                systemPromptBuilder.AppendLine("  - \"marketTrends\": string (Relevant market trends)");
                systemPromptBuilder.AppendLine("  - \"averagePrice\": decimal (The average market price in the target currency)");
                systemPromptBuilder.AppendLine("  - \"recommendedPrice\": decimal (A suggested price in the target currency based on the analysis)");
                systemPromptBuilder.AppendLine("  - \"currency\": string (The target currency code, e.g., 'UAH', 'USD')");
                systemPromptBuilder.AppendLine("  - \"popularFeatures\": object (Key features/specifications commonly found in similar products as an object of key-value pairs)");
                systemPromptBuilder.AppendLine("  - \"recommendedCategory\": string (Suggested product category for marketplace)");
                systemPromptBuilder.AppendLine("  - \"recommendedMeasureUnit\": string (Suggested measure unit, e.g., 'шт.', 'кг', 'м')");
                systemPromptBuilder.AppendLine("  - \"recommendedAvailability\": string (Suggested availability status, e.g., 'в наявності', 'під замовлення')");
                systemPromptBuilder.AppendLine("  - \"minimumOrderQuantity\": integer (Suggested minimum order quantity, usually 1)");
                systemPromptBuilder.AppendLine("  - \"dimensions\": object (Suggested dimensions with fields: width, height, length, weight)");
                systemPromptBuilder.AppendLine("  - \"seoKeywords\": array (Keywords for SEO optimization)");
                
                string systemPrompt = systemPromptBuilder.ToString();

                var userInputBuilder = new StringBuilder();
                userInputBuilder.AppendLine($"Analyze the market for Product: {input.Title}");
                userInputBuilder.AppendLine($"Scene/Context: {input.SceneDescription}");
                userInputBuilder.AppendLine($"Target Currency: {input.TargetCurrency}");
                
                if (!string.IsNullOrEmpty(input.UserHints))
                {
                    userInputBuilder.AppendLine($"User Hints: {input.UserHints}");
                }
                
                userInputBuilder.AppendLine("\n--- Context from Scraped Web Data ---");
                
                if (!string.IsNullOrEmpty(input.ScrapedWebDataJson)) 
                {
                    _logger.LogInformation("[{ToolName}] Analyzing web scraping data ({Length} characters)", Name, input.ScrapedWebDataJson.Length);
                    userInputBuilder.AppendLine("IMPORTANT! Analyze this scraped web data carefully to extract product attributes, prices, and specifications:");
                    userInputBuilder.AppendLine(input.ScrapedWebDataJson);
                }
                else
                {
                    userInputBuilder.AppendLine("No web scraping data available.");
                }

                if (input.Keywords?.Any() == true)
                {
                    userInputBuilder.AppendLine("\n--- Keywords ---");
                    userInputBuilder.AppendLine(string.Join(", ", input.Keywords));
                }

                userInputBuilder.AppendLine("\n--- Context from Google Search --- ");
                userInputBuilder.AppendLine(searchResultsSummary);
                userInputBuilder.AppendLine("\n--- Context from Web Scraping --- ");
                userInputBuilder.AppendLine(scrapedDataSummary);
                string userInput = userInputBuilder.ToString();
                // --- End Prompt Generation ---

                var messages = new List<AiChatMessage> { AiChatMessage.Create(ChatMessageRole.System, systemPrompt), AiChatMessage.Create(ChatMessageRole.User, userInput) };
                var options = new AiCompletionOptions { Temperature = 0.3f, MaxTokens = 1500, ResponseFormat = ChatResponseFormatType.JsonObject }; // Increased tokens
                
                _logger.LogInformation("[{ToolName}] Sending request to AI service for market analysis synthesis...", Name);
                string? jsonResponse = await _aiChatService.GetCompletionAsync(messages, options, cancellationToken);

                if (cancellationToken.IsCancellationRequested) 
                {
                    _logger.LogInformation("[{ToolName}] Market analysis synthesis cancelled.", Name);
                     return "{\"error\": \"Operation cancelled\"}";
                }

                if (jsonResponse == null) 
                { 
                    _logger.LogError("[{ToolName}] AI service returned null response for market analysis.", Name);
                    return "{\"error\": \"Error communicating with AI API for market analysis\"}"; 
                }
                
                try 
                { 
                    using (JsonDocument.Parse(jsonResponse)) { } 
                    _logger.LogInformation("[{ToolName}] Successfully received and validated JSON response for market analysis.", Name);
                    return jsonResponse; 
                }
                catch (JsonException jex) 
                { 
                    _logger.LogError(jex, "[{ToolName}] Failed to parse AI analysis result as JSON. Response: {Response}", Name, jsonResponse);
                    return "{\"error\": \"Failed to parse AI analysis result as JSON\"}"; 
                }
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInformation("[{ToolName}] Market analysis operation cancelled.", Name);
                 return "{\"error\": \"Operation cancelled\"}";
            }
            catch (Exception ex) 
            { 
                 _logger.LogError(ex, "[{ToolName}] Internal tool error occurred during market analysis.", Name);
                 return "{\"error\": \"Internal tool error occurred during market analysis execution.\"}"; 
            }
        }

        // Updated to accept CancellationToken
        private async Task<List<GoogleSearchResultItem>> PerformGoogleSearchAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_searchConfig.ApiKey) || string.IsNullOrEmpty(_searchConfig.Cx)) 
            {
                 _logger.LogWarning("[{ToolName}] Google Search API Key or CX is not configured. Skipping search.", Name);
                 return new List<GoogleSearchResultItem>();
            }
            
            string url = $"https://www.googleapis.com/customsearch/v1?key={_searchConfig.ApiKey}&cx={_searchConfig.Cx}&q={Uri.EscapeDataString(query)}&num=5";
            _logger.LogDebug("[{ToolName}] Google Search URL: {Url}", Name, url);
            
            try 
            { 
                // Pass CancellationToken to HttpClient methods
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (response.IsSuccessStatusCode) 
                { 
                    var searchData = await response.Content.ReadFromJsonAsync<GoogleSearchResponse>(cancellationToken: cancellationToken); 
                    return searchData?.Items ?? new List<GoogleSearchResultItem>(); 
                } 
                else 
                { 
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken); 
                    _logger.LogError("[{ToolName}] Google Search API request failed. Status: {StatusCode}, Reason: {Reason}, Content: {Content}", 
                                    Name, response.StatusCode, response.ReasonPhrase, errorContent);
                    return new List<GoogleSearchResultItem>(); 
                } 
            } 
            catch (OperationCanceledException)
            {
                 _logger.LogInformation("[{ToolName}] Google Search operation cancelled.", Name);
                 return new List<GoogleSearchResultItem>(); // Return empty list on cancellation
            }
            catch (Exception ex) 
            { 
                 _logger.LogError(ex, "[{ToolName}] Exception during Google Search API call.", Name);
                 return new List<GoogleSearchResultItem>(); 
            }
        }
    }
} 