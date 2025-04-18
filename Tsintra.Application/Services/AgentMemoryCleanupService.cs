using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Domain.Interfaces;

namespace Tsintra.Application.Services;

public interface IAgentMemoryCleanupService
{
    Task CleanupExpiredMemoriesAsync(CancellationToken cancellationToken = default);
}

public class AgentMemoryCleanupService : IAgentMemoryCleanupService
{
    private readonly IAgentMemoryRepository _memoryRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AgentMemoryCleanupService> _logger;
    private readonly string _keyPrefix = "agent:memory:";

    public AgentMemoryCleanupService(
        IAgentMemoryRepository memoryRepository,
        ICacheService cacheService,
        ILogger<AgentMemoryCleanupService> logger)
    {
        _memoryRepository = memoryRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task CleanupExpiredMemoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Запуск очищення застарілих записів пам'яті агента");
            
            var expiredMemories = await _memoryRepository.GetExpiredMemoriesAsync(DateTime.UtcNow);
            var count = 0;

            foreach (var memory in expiredMemories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Очищення застарілих записів пам'яті перервано");
                    break;
                }

                var cacheKey = $"{_keyPrefix}{memory.UserId}:{memory.ConversationId}";
                
                try
                {
                    // Видалення з кешу - не зупиняємо обробку, якщо Redis недоступний
                    await _cacheService.RemoveAsync(cacheKey);
                }
                catch (Exception cacheEx)
                {
                    // Просто логуємо помилку кешу і продовжуємо
                    _logger.LogWarning(cacheEx, "Помилка видалення запису з кешу для {UserId}:{ConversationId}. Продовжуємо з базою даних",
                        memory.UserId, memory.ConversationId);
                }
                
                try
                {
                    // Видалення з бази даних
                    await _memoryRepository.DeleteAsync(memory.UserId, memory.ConversationId);
                    count++;
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Помилка видалення запису з бази даних для {UserId}:{ConversationId}",
                        memory.UserId, memory.ConversationId);
                    // Продовжуємо обробку наступних записів
                }
            }

            _logger.LogInformation("Очищення застарілих записів пам'яті завершено. Видалено {Count} записів", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка під час очищення застарілих записів пам'яті");
            // Не передаємо помилку далі, щоб сервіс міг продовжувати роботу
        }
    }
} 