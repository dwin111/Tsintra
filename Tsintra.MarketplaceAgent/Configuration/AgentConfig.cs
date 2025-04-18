namespace Tsintra.MarketplaceAgent.Configuration
{
    /// <summary>
    /// Configuration for the marketplace agent
    /// </summary>
    public class AgentConfig
    {
        /// <summary>
        /// The section name in the configuration file
        /// </summary>
        public const string SectionName = "MarketplaceAgent";
        
        /// <summary>
        /// The URL of the API endpoint
        /// </summary>
        public string? ApiUrl { get; set; }
        
        /// <summary>
        /// The API key for authentication
        /// </summary>
        public string? ApiKey { get; set; }
    }
} 