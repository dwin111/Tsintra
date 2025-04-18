using System;
using System.Text.Json.Serialization;

namespace Tsintra.Domain.Models.NovaPost
{
    public class Warehouse
    {
        [JsonPropertyName("SiteKey")]
        public string SiteKey { get; set; }

        [JsonPropertyName("Description")]
        public string Description { get; set; }

        [JsonPropertyName("DescriptionRu")]
        public string DescriptionRu { get; set; }

        [JsonPropertyName("ShortAddress")]
        public string ShortAddress { get; set; }

        [JsonPropertyName("ShortAddressRu")]
        public string ShortAddressRu { get; set; }

        [JsonPropertyName("Phone")]
        public string Phone { get; set; }

        [JsonPropertyName("TypeOfWarehouse")]
        public string TypeOfWarehouse { get; set; }

        [JsonPropertyName("Ref")]
        public string Ref { get; set; }

        [JsonPropertyName("Number")]
        public string Number { get; set; }

        [JsonPropertyName("CityRef")]
        public string CityRef { get; set; }

        [JsonPropertyName("CityDescription")]
        public string CityDescription { get; set; }

        [JsonPropertyName("CityDescriptionRu")]
        public string CityDescriptionRu { get; set; }

        [JsonPropertyName("Longitude")]
        public string Longitude { get; set; }

        [JsonPropertyName("Latitude")]
        public string Latitude { get; set; }

        [JsonPropertyName("PostFinance")]
        public string PostFinance { get; set; }

        [JsonPropertyName("BicycleParking")]
        public string BicycleParking { get; set; }

        [JsonPropertyName("PaymentAccess")]
        public string PaymentAccess { get; set; }

        [JsonPropertyName("POSTerminal")]
        public string POSTerminal { get; set; }

        [JsonPropertyName("InternationalShipping")]
        public string InternationalShipping { get; set; }

        [JsonPropertyName("TotalMaxWeightAllowed")]
        public string TotalMaxWeightAllowed { get; set; }

        [JsonPropertyName("PlaceMaxWeightAllowed")]
        public string PlaceMaxWeightAllowed { get; set; }

        [JsonPropertyName("SendingLimitationsOnDimensions")]
        public Dimensions SendingLimitationsOnDimensions { get; set; }

        [JsonPropertyName("ReceivingLimitationsOnDimensions")]
        public Dimensions ReceivingLimitationsOnDimensions { get; set; }

        [JsonPropertyName("Reception")]
        public Schedule Reception { get; set; }

        [JsonPropertyName("Delivery")]
        public Schedule Delivery { get; set; }

        [JsonPropertyName("Schedule")]
        public Schedule Schedule { get; set; }

        [JsonPropertyName("DistrictCode")]
        public string DistrictCode { get; set; }

        [JsonPropertyName("WarehouseStatus")]
        public string WarehouseStatus { get; set; }

        [JsonPropertyName("WarehouseStatusDate")]
        public string WarehouseStatusDate { get; set; }

        [JsonPropertyName("CategoryOfWarehouse")]
        public string CategoryOfWarehouse { get; set; }

        [JsonPropertyName("Direct")]
        public string Direct { get; set; }
    }

    public class Dimensions
    {
        [JsonPropertyName("Width")]
        public double Width { get; set; }

        [JsonPropertyName("Height")]
        public double Height { get; set; }

        [JsonPropertyName("Length")]
        public double Length { get; set; }
    }

    public class Schedule
    {
        [JsonPropertyName("Monday")]
        public string Monday { get; set; }

        [JsonPropertyName("Tuesday")]
        public string Tuesday { get; set; }

        [JsonPropertyName("Wednesday")]
        public string Wednesday { get; set; }

        [JsonPropertyName("Thursday")]
        public string Thursday { get; set; }

        [JsonPropertyName("Friday")]
        public string Friday { get; set; }

        [JsonPropertyName("Saturday")]
        public string Saturday { get; set; }

        [JsonPropertyName("Sunday")]
        public string Sunday { get; set; }
    }
} 