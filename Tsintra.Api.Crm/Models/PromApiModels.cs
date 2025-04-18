using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Tsintra.Api.Crm.Models
{
    #region Product models

    public class PromProductListResponse
    {
        [JsonPropertyName("products")]
        public List<PromProduct> Products { get; set; } = new();

        [JsonPropertyName("group_id")]
        public long GroupId { get; set; }
    }

    public class PromProductResponse
    {
        [JsonPropertyName("product")]
        public PromProduct Product { get; set; } = new();
    }

    public class PromProductCreateRequest
    {
        [JsonPropertyName("product")]
        public PromProductData Product { get; set; } = new();
    }

    public class PromProductCreateResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    public class PromProductData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("discount")]
        public decimal? Discount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "UAH";

        [JsonPropertyName("presence")]
        public string Presence { get; set; } = "available";

        [JsonPropertyName("keywords")]
        public string Keywords { get; set; } = string.Empty;

        [JsonPropertyName("quantity_in_stock")]
        public int? QuantityInStock { get; set; }

        [JsonPropertyName("group_id")]
        public string GroupId { get; set; }
    }

    public class PromProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("external_id")]
        public object ExternalId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("keywords")]
        public string Keywords { get; set; } = string.Empty;

        [JsonPropertyName("presence")]
        public string Presence { get; set; } = "available";

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("minimum_order_quantity")]
        public object MinimumOrderQuantity { get; set; }

        [JsonPropertyName("discount")]
        public object Discount { get; set; }

        [JsonPropertyName("prices")]
        public List<object> Prices { get; set; } = new();

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "UAH";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("group")]
        public PromGroup Group { get; set; } = new();

        [JsonPropertyName("category")]
        public PromCategory Category { get; set; } = new();

        [JsonPropertyName("main_image")]
        public string MainImage { get; set; } = string.Empty;

        [JsonPropertyName("images")]
        public List<PromImage> Images { get; set; } = new();

        [JsonPropertyName("selling_type")]
        public string SellingType { get; set; } = "retail";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "on_display";

        [JsonPropertyName("quantity_in_stock")]
        public object QuantityInStock { get; set; }

        [JsonPropertyName("measure_unit")]
        public string MeasureUnit { get; set; } = "шт.";

        [JsonPropertyName("is_variation")]
        public bool IsVariation { get; set; }

        [JsonPropertyName("variation_base_id")]
        public object VariationBaseId { get; set; }

        [JsonPropertyName("variation_group_id")]
        public object VariationGroupId { get; set; }

        [JsonPropertyName("date_modified")]
        public DateTime DateModified { get; set; }

        [JsonPropertyName("in_stock")]
        public bool InStock { get; set; }

        [JsonPropertyName("regions")]
        public List<object> Regions { get; set; } = new();

        [JsonPropertyName("name_multilang")]
        public Dictionary<string, string> NameMultilang { get; set; } = new();

        [JsonPropertyName("description_multilang")]
        public Dictionary<string, string> DescriptionMultilang { get; set; } = new();
    }

    public class PromGroup
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("name_multilang")]
        public Dictionary<string, string> NameMultilang { get; set; } = new();

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("description_multilang")]
        public Dictionary<string, string> DescriptionMultilang { get; set; } = new();

        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("parent_group_id")]
        public long ParentGroupId { get; set; }
    }

    public class PromCategory
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("caption")]
        public string Caption { get; set; } = string.Empty;
    }

    public class PromImage
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    #endregion

    #region Group models

    public class PromGroupListResponse
    {
        [JsonPropertyName("groups")]
        public List<PromGroup> Groups { get; set; } = new();
    }

    #endregion

    #region Order models

    public class PromOrderListResponse
    {
        [JsonPropertyName("orders")]
        public List<PromOrder> Orders { get; set; } = new();
    }

    public class PromOrderResponse
    {
        [JsonPropertyName("order")]
        public PromOrder Order { get; set; } = new();
    }

    public class PromOrder
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("date_created")]
        public DateTime DateCreated { get; set; }

        [JsonPropertyName("client_first_name")]
        public string ClientFirstName { get; set; } = string.Empty;

        [JsonPropertyName("client_last_name")]
        public string ClientLastName { get; set; } = string.Empty;

        [JsonPropertyName("client_email")]
        public string ClientEmail { get; set; } = string.Empty;

        [JsonPropertyName("client_phone")]
        public string ClientPhone { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("delivery_address")]
        public string DeliveryAddress { get; set; } = string.Empty;

        [JsonPropertyName("delivery_type")]
        public string DeliveryType { get; set; } = string.Empty;

        [JsonPropertyName("payment_type")]
        public string PaymentType { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<PromOrderItem> Items { get; set; } = new();
    }

    public class PromOrderItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("total")]
        public decimal Total { get; set; }

        [JsonPropertyName("product_id")]
        public string ProductId { get; set; } = string.Empty;
    }

    public class PromStatusUpdateRequest
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("cancellation_reason")]
        public string CancellationReason { get; set; } = string.Empty;
    }

    #endregion
} 