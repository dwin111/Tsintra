using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Application.Services
{
    public class OpenAIServices : ILLMServices
    {
        private readonly ILLMClient _clientService;
        private readonly IAgentMemoryService _memoryService;
        private readonly ILogger<OpenAIServices> _logger;

        public OpenAIServices(
            ILLMClient clientService, 
            IAgentMemoryService memoryService,
            ILogger<OpenAIServices> logger)
        {
            _clientService = clientService;
            _memoryService = memoryService;
            _logger = logger;
        }

        public async Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null)
        {
            return await _clientService.GenerateTextAsync(prompt);
        }

        public async Task<string> DescribeImagesAsync(string prompt, List<IFormFile> files, List<string> urls)
        {
            if ((files == null || !files.Any()) && (urls == null || !urls.Any()))
            {
                throw new ArgumentException("Не знайдено жодного дійсного зображення.");
            }

            if (files?.FirstOrDefault() != null)
            {
                return await _clientService.DescribeImageAsync(files.First(), prompt);
            }
            else
            {
                throw new NotImplementedException("Опис зображення за URL ще не реалізовано");
            }
        }

        public async Task<byte[]> GenerateImageAsync(ImageOptions options)
        {
            if (options == null || string.IsNullOrWhiteSpace(options.Prompt))
            {
                throw new ArgumentException("Тіло запиту відсутнє або 'prompt' порожній.");
            }
            
            return await _clientService.GenerateImageAsync(options);
        }

        public async Task<List<string>> ConvertImageSourcesAsync(List<IFormFile> images, List<string> imageUrls)
        {
            // Реалізуємо новий метод, який повертає List<string>
            List<string> results = new List<string>();
            
            // Обробляємо файли
            if (images != null && images.Any())
            {
                foreach (var image in images)
                {
                    // Тут можна додати логіку для тимчасового збереження або конвертації файлу
                    results.Add($"file:{image.FileName}");
                }
            }
            
            // Додаємо URL
            if (imageUrls != null && imageUrls.Any())
            {
                results.AddRange(imageUrls);
            }
            
            return results;
        }

        /// <summary>
        /// Генерує текст з використанням пам'яті агента як контексту
        /// </summary>
        public async Task<string> GenerateTextWithMemoryAsync(string prompt, string? conversationId = null, Guid? userId = null)
        {
            _logger.LogInformation("Генерація тексту з пам'яттю. ConversationId: {ConversationId}, UserId: {UserId}", 
                conversationId ?? "не вказано", userId?.ToString() ?? "не вказано");
            
            var fullPrompt = prompt;
            
            // Якщо є userId, але немає conversationId, створюємо глобальний ідентифікатор на основі userId
            if (string.IsNullOrEmpty(conversationId) && userId.HasValue)
            {
                conversationId = $"global-{userId.Value}";
                _logger.LogDebug("Використовуємо глобальний ідентифікатор розмови на основі UserId: {ConversationId}", conversationId);
            }
            
            // Якщо є conversationId і userId, додаємо контекст з пам'яті
            if (!string.IsNullOrEmpty(conversationId) && userId.HasValue)
            {
                try
                {
                    _logger.LogDebug("Спроба отримати контекст пам'яті для UserId: {UserId}, ConversationId: {ConversationId}", 
                        userId, conversationId);
                        
                    var memoryContext = await _memoryService.GetMemoryPromptContextAsync(userId.Value, conversationId);
                    
                    if (!string.IsNullOrEmpty(memoryContext))
                    {
                        _logger.LogDebug("Отримано контекст пам'яті розміром {Size} символів", memoryContext.Length);
                        
                        // Додаємо контекст перед основним промтом з чіткими інструкціями
                        fullPrompt = $"""
                        === ІСТОРІЯ РОЗМОВИ ТА КОНТЕКСТ ===
                        {memoryContext}
                        
                        ВАЖЛИВО: Ти маєш використовувати цю історію як контекст для своєї відповіді. 
                        Ця інформація дуже важлива для формулювання повної та правильної відповіді.
                        Якщо користувач посилається на щось згадане раніше в історії, обов'язково використай цю інформацію.
                        
                        === ПОТОЧНИЙ ЗАПИТ ===
                        {prompt}
                        
                        Відповідай так, ніби пам'ятаєш весь попередній контекст розмови. Не згадуй, що ти використовуєш історію.
                        """;
                        
                        _logger.LogDebug("Сформовано повний промт з контекстом пам'яті, загальний розмір: {Size} символів", 
                            fullPrompt.Length);
                    }
                    else
                    {
                        _logger.LogDebug("Контекст пам'яті порожній, використовую оригінальний промт");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Помилка при отриманні контексту пам'яті для UserId: {UserId}, ConversationId: {ConversationId}", 
                        userId, conversationId);
                    // Продовжуємо з оригінальним промтом
                }
            }
            else
            {
                _logger.LogDebug("ConversationId або UserId не вказано, використовую оригінальний промт без контексту пам'яті");
            }
            
            // Відправляємо запит до LLM
            try
            {
                _logger.LogDebug("Відправляю запит до LLM...");
                var response = await _clientService.GenerateTextAsync(fullPrompt);
                _logger.LogDebug("Отримано відповідь від LLM довжиною {Length} символів", response?.Length ?? 0);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при генерації тексту через LLM");
                throw; // Прокидаємо помилку далі
            }
        }
        
        public async Task<string> GetCompletionAsync(string prompt, string? modelId = null)
        {
            return await _clientService.CompletionAsync(prompt, new Dictionary<string, object>
            {
                { "model", modelId ?? "gpt-3.5-turbo" }
            });
        }
        
        public async Task<string> GetChatCompletionAsync(string systemMessage, string userMessage, string? modelId = null)
        {
            var messages = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "role", "system" }, { "content", systemMessage } },
                new Dictionary<string, string> { { "role", "user" }, { "content", userMessage } }
            };
            
            return await GetChatCompletionAsync(messages, modelId);
        }
        
        public async Task<string> GetChatCompletionAsync(List<Dictionary<string, string>> messages, string? modelId = null)
        {
            return await _clientService.ChatCompletionAsync(messages, new Dictionary<string, object>
            {
                { "model", modelId ?? "gpt-3.5-turbo" }
            });
        }
        
        public async Task<string> GenerateImageDescriptionAsync(IFormFile imageFile, string? prompt = null)
        {
            return await _clientService.DescribeImageAsync(imageFile, prompt ?? "Опиши це зображення детально");
        }
        
        public async Task<string> GenerateImageDescriptionAsync(string imageUrl, string? prompt = null)
        {
            throw new NotImplementedException("Опис зображення за URL ще не реалізовано");
        }
    }
}