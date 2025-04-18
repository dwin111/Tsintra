// Corrected using statements
using Microsoft.Extensions.Logging; // Додамо логування
using Tsintra.MarketplaceAgent.Interfaces; // For ITool
using Tsintra.MarketplaceAgent.DTOs;

// Updated namespace
namespace Tsintra.MarketplaceAgent.Tools.Core;

// Updated to implement ITool<MarketplaceProductDetailsDto, string>
public class ValidationTool : ITool<MarketplaceProductDetailsDto, string>
{
    private readonly ILogger<ValidationTool> _logger;

    public ValidationTool(ILogger<ValidationTool> logger)
    {
        _logger = logger;
    }

    public string Name => "product_validation";
    public string Description => "Validates a product details before publishing or further analysis.";

    // Updated RunAsync signature
    public Task<string> RunAsync(MarketplaceProductDetailsDto input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ToolName}] Starting product validation...", Name);

        // Check for cancellation early
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("[{ToolName}] Validation cancelled.", Name);
            return Task.FromResult("Validation cancelled.");
        }

        if (input == null)
        {
            _logger.LogWarning("[{ToolName}] Received null input. Expected MarketplaceProductDetailsDto.", Name);
            return Task.FromResult("Error: Product data is missing.");
        }

        var errors = new List<string>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(input.RefinedTitle))
        {
            errors.Add("Product title is missing.");
        }
        if (string.IsNullOrWhiteSpace(input.RefinedDescription))
        {
            errors.Add("Product description is missing.");
        }
        if (input.Images == null || input.Images.Count == 0)
        {
            errors.Add("At least one product image is required.");
        }
        if (input.RecommendedPrice <= 0)
        {
            errors.Add("Product price must be greater than zero.");
        }
        if (input.Keywords == null || input.Keywords.Count == 0)
        {
            errors.Add("At least one keyword/tag is recommended.");
        }

        if (errors.Any())
        {
            string errorMessage = "Validation failed: " + string.Join(" ", errors);
            _logger.LogWarning("[{ToolName}] Validation failed for product '{Title}'. Errors: {Errors}", 
                             Name, input.RefinedTitle ?? "N/A", string.Join("; ", errors));
            return Task.FromResult(errorMessage);
        }

        _logger.LogInformation("[{ToolName}] Validation successful for product '{Title}'.", Name, input.RefinedTitle ?? "N/A");
        // Return "OK" or similar success indicator
        return Task.FromResult("OK");
    }
} 