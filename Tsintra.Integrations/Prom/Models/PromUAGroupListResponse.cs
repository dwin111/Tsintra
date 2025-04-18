using System.Text.Json.Serialization;

namespace Tsintra.Integrations.Prom.Models
{
    public class PromUAGroupListResponse
    {
        [JsonPropertyName("groups")]
        public List<PromUAGroup> Groups { get; set; } = new();
    }
} 