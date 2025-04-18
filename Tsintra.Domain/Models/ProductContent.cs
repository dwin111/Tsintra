using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    // Модель контенту продукту
    public class ProductContent
    {
        public string? Description { get; set; }
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; }
        
        // Мультимовний контент
        public Dictionary<string, string>? NameMultilang { get; set; }
        public Dictionary<string, string>? DescriptionMultilang { get; set; }
    }
} 