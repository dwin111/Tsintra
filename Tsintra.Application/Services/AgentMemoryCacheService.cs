using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Application.Services;

public class AgentMemoryCacheService : IAgentMemoryCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AgentMemoryCacheService> _logger;
    private readonly MemoryCacheOptions _options;

    public AgentMemoryCacheService(
        IMemoryCache memoryCache,
        ILogger<AgentMemoryCacheService> logger,
        IOptions<MemoryCacheOptions> options)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            return _memoryCache.Get<T>(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from memory cache for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.SetAbsoluteExpiration(expiration.Value);
            }
            _memoryCache.Set(key, value, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in memory cache for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            _memoryCache.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from memory cache for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return _memoryCache.TryGetValue(key, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key exists in memory cache: {Key}", key);
            return false;
        }
    }
} 