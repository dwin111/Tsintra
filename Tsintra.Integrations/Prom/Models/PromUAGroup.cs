using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tsintra.Integrations.Prom.Models
{
    public class PromUAGroup
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
}
