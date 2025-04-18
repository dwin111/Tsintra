using System;
using System.Text.Json.Serialization;

namespace Tsintra.Domain.Models.NovaPost
{
    public class NovaPoshtaApiRequest
    {
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; }

        [JsonPropertyName("modelName")]
        public string ModelName { get; set; }

        [JsonPropertyName("calledMethod")]
        public string CalledMethod { get; set; }

        [JsonPropertyName("methodProperties")]
        public object MethodProperties { get; set; }
    }
} 