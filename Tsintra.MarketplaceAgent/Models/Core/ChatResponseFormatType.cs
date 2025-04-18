namespace Tsintra.MarketplaceAgent.Models.Core // Using root namespace for now
{
    /// <summary>
    /// Defines the desired format for the AI model's response.
    /// </summary>
    public enum ChatResponseFormatType
    {
        Text,       // Standard text response
        JsonObject  // Request response as a valid JSON object
    }
} 