using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Tsintra.Domain.DTOs
{
    /// <summary>
    /// Represents the final generated product details to be potentially published.
    /// </summary>
    public class ProductDetailsDto
    {
        [JsonPropertyName("refinedTitle")]
        public string? RefinedTitle { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("price")]
        public decimal? Price { get; set; }
        
        [JsonPropertyName("images")]
        public List<string>? Images { get; set; }
        
        [JsonPropertyName("attributes")]
        public Dictionary<string, string>? Attributes { get; set; }
        
        [JsonPropertyName("category")]
        public string? Category { get; set; }
        
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        // Багатомовна підтримка - ці поля також є у базовому класі, але додаємо атрибути для підстраховки
        [JsonPropertyName("nameMultilang")]
        public Dictionary<string, string>? NameMultilang { get; set; }

        [JsonPropertyName("descriptionMultilang")]
        public Dictionary<string, string>? DescriptionMultilang { get; set; }

        // SEO оптимізація
        [JsonPropertyName("metaTitle")]
        public string? MetaTitle { get; set; }
        
        [JsonPropertyName("metaDescription")]
        public string? MetaDescription { get; set; }
        
        [JsonPropertyName("seoUrl")]
        public string? SeoUrl { get; set; }

        public static ProductDetailsDto? FromJsonElement(JsonElement refineElement, string caption)
        {
            try
            {
                var dto = new ProductDetailsDto
                {
                    RefinedTitle = refineElement.TryGetProperty("refinedTitle", out var title) ? title.GetString() : null,
                    Description = refineElement.TryGetProperty("refinedDescription", out var desc) ? desc.GetString() : null,
                    Price = refineElement.TryGetProperty("recommendedPrice", out var price) && price.ValueKind == JsonValueKind.Number ? price.GetDecimal() : null,
                    Images = refineElement.TryGetProperty("Images", out var imgs) && imgs.ValueKind == JsonValueKind.Array
                             ? imgs.Deserialize<List<string>>()
                             : new List<string>(),
                    Attributes = refineElement.TryGetProperty("Attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object
                             ? attrs.Deserialize<Dictionary<string, string>>()
                             : null,
                    Category = refineElement.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    Tags = refineElement.TryGetProperty("Keywords", out var keywords) && keywords.ValueKind == JsonValueKind.Array
                             ? keywords.Deserialize<List<string>>()
                             : null
                };
                
                // Додаємо багатомовну підтримку якщо є відповідні поля
                if (refineElement.TryGetProperty("nameMultilang", out var nameML) && nameML.ValueKind == JsonValueKind.Object) 
                {
                    dto.NameMultilang = nameML.Deserialize<Dictionary<string, string>>();
                }
                
                if (refineElement.TryGetProperty("descriptionMultilang", out var descML) && descML.ValueKind == JsonValueKind.Object) 
                {
                    dto.DescriptionMultilang = descML.Deserialize<Dictionary<string, string>>();
                }
                
                // Додаємо SEO поля якщо є відповідні
                if (refineElement.TryGetProperty("metaTitle", out var metaTitle) && metaTitle.ValueKind == JsonValueKind.String) 
                {
                    dto.MetaTitle = metaTitle.GetString();
                }
                
                if (refineElement.TryGetProperty("metaDescription", out var metaDesc) && metaDesc.ValueKind == JsonValueKind.String) 
                {
                    dto.MetaDescription = metaDesc.GetString();
                }
                
                if (refineElement.TryGetProperty("seoUrl", out var seoUrl) && seoUrl.ValueKind == JsonValueKind.String) 
                {
                    dto.SeoUrl = seoUrl.GetString();
                }
                
                return dto;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error deserializing refined content: {ex.Message}");
                return null;
            }
        }
    }
} 