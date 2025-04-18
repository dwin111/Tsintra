using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Application.Services
{
    public interface IChatCleanupService
    {
        Task SyncRedisWithDatabaseAsync(CancellationToken cancellationToken = default);
    }

    public class ChatCleanupService : IChatCleanupService
    {
        private readonly IRedisChatCacheService _chatCache;
        private readonly IConversationRepository _conversationRepository;
        private readonly ILogger<ChatCleanupService> _logger;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromDays(7);

        public ChatCleanupService(
            IRedisChatCacheService chatCache,
            IConversationRepository conversationRepository,
            ILogger<ChatCleanupService> logger)
        {
            _chatCache = chatCache;
            _conversationRepository = conversationRepository;
            _logger = logger;
        }

        /// <summary>
        /// Synchronizes Redis cache with the PostgreSQL database to ensure data consistency
        /// </summary>
        public async Task SyncRedisWithDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting chat data sync from PostgreSQL to Redis");

                // Перевіряємо, чи доступний Redis
                try
                {
                    // Спробуємо встановити тестове значення, щоб переконатися, що Redis працює
                    await _chatCache.CacheConversationAsync(new Conversation 
                    { 
                        Id = Guid.NewGuid(), 
                        Title = "Test Connection",
                        UserId = Guid.Empty,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }, TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Redis is unavailable. Skipping sync operation.");
                    return;
                }

                // Отримати всі розмови з бази даних
                // У реальній реалізації бажано обмежити кількість і обробляти пакетами
                var conversations = await _conversationRepository.GetAllAsync(cancellationToken);
                int conversationCount = 0;
                int messageCount = 0;

                foreach (var conversation in conversations)
                {
                    // Пропустити, якщо запитано скасування
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Sync operation cancelled");
                        break;
                    }

                    try
                    {
                        // Завантажити повідомлення розмови
                        conversation.Messages = await _conversationRepository.GetConversationMessagesAsync(conversation.Id);
                        
                        // Кешувати дані розмови
                        await _chatCache.CacheConversationAsync(conversation, _cacheExpiry);
                        
                        // Кешувати повідомлення окремо
                        if (conversation.Messages != null && conversation.Messages.Count > 0)
                        {
                            await _chatCache.CacheMessagesAsync(conversation.Messages, conversation.Id, _cacheExpiry);
                            messageCount += conversation.Messages.Count;
                        }
                        
                        conversationCount++;
                        
                        // Логувати прогрес кожні 100 розмов
                        if (conversationCount % 100 == 0)
                        {
                            _logger.LogInformation("Processed {count} conversations", conversationCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing conversation {conversationId} to Redis", conversation.Id);
                        // Продовжуємо з наступною розмовою
                    }
                }

                _logger.LogInformation("Completed sync from PostgreSQL to Redis. Processed {conversationCount} conversations and {messageCount} messages", 
                    conversationCount, messageCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chat data sync operation");
                throw;
            }
        }
    }
} 