using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Microsoft.AspNetCore.Http;
using Tsintra.MarketplaceAgent.Models;
using Tsintra.MarketplaceAgent.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace Tsintra.MarketplaceAgent.Agents;

/// <summary>
/// Агент для інтерактивного чату з підтримкою аналізу зображень, запам'ятовування контексту
/// та побудови аналітики щодо товарів, ринку та аудиторії.
/// </summary>
public class ChatAgent
{
    private readonly ILogger<ChatAgent> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentMemoryService _agentMemoryService;
    private readonly ILLMClient _llmClient;
    private readonly ILLMService _llmService;
    private Guid _clientUserId;
    
    public ChatAgent(
        ILogger<ChatAgent> logger,
        IServiceProvider serviceProvider,
        IAgentMemoryService agentMemoryService,
        ILLMClient llmClient,
        ILLMService llmService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _agentMemoryService = agentMemoryService ?? throw new ArgumentNullException(nameof(agentMemoryService));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _clientUserId = Guid.Empty;
        
        _logger.LogInformation("ChatAgent initialized with agent memory service and LLM services");
        
        // Перевіряємо підключення до Redis
        CheckRedisConnectionAsync().GetAwaiter().GetResult();
    }
    
    private async Task CheckRedisConnectionAsync()
    {
        try
        {
            var testMemory = await _agentMemoryService.GetMemoryAsync(_clientUserId, "test");
            _logger.LogInformation("Redis connection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis");
            // Не кидаємо помилку, щоб не блокувати ініціалізацію
            // Але логуємо для відстеження проблем
        }
    }
    
    /// <summary>
    /// Встановлює ID користувача для операцій з пам'яттю
    /// </summary>
    public void SetClientUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }
        _clientUserId = userId;
        _logger.LogInformation("Client user ID set to {UserId}", userId);
    }
    
    /// <summary>
    /// Отримує глобальний ідентифікатор розмови для користувача
    /// </summary>
    private string GetGlobalConversationId()
    {
        if (_clientUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Client user ID must be set before getting global conversation ID");
        }
        
        // Створюємо стабільний ідентифікатор на основі ID користувача
        // Це гарантує, що для одного й того ж користувача завжди буде використовуватися один і той же ідентифікатор
        return $"global-{_clientUserId}";
    }
    
    /// <summary>
    /// Обробляє повідомлення користувача, зберігає його в пам'яті та генерує відповідь
    /// </summary>
    public async Task<string> ProcessMessageAsync(
        string message, 
        List<IFormFile> images, 
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var timings = new Dictionary<string, long>();
        
        if (_clientUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Client user ID must be set before processing messages");
        }
        
        // Якщо ID розмови не вказано, використовуємо глобальний ID для цього користувача
        if (string.IsNullOrEmpty(conversationId))
        {
            conversationId = GetGlobalConversationId();
            _logger.LogInformation("Using global conversation ID for user: {ConversationId}", conversationId);
        }
        
        try
        {
            _logger.LogInformation("Processing message in conversation: {conversationId}", conversationId);
            
            // Get memory context
            var memoryContext = await _agentMemoryService.GetMemoryPromptContextAsync(_clientUserId, conversationId);
            _logger.LogInformation("Retrieved memory context for conversation: {conversationId}. Context length: {length}", 
                conversationId, memoryContext?.Length ?? 0);
            
            string response;
            
            if (images != null && images.Any())
            {
                // Process images first
                var imageAnalysisResult = await ProcessImagesAsync(message, images, conversationId, cancellationToken);
                response = await GenerateResponseWithImagesAsync(message, imageAnalysisResult, conversationId, cancellationToken);
            }
            else
            {
                // Generate response with memory context
                response = await GenerateResponseAsync(message, conversationId, cancellationToken);
            }
            
            // Save to memory
            await SaveActionToMemoryAsync(conversationId, "Message", new
            {
                UserMessage = message,
                AssistantResponse = response,
                Timestamp = DateTime.UtcNow,
                HasImages = images?.Any() ?? false
            }, cancellationToken);
            
            _logger.LogInformation("Saved message and response to memory for conversation: {conversationId}", conversationId);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message in conversation: {conversationId}", conversationId);
            throw;
        }
    }
    
    /// <summary>
    /// Аналізує зображення та генерує їх опис
    /// </summary>
    private async Task<string> ProcessImagesAsync(
        string userPrompt, 
        List<IFormFile> images, 
        string conversationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Формуємо промпт для аналізу зображень
            string analysisPrompt = string.IsNullOrEmpty(userPrompt) 
                ? "Опиши детально, що зображено на цих фотографіях. Зверни увагу на товари, якщо вони присутні."
                : $"Опиши детально, що зображено на цих фотографіях, враховуючи запит користувача: {userPrompt}";
            
            // Отримуємо опис зображень через LLM
            var imageDescription = await _llmService.DescribeImagesAsync(analysisPrompt, images, new List<string>());
            
            stopwatch.Stop();
            
            _logger.LogInformation("Image analysis completed in {ElapsedMilliseconds} ms", 
                stopwatch.ElapsedMilliseconds);
            
            return imageDescription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing images for conversation {ConversationId}", conversationId);
            throw new Exception("Не вдалося проаналізувати зображення", ex);
        }
    }
    
    /// <summary>
    /// Генерує відповідь на текстове повідомлення з використанням пам'яті
    /// </summary>
    private async Task<string> GenerateResponseAsync(
        string message, 
        string conversationId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get memory context
            var memoryContext = await _agentMemoryService.GetMemoryPromptContextAsync(_clientUserId, conversationId);
            
            // Build prompt with memory context
            var prompt = new System.Text.StringBuilder();
            
            // Додаємо чіткі інструкції для моделі
            prompt.AppendLine("Ти асистент для чату. Твоє завдання - відповідати на запитання користувача, враховуючи попередню історію діалогу.");
            prompt.AppendLine("Уважно аналізуй попередні повідомлення і відповідай так, ніби ти добре пам'ятаєш весь попередній контекст розмови.");
            prompt.AppendLine("Ти ПОВИНЕН використовувати інформацію з історії при формулюванні відповідей.");
            prompt.AppendLine("Якщо користувач посилається на щось, що було згадано раніше, обов'язково використовуй цю інформацію з історії.");
            prompt.AppendLine();
            
            if (!string.IsNullOrEmpty(memoryContext))
            {
                prompt.AppendLine("=== ІСТОРІЯ РОБОТИ АГЕНТА ===");
                prompt.AppendLine(memoryContext);
                prompt.AppendLine();
                prompt.AppendLine("Використовуй цю ІСТОРІЮ РОБОТИ АГЕНТА як контекст для своєї відповіді.");
                prompt.AppendLine();
            }
            
            prompt.AppendLine("=== ПОТОЧНЕ ПОВІДОМЛЕННЯ ===");
            prompt.AppendLine(message);
            
            // Generate response
            return await _llmClient.GenerateTextAsync(prompt.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response for conversation: {conversationId}", conversationId);
            throw;
        }
    }
    
    /// <summary>
    /// Генерує відповідь на повідомлення з урахуванням результатів аналізу зображень
    /// </summary>
    private async Task<string> GenerateResponseWithImagesAsync(
        string userMessage, 
        string imageAnalysisResult, 
        string conversationId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Формуємо промпт з аналізом зображень
            string prompt = $"""
            Ти асистент для чату з підтримкою аналізу зображень. 
            
            Твоє завдання - відповідати на запитання користувача, враховуючи попередню історію діалогу та результати аналізу зображень.
            При формулюванні відповіді обов'язково використовуй інформацію як з історії розмови, так і з аналізу зображень.
            Відповідай так, ніби ти добре пам'ятаєш весь попередній контекст розмови.
            
            Я надіслав тобі фотографії, які ти проаналізував так:
            
            {imageAnalysisResult}
            
            Моє повідомлення: {userMessage}
            
            Будь ласка, дай розгорнуту відповідь, яка враховує як поточний запит, так і історію нашої розмови. 
            Якщо на фото товари, додай рекомендації щодо цільової аудиторії, 
            стратегії просування, можливого ціноутворення та маркетингових підходів. Використовуй свої знання про ринок.
            """;
            
            // Використовуємо генерацію тексту з контекстом пам'яті
            return await _llmService.GenerateTextWithMemoryAsync(prompt, conversationId, _clientUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response with images for conversation {ConversationId}", conversationId);
            throw new Exception("Не вдалося згенерувати відповідь з аналізом зображень", ex);
        }
    }
    
    /// <summary>
    /// Генерує аналітику ринку на основі попереднього діалогу
    /// </summary>
    public async Task<string> GenerateMarketAnalysisAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        if (_clientUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Client user ID must be set before generating market analysis");
        }
        
        // Якщо ID розмови не вказано, використовуємо глобальний ID для цього користувача
        if (string.IsNullOrEmpty(conversationId))
        {
            conversationId = GetGlobalConversationId();
            _logger.LogInformation("Using global conversation ID for market analysis: {ConversationId}", conversationId);
        }
        
        _logger.LogInformation("Generating market analysis for conversation {ConversationId}", conversationId);
        
        try
        {
            // Зберігаємо запит на аналіз в пам'яті
            await SaveActionToMemoryAsync(conversationId, "MarketAnalysisRequest", new
            {
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            
            // Використовуємо LLM для генерації аналізу з контекстом пам'яті
            // Промпт для аналізу ринку
            string prompt = @"
                На основі всієї наявної інформації про продукт та його аналогів, створи детальний аналіз ринку.
                Аналіз має включати:
                1. Опис основних конкурентів та їх продуктів
                2. Цінові діапазони на ринку
                3. Тенденції та тренди у цій категорії
                4. Можливі ринкові ніші та позиціонування
                5. Цільову аудиторію та її потреби
                6. Рекомендації щодо маркетингової стратегії
                
                Надай аналітику в структурованому вигляді, з чіткими заголовками та підзаголовками.
            ";
            
            var analysis = await _llmService.GenerateTextWithMemoryAsync(prompt, conversationId, _clientUserId);
            
            // Зберігаємо результати аналізу в пам'яті
            await SaveActionToMemoryAsync(conversationId, "MarketAnalysisResult", new
            {
                Analysis = analysis,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating market analysis for conversation {ConversationId}", conversationId);
            
            // Зберігаємо інформацію про помилку
            await SaveActionToMemoryAsync(conversationId, "AnalysisError", new
            {
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            
            return $"Під час генерації аналітики сталася помилка: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Зберігає дію агента в пам'яті, доповнюючи існуючу пам'ять
    /// </summary>
    private async Task SaveActionToMemoryAsync(
        string conversationId, 
        string action, 
        object data, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var timings = new Dictionary<string, long>();
        
        try
        {
            _logger.LogDebug("Saving action {Action} to memory for conversation {ConversationId}", 
                action, conversationId);
                
            // Створюємо новий запис дії
            var newAction = new
            {
                Action = action,
                Timestamp = DateTime.UtcNow,
                Data = data
            };

            timings.Add("CreateAction", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();

            // Отримуємо існуючий запис пам'яті (якщо є)
            var existingMemory = await _agentMemoryService.GetMemoryAsync(_clientUserId, conversationId);
            
            timings.Add("GetExistingMemory", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();
            
            List<object> memoryActions = new List<object>();

            if (existingMemory != null && !string.IsNullOrEmpty(existingMemory.Content))
            {
                try
                {
                    memoryActions = JsonSerializer.Deserialize<List<object>>(existingMemory.Content) ?? new List<object>();
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Failed to deserialize existing memory content, starting with empty list");
                    memoryActions = new List<object>();
                }
            }

            memoryActions.Add(newAction);

            timings.Add("DeserializeAndAddAction", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();

            // Серіалізуємо оновлений список дій
            string serializedActions = JsonSerializer.Serialize(memoryActions);

            timings.Add("SerializeActions", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();

            // Створюємо або оновлюємо запис пам'яті
            await _agentMemoryService.SaveMemoryAsync(new AgentMemory
            {
                Id = existingMemory?.Id ?? Guid.NewGuid(),
                UserId = _clientUserId,
                ConversationId = conversationId,
                Content = serializedActions,
                CreatedAt = existingMemory?.CreatedAt ?? DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30) // Зберігаємо на 30 днів
            });
            
            timings.Add("SaveMemory", stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
            
            // Детальне логування тільки якщо операція зайняла більше 100мс
            if (timings.Values.Sum() > 100)
            {
                _logger.LogInformation("SaveActionToMemoryAsync timing for {Action} (ms): {Timings}, Total: {Total}ms", 
                    action, string.Join(", ", timings.Select(t => $"{t.Key}={t.Value}")), timings.Values.Sum());
            }
            
            _logger.LogDebug("Successfully saved action to memory");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to save action {Action} to memory: {ExceptionMessage}. Elapsed time: {ElapsedTime}ms", 
                action, ex.Message, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task<List<object>> GetMemoryHistoryAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting memory history for conversation {ConversationId}", conversationId);
            
            var memory = await _agentMemoryService.GetMemoryAsync(_clientUserId, conversationId);
            
            if (memory == null)
            {
                _logger.LogDebug("No memory found for conversation {ConversationId}", conversationId);
                return new List<object>();
            }
            
            if (memory.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Memory expired for conversation {ConversationId}", conversationId);
                return new List<object>();
            }
            
            if (string.IsNullOrEmpty(memory.Content))
            {
                _logger.LogDebug("Empty memory content for conversation {ConversationId}", conversationId);
                return new List<object>();
            }

            try
            {
                var memoryActions = JsonSerializer.Deserialize<List<object>>(memory.Content);
                if (memoryActions != null)
                {
                    _logger.LogDebug("Retrieved {Count} memory actions for conversation {ConversationId}", 
                        memoryActions.Count, conversationId);
                    return memoryActions;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize memory content for conversation {ConversationId}", conversationId);
            }
            
            return new List<object>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory history for conversation {ConversationId}", conversationId);
            return new List<object>();
        }
    }
} 