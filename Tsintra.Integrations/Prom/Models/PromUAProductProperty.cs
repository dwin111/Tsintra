using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tsintra.Integrations.Prom.Models
{
    public class PromUAProductProperty
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("value")]
        public string Value { get; set; } = default!;
    }
}
