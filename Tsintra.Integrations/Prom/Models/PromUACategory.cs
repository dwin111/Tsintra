using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tsintra.Integrations.Prom.Models
{
    public class PromUACategory
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("caption")]
        public string Caption { get; set; } = string.Empty;
    }
}
