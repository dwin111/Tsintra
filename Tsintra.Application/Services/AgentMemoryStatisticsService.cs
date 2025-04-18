using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Application.Services;

public class MemoryStatistics
{
    public int TotalMemoryCount { get; set; }
    public int ActiveMemoryCount { get; set; }
    public int ExpiredMemoryCount { get; set; }
    public long TotalContentSize { get; set; } // розмір у байтах
    public DateTime OldestMemoryDate { get; set; }
    public DateTime NewestMemoryDate { get; set; }
    public int UserCount { get; set; }
    
    // Середній розмір пам'яті у байтах
    public long AverageMemorySize => TotalMemoryCount > 0 ? TotalContentSize / TotalMemoryCount : 0;
}

public interface IAgentMemoryStatisticsService
{
    Task<MemoryStatistics> GetStatisticsAsync();
    Task<MemoryStatistics> GetStatisticsForUserAsync(Guid userId);
    Task<Dictionary<string, int>> GetTopConversationsAsync(int count = 10);
}

public class AgentMemoryStatisticsService : IAgentMemoryStatisticsService
{
    private readonly IAgentMemoryRepository _memoryRepository;
    private readonly ILogger<AgentMemoryStatisticsService> _logger;

    public AgentMemoryStatisticsService(
        IAgentMemoryRepository memoryRepository,
        ILogger<AgentMemoryStatisticsService> logger)
    {
        _memoryRepository = memoryRepository;
        _logger = logger;
    }

    public async Task<MemoryStatistics> GetStatisticsAsync()
    {
        try
        {
            _logger.LogDebug("Отримання загальної статистики пам'яті агента");

            var allMemories = await _memoryRepository.GetAllMemoriesAsync();
            var now = DateTime.UtcNow;

            var stats = new MemoryStatistics
            {
                TotalMemoryCount = allMemories.Count(),
                ActiveMemoryCount = allMemories.Count(m => m.ExpiresAt == null || m.ExpiresAt > now),
                ExpiredMemoryCount = allMemories.Count(m => m.ExpiresAt != null && m.ExpiresAt <= now),
                TotalContentSize = allMemories.Sum(m => m.Content?.Length ?? 0) * sizeof(char),
                UserCount = allMemories.Select(m => m.UserId).Distinct().Count(),
            };

            if (allMemories.Any())
            {
                stats.OldestMemoryDate = allMemories.Min(m => m.CreatedAt);
                stats.NewestMemoryDate = allMemories.Max(m => m.CreatedAt);
            }
            else
            {
                stats.OldestMemoryDate = stats.NewestMemoryDate = DateTime.UtcNow;
            }

            _logger.LogDebug("Статистика пам'яті агента успішно отримана");
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні статистики пам'яті агента");
            throw;
        }
    }

    public async Task<MemoryStatistics> GetStatisticsForUserAsync(Guid userId)
    {
        try
        {
            _logger.LogDebug("Отримання статистики пам'яті агента для користувача {UserId}", userId);

            var userMemories = await _memoryRepository.GetAllForUserAsync(userId);
            var now = DateTime.UtcNow;

            var stats = new MemoryStatistics
            {
                TotalMemoryCount = userMemories.Count(),
                ActiveMemoryCount = userMemories.Count(m => m.ExpiresAt == null || m.ExpiresAt > now),
                ExpiredMemoryCount = userMemories.Count(m => m.ExpiresAt != null && m.ExpiresAt <= now),
                TotalContentSize = userMemories.Sum(m => m.Content?.Length ?? 0) * sizeof(char),
                UserCount = 1 // завжди 1 для конкретного користувача
            };

            if (userMemories.Any())
            {
                stats.OldestMemoryDate = userMemories.Min(m => m.CreatedAt);
                stats.NewestMemoryDate = userMemories.Max(m => m.CreatedAt);
            }
            else
            {
                stats.OldestMemoryDate = stats.NewestMemoryDate = DateTime.UtcNow;
            }

            _logger.LogDebug("Статистика пам'яті агента для користувача {UserId} успішно отримана", userId);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні статистики пам'яті агента для користувача {UserId}", userId);
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetTopConversationsAsync(int count = 10)
    {
        try
        {
            _logger.LogDebug("Отримання топ-{Count} розмов за кількістю записів пам'яті", count);

            var allMemories = await _memoryRepository.GetAllMemoriesAsync();

            var topConversations = allMemories
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(count)
                .ToDictionary(x => x.ConversationId, x => x.Count);

            _logger.LogDebug("Топ розмови успішно отримані");
            return topConversations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні топ розмов");
            throw;
        }
    }
}