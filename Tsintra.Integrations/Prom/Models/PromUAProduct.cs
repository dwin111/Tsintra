using System.Text.Json.Serialization;


namespace Tsintra.Integrations.Prom.Models
{
    public class PromUAProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("external_id")]
        public object? ExternalId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("keywords")]
        public string Keywords { get; set; } = string.Empty;

        [JsonPropertyName("presence")]
        public string Presence { get; set; } = "available";

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("minimum_order_quantity")]
        public object? MinimumOrderQuantity { get; set; }

        [JsonPropertyName("discount")]
        public object? Discount { get; set; }

        [JsonPropertyName("prices")]
        public List<object> Prices { get; set; } = new();

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "UAH";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("group")]
        public PromUAGroup Group { get; set; } = new();

        [JsonPropertyName("category")]
        public PromUACategory Category { get; set; } = new();

        [JsonPropertyName("main_image")]
        public string MainImage { get; set; } = string.Empty;

        [JsonPropertyName("images")]
        public List<PromUAImage> Images { get; set; } = new();

        [JsonPropertyName("selling_type")]
        public string SellingType { get; set; } = "retail";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "on_display";

        [JsonPropertyName("quantity_in_stock")]
        public object? QuantityInStock { get; set; }

        [JsonPropertyName("measure_unit")]
        public string MeasureUnit { get; set; } = "шт.";

        [JsonPropertyName("is_variation")]
        public bool IsVariation { get; set; }

        [JsonPropertyName("variation_base_id")]
        public object? VariationBaseId { get; set; }

        [JsonPropertyName("variation_group_id")]
        public object? VariationGroupId { get; set; }

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
}
