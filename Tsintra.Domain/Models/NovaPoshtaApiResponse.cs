using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsintra.Domain.Models.NovaPost
{
    public class NovaPoshtaApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public List<T> Data { get; set; }

        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; }

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; }

        [JsonPropertyName("info")]
        [JsonConverter(typeof(FlexibleJsonElementConverter))]
        public JsonElement Info { get; set; }

        [JsonPropertyName("messageCodes")]
        public List<string> MessageCodes { get; set; }

        [JsonPropertyName("errorCodes")]
        public List<string> ErrorCodes { get; set; }

        [JsonPropertyName("warningCodes")]
        public List<string> WarningCodes { get; set; }

        [JsonPropertyName("infoCodes")]
        public List<string> InfoCodes { get; set; }
    }

    /// <summary>
    /// Конвертер, що дозволяє десеріалізувати значення різних типів (масив або об'єкт)
    /// </summary>
    public class FlexibleJsonElementConverter : JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value);
        }
    }
} 