using System;
using System.Text.Json.Serialization;

namespace Tsintra.Domain.Models.NovaPost
{
    public class InternetDocument
    {
        [JsonPropertyName("Ref")]
        public string Ref { get; set; }

        [JsonPropertyName("CostOnSite")]
        public decimal CostOnSite { get; set; }

        [JsonPropertyName("EstimatedDeliveryDate")]
        public string EstimatedDeliveryDate { get; set; }

        [JsonPropertyName("IntDocNumber")]
        public string IntDocNumber { get; set; }

        [JsonPropertyName("TypeDocument")]
        public string TypeDocument { get; set; }
    }

    public class InternetDocumentRequest
    {
        [JsonPropertyName("SenderWarehouseIndex")]
        public string SenderWarehouseIndex { get; set; }

        [JsonPropertyName("RecipientWarehouseIndex")]
        public string RecipientWarehouseIndex { get; set; }

        [JsonPropertyName("PayerType")]
        public string PayerType { get; set; }

        [JsonPropertyName("PaymentMethod")]
        public string PaymentMethod { get; set; }

        [JsonPropertyName("CargoType")]
        public string CargoType { get; set; }

        [JsonPropertyName("Weight")]
        public decimal Weight { get; set; }

        [JsonPropertyName("ServiceType")]
        public string ServiceType { get; set; }

        [JsonPropertyName("SeatsAmount")]
        public int SeatsAmount { get; set; }

        [JsonPropertyName("Description")]
        public string Description { get; set; }

        [JsonPropertyName("Cost")]
        public decimal Cost { get; set; }

        [JsonPropertyName("CitySender")]
        public string CitySender { get; set; }

        [JsonPropertyName("CityRecipient")]
        public string CityRecipient { get; set; }

        [JsonPropertyName("SenderAddress")]
        public string SenderAddress { get; set; }

        [JsonPropertyName("RecipientAddress")]
        public string RecipientAddress { get; set; }

        [JsonPropertyName("ContactSender")]
        public string ContactSender { get; set; }

        [JsonPropertyName("SendersPhone")]
        public string SendersPhone { get; set; }

        [JsonPropertyName("ContactRecipient")]
        public string ContactRecipient { get; set; }

        [JsonPropertyName("RecipientsPhone")]
        public string RecipientsPhone { get; set; }
    }
} 