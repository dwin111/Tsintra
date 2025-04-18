using System.Net.Http.Headers; // Для AuthenticationHeaderValue
using System.Net.Http.Json; // Для PostAsJsonAsync
using System.Text.Json;
using System.Text.Json.Serialization; // Added for JsonIgnoreCondition
// Corrected using statements
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.Configuration; // Added for PublishingConfig
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Domain.DTOs;

// Updated namespace
namespace Tsintra.MarketplaceAgent.Tools.Core
{

    // Updated to implement ITool<MarketplaceProductDetailsDto, PublishResultDto>
    public class PublishingTool : ITool<MarketplaceProductDetailsDto, PublishResultDto>
    {
        private readonly ILogger<PublishingTool> _logger;
        private readonly HttpClient _httpClient;
        private readonly PublishingToolConfig _config;

        public string Name => "product_publisher";
        public string Description => "Publishes a product to an e-commerce marketplace.";

        public PublishingTool(
            ILogger<PublishingTool> logger,
            HttpClient httpClient,
            IOptions<PublishingToolConfig> config)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = config.Value;
        }

        public async Task<PublishResultDto> RunAsync(MarketplaceProductDetailsDto input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[{ToolName}] Starting product publishing...", Name);
            
            if (input == null)
            {
                _logger.LogWarning("[{ToolName}] Received null input. Expected MarketplaceProductDetailsDto.", Name);
                return new PublishResultDto { Success = false, Message = "Error: Invalid data format for publishing (null input)." };
            }

            if (string.IsNullOrEmpty(_config.PromToken))
            {
                _logger.LogError("[{ToolName}] Publishing failed: PromToken is not configured.", Name);
                return new PublishResultDto { Success = false, Message = "Error: Publishing token not configured." };
            }

            if (string.IsNullOrEmpty(_config.PublishApiUrl))
            {
                _logger.LogError("[{ToolName}] Publishing failed: PublishApiUrl is not configured.", Name);
                return new PublishResultDto { Success = false, Message = "Error: Publishing API URL not configured." };
            }
            
            try
            {
                // Convert ProductDetailsDto to API request
                var requestContent = new
                {
                    product = new
                    {
                        name = input.RefinedTitle,
                        description = input.RefinedDescription,
                        price = input.RecommendedPrice,
                        keywords = input.Keywords != null ? string.Join(", ", input.Keywords) : string.Empty,
                        currency = input.Currency ?? "UAH",
                        images = input.Images?.Select(imageUrl => new { url = imageUrl }).ToList()
                    }
                };
                
                var requestJson = JsonSerializer.Serialize(requestContent);
                _logger.LogInformation("[{ToolName}] Prepared API request: {RequestJson}", Name, requestJson);
                
                var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                // Add authorization header
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.PromToken);
                
                // Send request to the publishing API
                _logger.LogInformation("[{ToolName}] Sending request to: {ApiUrl}", Name, _config.PublishApiUrl);
                var response = await _httpClient.PostAsync(_config.PublishApiUrl, httpContent, cancellationToken);
                
                // Process the response
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                bool success = response.IsSuccessStatusCode;
                
                // Try to extract response details like product ID
                string? publishedId = null;
                string message = success ? "Product successfully published!" : responseContent;
                
                if (success && !string.IsNullOrEmpty(responseContent))
                {
                    try
                    {
                        // Attempt to parse the successful response
                        var jsonDoc = JsonDocument.Parse(responseContent);
                        var root = jsonDoc.RootElement;
                        
                        if (root.TryGetProperty("id", out var idElement))
                        {
                            publishedId = idElement.GetString();
                        }
                        else if (root.TryGetProperty("product_id", out var prodIdElement))
                        {
                            publishedId = prodIdElement.GetString();
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning("[{ToolName}] Failed to parse successful response: {ErrorMessage}", 
                                          Name, ex.Message);
                    }
                }
                else if (!success)
                {
                    try 
                    { 
                        using var doc = JsonDocument.Parse(responseContent);
                        if (doc.RootElement.TryGetProperty("errors", out var errorsEl) && 
                            errorsEl.ValueKind == JsonValueKind.Array && 
                            errorsEl.GetArrayLength() > 0)
                        {
                            message = string.Join("; ", errorsEl.EnumerateArray()
                                     .Select(e => e.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : e.ToString()));
                        }
                        else if (doc.RootElement.TryGetProperty("message", out var msgEl))
                        {
                            message = msgEl.GetString() ?? responseContent;
                        }
                    }
                    catch (JsonException) { /* Keep original responseContent as message */ }
                }

                if (success)
                {
                    _logger.LogInformation("[{ToolName}] Product published successfully. Status: {StatusCode}. Published ID: {PublishedId}", 
                                         Name, response.StatusCode, publishedId ?? "N/A");
                    return new PublishResultDto { Success = true, Message = message, PublishedItemId = publishedId }; 
                }
                else
                {
                    _logger.LogError("[{ToolName}] Failed to publish product. Status: {StatusCode}, Reason: {ReasonPhrase}", 
                                   Name, response.StatusCode, response.ReasonPhrase);
                    return new PublishResultDto { Success = false, Message = $"Publishing failed ({response.StatusCode}). Reason: {message}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ToolName}] Exception during product publishing: {ErrorMessage}", Name, ex.Message);
                return new PublishResultDto { Success = false, Message = $"Publishing failed due to an error: {ex.Message}" };
            }
        }
         // Optional helper for mapping
        // private Dictionary<string, string> MapBenefitsToCharacteristics(List<string> benefits) { ... }
    }
} 