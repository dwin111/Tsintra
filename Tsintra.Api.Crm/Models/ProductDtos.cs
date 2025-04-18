using System;
using System.Collections.Generic;

namespace Tsintra.Api.Crm.Models
{
    // DTOs необхідні для роботи з продуктами в CRM API
    // Тимчасові копії з Domain, поки проект не збудується правильно
    
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? Keywords { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; }
        public string? Description { get; set; }
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; }
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? GroupId { get; set; }
        public string? GroupName { get; set; }
        public bool IsVariant { get; set; }
        public Guid? VariantGroupId { get; set; }
        public Guid? ParentProductId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ProductPropertyDto>? Properties { get; set; }
        public List<ProductVariantDto>? Variants { get; set; }
    }

    public class ProductPropertyDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Unit { get; set; }
    }

    public class ProductVariantDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; }
        public string? Status { get; set; }
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public Dictionary<string, string>? VariantAttributes { get; set; }
    }

    public class CreateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? Keywords { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; }
        public string? Description { get; set; }
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public string? Status { get; set; }
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? GroupId { get; set; }
        public string? GroupName { get; set; }
        public List<ProductPropertyDto>? Properties { get; set; }
        public List<ProductVariantCreateDto>? Variants { get; set; }
    }

    public class ProductVariantCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; }
        public string? Status { get; set; }
        public string? MainImage { get; set; }
        public List<string>? Images { get; set; }
        public Dictionary<string, string>? VariantAttributes { get; set; }
    }

    public class CreateSimpleProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public string? MainImage { get; set; }
        public string? CategoryName { get; set; }
        public int? QuantityInStock { get; set; }
    }
} 