// Defines the configuration for the OpenAI Chat Service
namespace Tsintra.MarketplaceAgent.Configuration
{
    public class ChatServiceConfig
    {
        /// <summary>
        /// The OpenAI model to use (e.g., "gpt-4o", "gpt-3.5-turbo").
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// The API key for accessing the OpenAI service.
        /// </summary>
        public string? ApiKey { get; set; }
    }
} 