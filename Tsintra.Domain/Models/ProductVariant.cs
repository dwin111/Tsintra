using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    public class ProductVariant : ProductVariantBase
    {
        // Pricing
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        
        // Inventory
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; }
        
        // Content
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; }
        
        // Navigation
        public Product Product { get; set; } = null!;
        
        // Marketplace mappings
        public string? MarketplaceId { get; set; }
        public string? MarketplaceType { get; set; }
        public Dictionary<string, object>? MarketplaceSpecificData { get; set; }
        
        // Metadata
        public DateTime CreatedAt { get; set; }
        public DateTime? DateModified { get; set; }
    }
} 