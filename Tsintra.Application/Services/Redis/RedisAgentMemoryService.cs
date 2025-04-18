using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.Json;
using Tsintra.Domain.Models;

namespace Tsintra.Application.Services.Redis;

public interface IRedisAgentMemoryService
{
    Task<string> GetConversationAsync(Guid userId, string conversationId);
    Task StoreConversationAsync(Guid userId, string conversationId, string conversationData);
    Task DeleteConversationAsync(Guid userId, string conversationId);
}

public class RedisAgentMemoryService : IRedisAgentMemoryService
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly string _keyPrefix = "agent:conversation:";
    private readonly TimeSpan _expiry = TimeSpan.FromDays(7); // Default expiry of 7 days
    
    public RedisAgentMemoryService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _database = _redis.GetDatabase();
    }
    
    public async Task<string> GetConversationAsync(Guid userId, string conversationId)
    {
        var key = $"{_keyPrefix}{userId}:{conversationId}";
        var data = await _database.StringGetAsync(key);
        return data.IsNullOrEmpty ? null : data.ToString();
    }
    
    public async Task StoreConversationAsync(Guid userId, string conversationId, string conversationData)
    {
        var key = $"{_keyPrefix}{userId}:{conversationId}";
        await _database.StringSetAsync(key, conversationData, _expiry);
    }
    
    public async Task DeleteConversationAsync(Guid userId, string conversationId)
    {
        var key = $"{_keyPrefix}{userId}:{conversationId}";
        await _database.KeyDeleteAsync(key);
    }
} 