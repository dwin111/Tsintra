using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tsintra.Integrations.Prom.Models
{

    // Модель для відповіді при створенні продукту
    public class PromUACreatedProductResponse
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }
        // Можуть бути інші поля у відповіді
    }
}
