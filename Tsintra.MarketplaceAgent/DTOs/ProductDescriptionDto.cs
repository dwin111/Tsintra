using System.ComponentModel.DataAnnotations;

namespace Tsintra.MarketplaceAgent.DTOs
{
    public class GenerateDescriptionRequest
    {
        [Required]
        public int ProductId { get; set; }
        
        public string? UserPreferences { get; set; }
    }

    public class RefineDescriptionRequest
    {
        [Required]
        public string CurrentDescription { get; set; } = string.Empty;
        
        [Required]
        public string UserFeedback { get; set; } = string.Empty;
    }

    public class ProductDescriptionResponse
    {
        public string Description { get; set; } = string.Empty;
        public string Hashtags { get; set; } = string.Empty;
        public string CallToAction { get; set; } = string.Empty;
    }
} 