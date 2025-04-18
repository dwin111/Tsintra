using System;
using System.Collections.Generic;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.DTOs
{
    /// <summary>
    /// DTO для повернення продукту в API відповідях
    /// </summary>
    public class ProductDto : ProductBase
    {
        // Pricing
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        
        // Inventory
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; }
        
        // Content
        public string? Description { get; set; }
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; }
        
        // Variants
        public List<ProductVariantDto>? Variants { get; set; }
        
        // Properties
        public List<ProductPropertyDto>? Properties { get; set; }
        
        // Metadata
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    /// <summary>
    /// DTO для варіанту продукту
    /// </summary>
    public class ProductVariantDto : ProductVariantBase
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
    }
} 