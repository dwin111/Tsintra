namespace Tsintra.MarketplaceAgent.Configuration
{
    /// <summary>
    /// Configuration for the publishing tool
    /// </summary>
    public class PublishingToolConfig
    {
        /// <summary>
        /// The section name in the configuration file
        /// </summary>
        public const string SectionName = "PublishingTool";
        
        /// <summary>
        /// The API key or token for authentication with the marketplace API (e.g., Prom.ua)
        /// </summary>
        public string? PromToken { get; set; }
        
        /// <summary>
        /// The URL of the API endpoint for publishing products
        /// </summary>
        public string? PublishApiUrl { get; set; }
    }
} 