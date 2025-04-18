using System;
using System.Text.Json.Serialization;

namespace Tsintra.Domain.Models.NovaPost
{
    public class City
    {
        [JsonPropertyName("Ref")]
        public string Ref { get; set; }

        [JsonPropertyName("Description")]
        public string Description { get; set; }

        [JsonPropertyName("DescriptionRu")]
        public string DescriptionRu { get; set; }

        [JsonPropertyName("DescriptionTranslit")]
        public string DescriptionTranslit { get; set; }

        [JsonPropertyName("Area")]
        public string Area { get; set; }

        [JsonPropertyName("SettlementType")]
        public string SettlementType { get; set; }

        [JsonPropertyName("IsBranch")]
        public string IsBranch { get; set; }

        [JsonPropertyName("PreventEntryNewStreetsUser")]
        public string PreventEntryNewStreetsUser { get; set; }

        [JsonPropertyName("Conglomerates")]
        public string Conglomerates { get; set; }

        [JsonPropertyName("CityID")]
        public string CityID { get; set; }

        [JsonPropertyName("SettlementTypeDescriptionRu")]
        public string SettlementTypeDescriptionRu { get; set; }

        [JsonPropertyName("SettlementTypeDescription")]
        public string SettlementTypeDescription { get; set; }
    }
} 