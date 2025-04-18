using System;
using System.Text.Json.Serialization;

namespace Tsintra.Domain.Models.NovaPost
{
    public class TrackingDocument
    {
        [JsonPropertyName("Number")]
        public string Number { get; set; }

        [JsonPropertyName("ActualDeliveryDate")]
        public string ActualDeliveryDate { get; set; }

        [JsonPropertyName("ScheduledDeliveryDate")]
        public string ScheduledDeliveryDate { get; set; }

        [JsonPropertyName("Status")]
        public string Status { get; set; }

        [JsonPropertyName("StatusCode")]
        public string StatusCode { get; set; }

        [JsonPropertyName("WarehouseRecipient")]
        public string WarehouseRecipient { get; set; }

        [JsonPropertyName("WarehouseSender")]
        public string WarehouseSender { get; set; }

        [JsonPropertyName("CitySender")]
        public string CitySender { get; set; }

        [JsonPropertyName("CityRecipient")]
        public string CityRecipient { get; set; }

        [JsonPropertyName("RecipientFullName")]
        public string RecipientFullName { get; set; }

        [JsonPropertyName("SenderFullName")]
        public string SenderFullName { get; set; }

        [JsonPropertyName("AnnouncedPrice")]
        public decimal AnnouncedPrice { get; set; }

        [JsonPropertyName("PaymentStatus")]
        public string PaymentStatus { get; set; }

        [JsonPropertyName("PaymentStatusDescription")]
        public string PaymentStatusDescription { get; set; }

        [JsonPropertyName("Weight")]
        public decimal Weight { get; set; }

        [JsonPropertyName("DateCreated")]
        public string DateCreated { get; set; }

        [JsonPropertyName("DocumentCost")]
        public decimal DocumentCost { get; set; }
    }
} 