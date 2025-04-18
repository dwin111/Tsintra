using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Tsintra.Api.Crm.Models.Prom
{
    /// <summary>
    /// Товар у Prom.ua
    /// </summary>
    public class PromProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("external_id")]
        public string ExternalId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        [JsonPropertyName("keywords")]
        public string Keywords { get; set; }

        [JsonPropertyName("presence")]
        public string Presence { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("group")]
        public PromGroup Group { get; set; }

        [JsonPropertyName("category")]
        public PromCategory Category { get; set; }

        [JsonPropertyName("main_image")]
        public string MainImage { get; set; }

        [JsonPropertyName("images")]
        public List<PromImage> Images { get; set; }

        [JsonPropertyName("selling_type")]
        public string SellingType { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("quantity_in_stock")]
        public object QuantityInStock { get; set; }

        [JsonPropertyName("measure_unit")]
        public string MeasureUnit { get; set; }

        [JsonPropertyName("is_variation")]
        public bool IsVariation { get; set; }

        [JsonPropertyName("variation_base_id")]
        public long? VariationBaseId { get; set; }

        [JsonPropertyName("variation_group_id")]
        public long? VariationGroupId { get; set; }

        [JsonPropertyName("name_multilang")]
        public Dictionary<string, string> NameMultilang { get; set; }

        [JsonPropertyName("description_multilang")]
        public Dictionary<string, string> DescriptionMultilang { get; set; }

        [JsonPropertyName("date_modified")]
        public DateTime? DateModified { get; set; }

        [JsonPropertyName("in_stock")]
        public bool InStock { get; set; }

        [JsonPropertyName("discount")]
        public decimal? Discount { get; set; }

        [JsonPropertyName("minimum_order_quantity")]
        public int? MinimumOrderQuantity { get; set; }

        [JsonPropertyName("prices")]
        public List<PromPrice> Prices { get; set; }

        [JsonPropertyName("regions")]
        public List<string> Regions { get; set; }
    }

    /// <summary>
    /// Групи товарів в Prom.ua
    /// </summary>
    public class PromGroup
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("image")]
        public string Image { get; set; }

        [JsonPropertyName("parent_group_id")]
        public long? ParentGroupId { get; set; }

        [JsonPropertyName("name_multilang")]
        public Dictionary<string, string> NameMultilang { get; set; }

        [JsonPropertyName("description_multilang")]
        public Dictionary<string, string> DescriptionMultilang { get; set; }
    }

    /// <summary>
    /// Категорія товару Prom.ua
    /// </summary>
    public class PromCategory
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("caption")]
        public string Caption { get; set; }
    }

    /// <summary>
    /// Зображення товару Prom.ua
    /// </summary>
    public class PromImage
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    /// <summary>
    /// Ціна товару в залежності від кількості Prom.ua
    /// </summary>
    public class PromPrice
    {
        [JsonPropertyName("min_quantity")]
        public int MinQuantity { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Відповідь API Prom.ua на запит списку товарів
    /// </summary>
    public class PromProductListResponse
    {
        [JsonPropertyName("products")]
        public List<PromProduct> Products { get; set; }
    }

    /// <summary>
    /// Запит на створення/оновлення товару Prom.ua
    /// </summary>
    public class PromProductCreateRequest
    {
        [JsonPropertyName("product")]
        public PromProduct Product { get; set; }
    }

    /// <summary>
    /// Відповідь API Prom.ua на запит груп товарів
    /// </summary>
    public class PromGroupListResponse
    {
        [JsonPropertyName("groups")]
        public List<PromGroup> Groups { get; set; }
    }
} 