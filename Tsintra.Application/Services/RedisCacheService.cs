using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.Json;

namespace Tsintra.Application.Services;

public interface IRedisCacheService
{
    Task<T> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
}

public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _cache;

    public RedisCacheService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Redis connection string is missing");
        }

        var connection = ConnectionMultiplexer.Connect(connectionString);
        _cache = connection.GetDatabase();
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var value = await _cache.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(value);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var serializedValue = JsonSerializer.Serialize(value);
        await _cache.StringSetAsync(key, serializedValue, expiry);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _cache.KeyExistsAsync(key);
    }
} 