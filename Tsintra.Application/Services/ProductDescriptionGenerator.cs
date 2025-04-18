using System;
using System.Text;
using System.Linq;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace Tsintra.Application.Services
{
    public class ProductDescriptionGenerator : IProductDescriptionGenerator
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _aiEndpoint;
        private readonly string _aiApiKey;

        public ProductDescriptionGenerator(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _aiEndpoint = _configuration["AI:Endpoint"] ?? throw new ArgumentNullException("AI:Endpoint is not configured");
            _aiApiKey = _configuration["AI:ApiKey"] ?? throw new ArgumentNullException("AI:ApiKey is not configured");
        }

        public string GenerateInstagramDescription(Product product)
        {
            var description = new StringBuilder();

            // Add product name with decorative emojis
            description.AppendLine($"✨ {product.Name} ✨");
            description.AppendLine();

            // Add price information
            if (product.OldPrice.HasValue && product.OldPrice > product.Price)
            {
                description.AppendLine($"💰 Ціна: {product.Price:N0} ₽ (було {product.OldPrice.Value:N0} ₽)");
            }
            else
            {
                description.AppendLine($"💰 Ціна: {product.Price:N0} ₽");
            }
            description.AppendLine();

            // Add stock information
            if (product.QuantityInStock.HasValue)
            {
                description.AppendLine($"📦 В наявності: {product.QuantityInStock} шт.");
            }
            description.AppendLine();

            // Add product description if available
            if (!string.IsNullOrWhiteSpace(product.Description))
            {
                description.AppendLine("📝 Опис:");
                description.AppendLine(product.Description);
                description.AppendLine();
            }

            // Add product properties
            if (product.Properties != null && product.Properties.Any())
            {
                description.AppendLine("🔍 Характеристики:");
                foreach (var property in product.Properties)
                {
                    description.AppendLine($"• {property.Name}: {property.Value}");
                }
                description.AppendLine();
            }

            // Add hashtags from keywords
            if (!string.IsNullOrWhiteSpace(product.Keywords))
            {
                var hashtags = product.Keywords
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(k => $"#{k.Replace(" ", "_")}");
                
                description.AppendLine(string.Join(" ", hashtags));
            }

            return description.ToString();
        }

        public async Task<string> GenerateAIDescriptionAsync(Product product, string? userPreferences = null)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Створи креативний опис товару для Instagram магазину. Опиши товар привабливо та емоційно.");
            prompt.AppendLine($"Назва товару: {product.Name}");
            prompt.AppendLine($"Ціна: {product.Price:N0} ₽");
            if (product.OldPrice.HasValue)
            {
                prompt.AppendLine($"Стара ціна: {product.OldPrice.Value:N0} ₽");
            }
            prompt.AppendLine($"Опис: {product.Description}");
            
            if (product.Properties != null)
            {
                prompt.AppendLine("Характеристики:");
                foreach (var property in product.Properties)
                {
                    prompt.AppendLine($"- {property.Name}: {property.Value}");
                }
            }

            if (!string.IsNullOrWhiteSpace(userPreferences))
            {
                prompt.AppendLine($"Додаткові побажання: {userPreferences}");
            }

            prompt.AppendLine("Створи опис у форматі:");
            prompt.AppendLine("- Привабливий заголовок з емодзі");
            prompt.AppendLine("- Основні характеристики з емодзі");
            prompt.AppendLine("- Детальний опис переваг");
            prompt.AppendLine("- Інформація про доставку");
            prompt.AppendLine("- Призив до дії");
            prompt.AppendLine("- Хештеги");

            var request = new
            {
                prompt = prompt.ToString(),
                max_tokens = 500,
                temperature = 0.7
            };

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _aiApiKey);
            var response = await _httpClient.PostAsync(_aiEndpoint, 
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"AI API request failed with status code: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var aiResponse = JsonSerializer.Deserialize<AIResponse>(responseContent);
            
            return aiResponse?.Choices?.FirstOrDefault()?.Text?.Trim() ?? 
                   throw new Exception("Failed to generate description");
        }

        public async Task<string> RefineDescriptionAsync(string currentDescription, string userFeedback)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Покращ опис товару для Instagram згідно з наступними побажаннями:");
            prompt.AppendLine($"Поточний опис: {currentDescription}");
            prompt.AppendLine($"Побажання користувача: {userFeedback}");
            prompt.AppendLine("Збережи структуру опису, але зміни його відповідно до побажань.");

            var request = new
            {
                prompt = prompt.ToString(),
                max_tokens = 500,
                temperature = 0.7
            };

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _aiApiKey);
            var response = await _httpClient.PostAsync(_aiEndpoint, 
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"AI API request failed with status code: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var aiResponse = JsonSerializer.Deserialize<AIResponse>(responseContent);
            
            return aiResponse?.Choices?.FirstOrDefault()?.Text?.Trim() ?? 
                   throw new Exception("Failed to refine description");
        }

        private class AIResponse
        {
            public List<Choice>? Choices { get; set; }
        }

        private class Choice
        {
            public string? Text { get; set; }
        }
    }
} 