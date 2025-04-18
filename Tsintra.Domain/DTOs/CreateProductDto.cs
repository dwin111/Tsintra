using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.DTOs
{
    /// <summary>
    /// DTO для створення нового продукту
    /// </summary>
    public class CreateProductDto : ProductBase
    {
        // Pricing
        [Required]
        public decimal Price { get; set; }
        
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        
        // Inventory
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; } = true;
        
        // Content
        public string? Description { get; set; }
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; } = "active";
        
        // Variants
        public List<CreateProductVariantDto>? Variants { get; set; }
        
        // Properties
        public List<ProductPropertyDto>? Properties { get; set; }
    }
    
    /// <summary>
    /// DTO для створення варіанту продукту
    /// </summary>
    public class CreateProductVariantDto : ProductVariantBase
    {
        // Pricing
        [Required]
        public decimal Price { get; set; }
        
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        
        // Inventory
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; } = true;
        
        // Content
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; } = "active";
    }
    
    /// <summary>
    /// DTO для властивостей продукту
    /// </summary>
    public class ProductPropertyDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Value { get; set; } = string.Empty;
        
        public string? Unit { get; set; }
    }

    /// <summary>
    /// DTO для створення продукту з мінімально необхідною інформацією
    /// </summary>
    public class MinimalCreateProductDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Sku { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }
    }
} 