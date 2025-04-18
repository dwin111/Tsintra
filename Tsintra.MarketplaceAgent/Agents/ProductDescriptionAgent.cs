using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.Services;
using Tsintra.Persistence.Repositories;

namespace Tsintra.MarketplaceAgent.Agents
{
    public class ProductDescriptionAgent : IProductDescriptionAgent
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IAgentMemoryService _agentMemory;
        private readonly IProductRepository _productRepository;
        private readonly IAgent _agent;

        public ProductDescriptionAgent(
            IConfiguration configuration,
            HttpClient httpClient,
            IAgentMemoryService agentMemory,
            IProductRepository productRepository,
            IAgent agent)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _agentMemory = agentMemory;
            _productRepository = productRepository;
            _agent = agent;
        }

        private async Task<string> GetContextAsync(Product product)
        {
            var key = $"product_{product.Id}_context";
            var context = await _agentMemory.GetMemoryPromptContextAsync(Guid.Empty, key);
            
            if (string.IsNullOrEmpty(context))
            {
                // Отримуємо історію описів товару з бази даних
                var productHistory = await _productRepository.GetProductHistoryAsync(int.Parse(product.Id.ToString()));
                
                var contextBuilder = new StringBuilder();
                contextBuilder.AppendLine("Історія описів товару:");
                if (productHistory != null && productHistory.Any())
                {
                    foreach (var history in productHistory)
                    {
                        contextBuilder.AppendLine($"- {history.Description}");
                    }
                }
                
                context = contextBuilder.ToString();
                
                // Зберігаємо в Redis
                await _agentMemory.SaveMemoryAsync(new AgentMemory
                {
                    UserId = Guid.Empty,
                    ConversationId = key,
                    Content = context,
                    CreatedAt = DateTime.UtcNow
                });
            }
            
            return context;
        }

        public async Task<string> GenerateDescriptionAsync(Product product, string? userPreferences = null)
        {
            var context = await GetContextAsync(product);
            
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

            // Додаємо історичний контекст
            if (!string.IsNullOrWhiteSpace(context))
            {
                prompt.AppendLine("\nПопередні описи товару для референсу:");
                prompt.AppendLine(context);
            }

            prompt.AppendLine("\nСтвори опис у форматі:");
            prompt.AppendLine("- Привабливий заголовок з емодзі");
            prompt.AppendLine("- Основні характеристики з емодзі");
            prompt.AppendLine("- Детальний опис переваг");
            prompt.AppendLine("- Інформація про доставку");
            prompt.AppendLine("- Призив до дії");
            prompt.AppendLine("- Хештеги");

            var newDescription = await _agent.GenerateResponseAsync(prompt.ToString());
            
            // Зберігаємо новий опис в історію
            await _productRepository.SaveProductDescriptionHistoryAsync(int.Parse(product.Id.ToString()), newDescription);
            
            return newDescription;
        }

        public async Task<string> RefineDescriptionAsync(string currentDescription, string userFeedback)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Покращ опис товару для Instagram згідно з наступними побажаннями:");
            prompt.AppendLine($"Поточний опис:\n{currentDescription}");
            prompt.AppendLine($"\nПобажання користувача:\n{userFeedback}");
            
            prompt.AppendLine("\nЗбережи структуру опису, але зміни його відповідно до побажань.");

            return await _agent.GenerateResponseAsync(prompt.ToString());
        }

        public async Task<string> GenerateHashtagsAsync(Product product)
        {
            var hashtagHistory = await _productRepository.GetProductHashtagsAsync(int.Parse(product.Id.ToString()));
            
            var prompt = new StringBuilder();
            prompt.AppendLine("Створи релевантні хештеги для товару в Instagram:");
            prompt.AppendLine($"Назва товару: {product.Name}");
            prompt.AppendLine($"Опис: {product.Description}");
            prompt.AppendLine($"Ключові слова: {product.Keywords}");
            
            if (hashtagHistory != null && hashtagHistory.Any())
            {
                prompt.AppendLine("\nПопередні хештеги для референсу:");
                foreach (var hashtag in hashtagHistory)
                {
                    prompt.AppendLine($"- {hashtag}");
                }
            }

            prompt.AppendLine("\nХештеги повинні бути:");
            prompt.AppendLine("- Релевантними до товару");
            prompt.AppendLine("- Популярними в Instagram");
            prompt.AppendLine("- Включати місцеві хештеги (Київ, Україна тощо)");
            prompt.AppendLine("- Включати тематичні хештеги");
            prompt.AppendLine("- Включати хештеги для пошуку");

            var newHashtags = await _agent.GenerateResponseAsync(prompt.ToString());
            
            // Зберігаємо нові хештеги
            await _productRepository.SaveProductHashtagsAsync(int.Parse(product.Id.ToString()), 
                newHashtags.Split('\n').Select(h => h.Trim()).Where(h => !string.IsNullOrEmpty(h)));
            
            return newHashtags;
        }

        public async Task<string> GenerateCallToActionAsync(Product product)
        {
            var ctaHistory = await _productRepository.GetProductCTAsAsync(int.Parse(product.Id.ToString()));
            
            var prompt = new StringBuilder();
            prompt.AppendLine("Створи ефективний призив до дії для товару в Instagram:");
            prompt.AppendLine($"Назва товару: {product.Name}");
            prompt.AppendLine($"Ціна: {product.Price:N0} ₽");
            
            if (ctaHistory != null && ctaHistory.Any())
            {
                prompt.AppendLine("\nПопередні призиви до дії для референсу:");
                foreach (var cta in ctaHistory)
                {
                    prompt.AppendLine($"- {cta}");
                }
            }

            prompt.AppendLine("\nПризив до дії повинен бути:");
            prompt.AppendLine("- Коротким та зрозумілим");
            prompt.AppendLine("- Мотивуючим до покупки");
            prompt.AppendLine("- Включати емодзі");
            prompt.AppendLine("- Вказувати на унікальність пропозиції");

            var newCta = await _agent.GenerateResponseAsync(prompt.ToString());
            
            // Зберігаємо новий CTA
            await _productRepository.SaveProductCTAsAsync(int.Parse(product.Id.ToString()), 
                new[] { newCta });
            
            return newCta;
        }
    }
} 