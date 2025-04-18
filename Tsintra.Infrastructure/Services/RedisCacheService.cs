using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using Tsintra.Domain.Interfaces;

namespace Tsintra.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private bool IsRedisAvailable()
    {
        try
        {
            return _redis?.IsConnected ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Redis availability");
            return false;
        }
    }

    public async Task<T> GetAsync<T>(string key)
    {
        if (!IsRedisAvailable())
        {
            _logger.LogWarning("Redis unavailable. Cannot retrieve key: {key}", key);
            return default;
        }

        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving key from Redis: {key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        if (!IsRedisAvailable())
        {
            _logger.LogWarning("Redis unavailable. Cannot set key: {key}", key);
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var serializedValue = JsonSerializer.Serialize(value);

            await db.StringSetAsync(key, serializedValue, expiry);
            _logger.LogDebug("Successfully set Redis key: {key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key in Redis: {key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        if (!IsRedisAvailable())
        {
            _logger.LogWarning("Redis unavailable. Cannot remove key: {key}", key);
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            _logger.LogDebug("Successfully removed Redis key: {key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing key from Redis: {key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (!IsRedisAvailable())
        {
            _logger.LogWarning("Redis unavailable. Cannot check if key exists: {key}", key);
            return false;
        }

        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key exists in Redis: {key}", key);
            return false;
        }
    }
} 