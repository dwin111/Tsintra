using System;
using System.Collections.Generic;
using Tsintra.Domain.Interfaces;

namespace Tsintra.Domain.Models
{
    public class Product : ProductBase, IMarketplaceProduct
    {
        // Базові властивості
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

        // Властивості варіантів
        public bool IsVariant { get; set; }
        public Guid? VariantGroupId { get; set; }
        public Guid? ParentProductId { get; set; }
        
        // Pricing
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string? Currency { get; set; }
        
        // Inventory
        public int? QuantityInStock { get; set; }
        public bool InStock { get; set; }
        
        // Content
        public ProductContent? Content { get; set; } = new ProductContent();
        
        // Metadata
        public ProductMetadata? Metadata { get; set; } = new ProductMetadata();
        
        // Variants
        public List<ProductVariant>? ProductVariants { get; set; }
        
        // IMarketplaceProduct імплементація властивостей, яких немає безпосередньо в класі
        public string? Description 
        { 
            get => Content?.Description; 
            set { if (Content != null) Content.Description = value; } 
        }
        
        public string? MainImage 
        { 
            get => Content?.MainImage; 
            set { if (Content != null) Content.MainImage = value; } 
        }
        
        public List<string>? Images 
        { 
            get => Content?.Images; 
            set { if (Content != null) Content.Images = value; } 
        }
        
        public string? Status 
        { 
            get => Content?.Status; 
            set { if (Content != null) Content.Status = value; } 
        }
        
        public DateTime? DateModified 
        { 
            get => Metadata?.DateModified; 
            set { if (Metadata != null) Metadata.DateModified = value; } 
        }
        
        public Dictionary<string, string>? NameMultilang 
        { 
            get => Content?.NameMultilang; 
            set { if (Content != null) Content.NameMultilang = value; } 
        }
        
        public Dictionary<string, string>? DescriptionMultilang 
        { 
            get => Content?.DescriptionMultilang; 
            set { if (Content != null) Content.DescriptionMultilang = value; } 
        }
        
        // Metadata properties
        public DateTime CreatedAt 
        {
            get => Metadata?.CreatedAt ?? DateTime.UtcNow;
            set { if (Metadata != null) Metadata.CreatedAt = value; }
        }
        
        public DateTime UpdatedAt 
        {
            get => Metadata?.UpdatedAt ?? DateTime.UtcNow;
            set { if (Metadata != null) Metadata.UpdatedAt = value; }
        }
        
        public string? MarketplaceId 
        {
            get => Metadata?.MarketplaceId;
            set { if (Metadata != null) Metadata.MarketplaceId = value; }
        }
        
        public string? MarketplaceType 
        {
            get => Metadata?.MarketplaceType;
            set { if (Metadata != null) Metadata.MarketplaceType = value; }
        }
        
        public Dictionary<string, object>? MarketplaceSpecificData 
        {
            get => Metadata?.MarketplaceSpecificData;
            set { if (Metadata != null) Metadata.MarketplaceSpecificData = value; }
        }
        
        public Dictionary<string, string> MarketplaceMappings 
        {
            get => Metadata?.MarketplaceMappings ?? new Dictionary<string, string>();
            set { if (Metadata != null) Metadata.MarketplaceMappings = value; }
        }
        
        public List<ProductProperty>? Properties 
        {
            get => Metadata?.Properties;
            set { if (Metadata != null) Metadata.Properties = value; }
        }
    }

    public class ProductProperty
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Unit { get; set; }
    }
} 