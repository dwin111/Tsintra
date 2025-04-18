using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    // Метадані продукту
    public class ProductMetadata
    {
        public Dictionary<string, object>? MarketplaceSpecificData { get; set; }
        public Dictionary<string, string> MarketplaceMappings { get; set; } = new();
        
        // Метадані маркетплейсу
        public string? MarketplaceId { get; set; }
        public string? MarketplaceType { get; set; }
        
        // Часові метадані
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DateModified { get; set; }
        
        // Додаткові властивості
        public List<ProductProperty>? Properties { get; set; }
    }
} 