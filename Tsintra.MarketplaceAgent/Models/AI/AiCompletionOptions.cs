using Tsintra.MarketplaceAgent.Models.Core;

namespace Tsintra.MarketplaceAgent.Models.AI // Using root namespace for now
{
    /// <summary>
    /// Represents generic options for an AI chat completion request.
    /// </summary>
    public record AiCompletionOptions
    {
        /// <summary>
        /// Controls randomness: lower values make the model more deterministic.
        /// </summary>
        public float? Temperature { get; init; }

        /// <summary>
        /// The maximum number of tokens to generate in the response.
        /// </summary>
        public int? MaxTokens { get; init; }

        /// <summary>
        /// The desired format for the response (e.g., Text or JSON object).
        /// </summary>
        public ChatResponseFormatType? ResponseFormat { get; init; }

        // Add other common options as needed, e.g., TopP, StopSequences
    }
} 