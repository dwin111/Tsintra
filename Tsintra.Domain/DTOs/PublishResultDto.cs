namespace Tsintra.Domain.DTOs
{
    /// <summary>
    /// Represents the result of attempting to publish a product.
    /// </summary>
    public class PublishResultDto
    {
        /// <summary>
        /// Indicates whether the publishing attempt was successful.
        /// </summary>
        public bool Success { get; set; }
        public string? Message { get; set; } // Optional message (e.g., error details)
        public string? MarketplaceProductId { get; set; } // ID on the specific marketplace
        public string? PublishedItemId { get; set; } // Could be ID or URL
    }
} 