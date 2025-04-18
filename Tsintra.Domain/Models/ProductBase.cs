using System;

namespace Tsintra.Domain.Models
{
    public class ProductBase
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? Keywords { get; set; }
        
        // Категорії та групи
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? GroupId { get; set; }
        public string? GroupName { get; set; }
        
        // Варіанти
        public bool IsVariant { get; set; }
        public Guid? VariantGroupId { get; set; }
        public Guid? ParentProductId { get; set; }
    }
} 