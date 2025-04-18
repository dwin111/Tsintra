using Microsoft.Extensions.Logging;
using Tsintra.Application.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System; // For Guid
using Tsintra.Domain.Interfaces; // For IAgentMemoryService
using System.Linq; // For LINQ operations
using System.Text.Json; // For JsonSerializer
using Tsintra.Domain.Models; // For AgentMemory
using Tsintra.Domain.DTOs; // For DTOs

namespace Tsintra.Application.Services;

public class ProductGenerationService : IProductGenerationService
{
    private readonly ILogger<ProductGenerationService> _logger;
    private readonly IProductGenerationTools _listingAgent;
    private readonly IAgentMemoryService _agentMemoryService;
    private readonly IAgentMemoryRepository _agentMemoryRepository;
    private readonly ILLMServices _llmServices;
    private Guid _userId = Guid.Empty;
    private string _lastConversationId;
    private const string PERSISTENT_CONVERSATION_PREFIX = "product-generation-persistent-";

    public ProductGenerationService(
        ILogger<ProductGenerationService> logger, 
        IProductGenerationTools listingAgent,
        IAgentMemoryService agentMemoryService,
        IAgentMemoryRepository agentMemoryRepository,
        ILLMServices llmServices)
    {
        _logger = logger;
        _listingAgent = listingAgent;
        _agentMemoryService = agentMemoryService;
        _agentMemoryRepository = agentMemoryRepository;
        _llmServices = llmServices;
    }

    public void SetUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }
        
        _userId = userId;
        _listingAgent.SetClientUserId(userId);

        // Generate a persistent conversation ID based on the user ID
        // This ensures we always use the same Redis cache for this user
        _lastConversationId = $"{PERSISTENT_CONVERSATION_PREFIX}{_userId}";
        _logger.LogInformation("Set user ID for ListingAgent to {UserId}, using persistent conversation ID: {ConversationId}", userId, _lastConversationId);
    }

    public async Task<ProductDetailsDto?> GenerateProductAsync(
        IEnumerable<string> base64Images,
        string? userHints,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting product generation via ProductGenerationService...");
        try
        {
            if (_userId == Guid.Empty)
            {
                throw new InvalidOperationException("User ID must be set before generating products");
            }

            // Always use the consistent conversation ID for this user
            // No need to generate a new one or check if it's empty
            string conversationId = _lastConversationId;
            _logger.LogInformation("Using persistent conversation ID: {ConversationId}", conversationId);
            
            // Try to enrich user hints with information from chat memory
            string enrichedUserHints = await EnrichUserHintsFromChatMemoryAsync(userHints, cancellationToken);
            
            // If we found any useful information in chat memory, log it
            if (!string.IsNullOrEmpty(enrichedUserHints) && enrichedUserHints != userHints)
            {
                _logger.LogInformation("Enhanced user hints with information from chat memory");
                
                // Save the fact that we used chat memory to enhance the hints
                await SaveActionToMemoryAsync(conversationId, "EnhancedFromChatMemory", new
                {
                    OriginalHints = userHints,
                    EnrichedHints = enrichedUserHints
                }, cancellationToken);
                
                // Use the enriched hints instead of original ones
                userHints = enrichedUserHints;
            }
            
            // Save generation start to memory
            await SaveActionToMemoryAsync(conversationId, "StartProductGeneration", new
            {
                UserHints = userHints,
                ImageCount = base64Images?.Count() ?? 0,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            
            // Pass the conversationId to the ListingAgent to ensure it uses the same memory context
            var productDetails = await _listingAgent.GenerateProductAsync(
                base64Images, 
                userHints: userHints, 
                conversationId: conversationId, 
                cancellationToken: cancellationToken);
            
            if(productDetails == null)
            {
                _logger.LogWarning("ListingAgent returned null product details.");
                
                // Save error to memory
                await SaveActionToMemoryAsync(conversationId, "ErrorNullProductDetails", new {
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Product generation successful: {ProductName}", productDetails.RefinedTitle);
                
                // Save success to memory
                await SaveActionToMemoryAsync(conversationId, "SuccessfulProductGeneration", new
                {
                    ProductName = productDetails.RefinedTitle,
                    ImagesCount = productDetails.Images?.Count ?? 0,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
            }
            
            return productDetails;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error occurred during product generation in ProductGenerationService.");
            
            if (!string.IsNullOrEmpty(_lastConversationId))
            {
                // Save error to memory
                await SaveActionToMemoryAsync(_lastConversationId, "ErrorProductGeneration", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
            }
            
            return null;
        }
    }

    public async Task<PublishResultDto> PublishProductAsync(
        ProductDetailsDto productDetails,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting product publishing via ProductGenerationService for: {ProductName}...", 
            productDetails.RefinedTitle);
            
        try
        {
            if (_userId == Guid.Empty)
            {
                throw new InvalidOperationException("User ID must be set before publishing products");
            }

            // Always use the persistent conversation ID for this user
            string conversationId = _lastConversationId;
            _logger.LogInformation("Using persistent conversation ID for publishing: {ConversationId}", conversationId);
            
            // Save publish start to memory
            await SaveActionToMemoryAsync(conversationId, "StartProductPublish", new
            {
                ProductName = productDetails.RefinedTitle,
                ImagesCount = productDetails.Images?.Count ?? 0,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            
            var result = await _listingAgent.PublishProductAsync(productDetails, conversationId, cancellationToken);
            
            _logger.LogInformation("Publishing attempt finished. Success: {Success}, Message: {Message}", 
                result.Success, result.Message);
                
            // Save publish result to memory
            await SaveActionToMemoryAsync(conversationId, result.Success ? "SuccessfulProductPublish" : "FailedProductPublish", 
                new
                {
                    Success = result.Success,
                    Message = result.Message,
                    PublishedItemId = result.PublishedItemId,
                    MarketplaceProductId = result.MarketplaceProductId,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
                
            return result;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error occurred during product publishing in ProductGenerationService.");
            
            if (!string.IsNullOrEmpty(_lastConversationId))
            {
                // Save error to memory
                await SaveActionToMemoryAsync(_lastConversationId, "ErrorProductPublish", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
            }
            
            return new PublishResultDto 
            { 
                Success = false, 
                Message = $"An unexpected error occurred during publishing: {ex.Message}" 
            };
        }
    }
    
    /// <summary>
    /// Збагачує підказки користувача інформацією з чату
    /// </summary>
    private async Task<string> EnrichUserHintsFromChatMemoryAsync(string? userHints, CancellationToken cancellationToken = default)
    {
        if (_userId == Guid.Empty)
        {
            _logger.LogWarning("Cannot enrich user hints: User ID is not set");
            return userHints ?? string.Empty;
        }
        
        try
        {
            // Шукаємо розмови в чаті для цього користувача
            var chatConversations = await FindChatConversationsAsync(_userId);
            
            if (!chatConversations.Any())
            {
                _logger.LogInformation("No chat conversations found for user {UserId}", _userId);
                return userHints ?? string.Empty;
            }
            
            // Беремо найновішу розмову в чаті (найбільш актуальну)
            var latestChatConversationId = chatConversations.First();
            _logger.LogInformation("Found chat conversation: {ConversationId}", latestChatConversationId);
            
            // Створюємо промпт для LLM, щоб витягнути інформацію про продукт з розмови
            string prompt = $"""
            Ти допомагаєш проаналізувати попередню розмову з користувачем та витягнути інформацію про продукт, який вони хочуть додати на маркетплейс.
            
            Якщо користувач описав продукт (його характеристики, деталі, цільову аудиторію, тощо), надай цю інформацію у структурованому вигляді.
            Витягни будь-яку корисну інформацію про:
            - Назву продукту
            - Категорію
            - Опис
            - Характеристики (матеріал, розмір, колір тощо)
            - Цільову аудиторію
            - Переваги продукту
            - Будь-які інші специфічні деталі
            
            Якщо користувач не описав жодних деталей продукту, поверни "Немає інформації про продукт".
            Надай відповідь у форматі, зручному для використання як підказки для генерації продукту.
            """;
            
            // Використовуємо LLM для аналізу розмови
            var productInfo = await _llmServices.GenerateTextWithMemoryAsync(prompt, latestChatConversationId, _userId);
            
            if (string.IsNullOrEmpty(productInfo) || productInfo.Contains("Немає інформації про продукт"))
            {
                _logger.LogInformation("No useful product information found in chat");
                return userHints ?? string.Empty;
            }
            
            // Комбінуємо знайдену інформацію з оригінальними підказками користувача
            if (string.IsNullOrEmpty(userHints))
            {
                return productInfo;
            }
            else
            {
                return $"{userHints}\n\nДодаткова інформація з чату:\n{productInfo}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching user hints from chat memory");
            return userHints ?? string.Empty; // У разі помилки повертаємо оригінальні підказки
        }
    }
    
    /// <summary>
    /// Знаходить ID розмов у чаті для користувача
    /// </summary>
    private async Task<List<string>> FindChatConversationsAsync(Guid userId)
    {
        try
        {
            // Список для зберігання ID розмов
            List<string> chatConversationIds = new List<string>();
            
            // Спочатку шукаємо в базі даних через репозиторій
            var memories = await _agentMemoryRepository.GetAllForUserAsync(userId);
            
            // Фільтруємо тільки розмови чату (їх ID починаються з "chat-")
            chatConversationIds = memories
                .Where(m => m.ConversationId.StartsWith("chat-"))
                .OrderByDescending(m => m.CreatedAt) // Найновіші спочатку
                .Select(m => m.ConversationId)
                .ToList();
                
            return chatConversationIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding chat conversations for user {UserId}", userId);
            return new List<string>();
        }
    }
    
    /// <summary>
    /// Отримує всі записи пам'яті для користувача
    /// </summary>
    private async Task<List<AgentMemory>> GetUserMemoriesAsync(Guid userId)
    {
        try 
        {
            // Використовуємо репозиторій для отримання всіх записів пам'яті користувача
            var memories = await _agentMemoryRepository.GetAllForUserAsync(userId);
            return memories.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting memories for user {UserId}", userId);
            return new List<AgentMemory>();
        }
    }
    
    /// <summary>
    /// Зберігає дію в пам'яті агента, доповнюючи існуючу пам'ять
    /// </summary>
    private async Task SaveActionToMemoryAsync(
        string conversationId, 
        string action, 
        object data, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Створюємо новий запис дії
            var newAction = new
            {
                Action = action,
                Timestamp = DateTime.UtcNow,
                Data = data
            };

            // Отримуємо існуючий запис пам'яті (якщо є)
            var existingMemory = await _agentMemoryService.GetMemoryAsync(_userId, conversationId);
            List<object> memoryActions = new List<object>();

            if (existingMemory != null && !string.IsNullOrEmpty(existingMemory.Content))
            {
                try
                {
                    // Десеріалізуємо існуючий контент
                    memoryActions = JsonSerializer.Deserialize<List<object>>(existingMemory.Content) ?? new List<object>();
                    _logger.LogDebug("Знайдено існуючу пам'ять з {Count} діями для розмови {ConversationId}", 
                        memoryActions.Count, conversationId);
                }
                catch
                {
                    // Якщо не вдалося десеріалізувати як List<object>, спробуємо як одиночний об'єкт
                    var singleAction = JsonSerializer.Deserialize<object>(existingMemory.Content);
                    if (singleAction != null)
                    {
                        memoryActions.Add(singleAction);
                        _logger.LogDebug("Знайдено існуючу пам'ять з одиночною дією для розмови {ConversationId}", 
                            conversationId);
                    }
                }
            }
            else
            {
                _logger.LogDebug("Пам'ять для розмови {ConversationId} не знайдена, створюємо нову", conversationId);
            }

            // Додаємо нову дію
            memoryActions.Add(newAction);
            _logger.LogDebug("Додано нову дію '{Action}' до пам'яті, тепер всього {Count} дій", 
                action, memoryActions.Count);

            // Серіалізуємо оновлений список дій
            string jsonContent = JsonSerializer.Serialize(memoryActions, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Створюємо або оновлюємо запис пам'яті
            var memory = existingMemory ?? new AgentMemory
            {
                UserId = _userId,
                ConversationId = conversationId,
                Content = "[]",
                CreatedAt = DateTime.UtcNow
            };

            memory.Content = jsonContent;
            memory.ExpiresAt = DateTime.UtcNow.AddDays(30); // Оновлюємо термін дії

            await _agentMemoryService.SaveMemoryAsync(memory);
            
            // Логування оновленого вмісту пам'яті
            _logger.LogInformation("Оновлено пам'ять для розмови {ConversationId}. Поточний стан пам'яті: {ActionCount} дій. Розмір контенту: {ContentSize} символів", 
                conversationId, memoryActions.Count, jsonContent.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save action to memory for conversation {ConversationId}", conversationId);
            // Don't rethrow - we don't want memory failures to interrupt the main workflow
        }
    }
} 