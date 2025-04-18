namespace Tsintra.Domain.Models
{
    public class ProductVariantPricing
    {
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
    }
} 