namespace Tsintra.MarketplaceAgent.Configuration;

/// <summary>
/// Configuration for the PublishingTool.
/// </summary>
public class PublishingConfig
{
    /// <summary>
    /// API Token for the target marketplace (e.g., Prom.ua).
    /// </summary>
    public string? PromToken { get; set; }

    /// <summary>
    /// The API endpoint URL for creating/publishing products.
    /// </summary>
    public string? PublishApiUrl { get; set; } = "https://my.prom.ua/api/v1/products/create"; // Default example for Prom.ua
} 