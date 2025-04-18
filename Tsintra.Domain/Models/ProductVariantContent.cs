using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    public class ProductVariantContent
    {
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; }
    }
} 