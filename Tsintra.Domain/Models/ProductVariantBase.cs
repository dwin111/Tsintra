using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    public class ProductVariantBase
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public Dictionary<string, string> VariantAttributes { get; set; } = new();
    }
} 