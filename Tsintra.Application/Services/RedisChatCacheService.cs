using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Tsintra.Domain.Models;

namespace Tsintra.Application.Services
{
    public interface IRedisChatCacheService
    {
        // Conversation methods
        Task<Conversation> GetConversationAsync(Guid conversationId);
        Task<List<Conversation>> GetUserConversationsAsync(Guid userId);
        Task CacheConversationAsync(Conversation conversation, TimeSpan? expiry = null);
        Task RemoveConversationAsync(Guid conversationId);
        Task<bool> ConversationExistsAsync(Guid conversationId);
        
        // Message methods
        Task<List<Message>> GetConversationMessagesAsync(Guid conversationId);
        Task CacheMessageAsync(Message message, TimeSpan? expiry = null);
        Task CacheMessagesAsync(List<Message> messages, Guid conversationId, TimeSpan? expiry = null);
        
        // Connection status
        bool IsConnected { get; }
        Task<bool> TryReconnectAsync();
    }

    public class RedisChatCacheService : IRedisChatCacheService
    {
        private IDatabase _cache;
        private ConnectionMultiplexer _connection;
        private readonly ILogger<RedisChatCacheService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _convKeyPrefix = "chat:conversation:";
        private readonly string _msgKeyPrefix = "chat:messages:";
        private readonly string _userConvKeyPrefix = "chat:user:conversations:";
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromDays(7); // Default 7-day expiry
        private readonly ConfigurationOptions _redisOptions;
        private readonly object _connectionLock = new object();
        private bool _isInitialized = false;

        public bool IsConnected => _connection?.IsConnected ?? false;

        public RedisChatCacheService(IConfiguration configuration, ILogger<RedisChatCacheService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            try
            {
                var connectionString = configuration.GetConnectionString("Redis");
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "localhost:6379";
                    logger.LogWarning("Redis connection string not found, using default: {connectionString}", connectionString);
                }

                // Make sure we have abortConnect=false in the connection string
                if (!connectionString.Contains("abortConnect="))
                {
                    connectionString = connectionString + ",abortConnect=false";
                }

                // Configure Redis with resilient settings
                _redisOptions = ConfigurationOptions.Parse(connectionString);
                _redisOptions.AbortOnConnectFail = false;
                _redisOptions.ConnectRetry = 5;
                _redisOptions.ConnectTimeout = 5000;
                _redisOptions.SyncTimeout = 5000;
                _redisOptions.ResponseTimeout = 5000;
                
                // Initialize connection
                InitializeConnection();
                
                // Subscribe to connection events for automatic reconnection
                if (_connection != null)
                {
                    _connection.ConnectionFailed += OnConnectionFailed;
                    _connection.ConnectionRestored += OnConnectionRestored;
                    _connection.ErrorMessage += OnErrorMessage;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Redis connection. Chat caching will be disabled.");
                _cache = null;
                _connection = null;
            }
        }

        private void InitializeConnection()
        {
            lock (_connectionLock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    _connection = ConnectionMultiplexer.Connect(_redisOptions);
                    _cache = _connection.GetDatabase();
                    _isInitialized = true;
                    _logger.LogInformation("Redis connection initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Redis connection");
                    _cache = null;
                    _connection = null;
                }
            }
        }

        public async Task<bool> TryReconnectAsync()
        {
            if (IsConnected)
                return true;

            lock (_connectionLock)
            {
                if (IsConnected)
                    return true;

                try
                {
                    _logger.LogInformation("Attempting to reconnect to Redis...");
                    
                    if (_connection != null)
                    {
                        _connection.Dispose();
                    }
                    
                    _connection = ConnectionMultiplexer.Connect(_redisOptions);
                    _cache = _connection.GetDatabase();
                    
                    // Resubscribe to events
                    _connection.ConnectionFailed += OnConnectionFailed;
                    _connection.ConnectionRestored += OnConnectionRestored;
                    _connection.ErrorMessage += OnErrorMessage;
                    
                    _logger.LogInformation("Successfully reconnected to Redis");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect to Redis");
                    _cache = null;
                    return false;
                }
            }
        }

        private void OnConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            _logger.LogWarning("Redis connection failed: {endpoint}, {failureType}, {exception}", 
                e.EndPoint, e.FailureType, e.Exception?.Message);
        }

        private void OnConnectionRestored(object sender, ConnectionFailedEventArgs e)
        {
            _logger.LogInformation("Redis connection restored: {endpoint}", e.EndPoint);
        }

        private void OnErrorMessage(object sender, RedisErrorEventArgs e)
        {
            _logger.LogWarning("Redis error: {message}", e.Message);
        }

        // Helper method to check and potentially reconnect before operations
        private async Task<bool> EnsureConnectionAsync()
        {
            if (_cache != null && IsConnected)
                return true;

            return await TryReconnectAsync();
        }

        public async Task<Conversation> GetConversationAsync(Guid conversationId)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot retrieve conversation: {conversationId}", conversationId);
                return null;
            }
            
            try
            {
                var key = $"{_convKeyPrefix}{conversationId}";
                var value = await _cache.StringGetAsync(key);
                
                if (value.IsNullOrEmpty)
                {
                    _logger.LogDebug("Conversation not found in cache: {conversationId}", conversationId);
                    return null;
                }

                var conversation = JsonSerializer.Deserialize<Conversation>(value);
                _logger.LogDebug("Retrieved conversation from cache: {conversationId}", conversationId);
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation from cache: {conversationId}", conversationId);
                return null;
            }
        }

        public async Task<List<Conversation>> GetUserConversationsAsync(Guid userId)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot retrieve user conversations: {userId}", userId);
                return null;
            }
            
            try
            {
                var key = $"{_userConvKeyPrefix}{userId}";
                var value = await _cache.StringGetAsync(key);
                
                if (value.IsNullOrEmpty)
                {
                    _logger.LogDebug("User conversations not found in cache: {userId}", userId);
                    return null;
                }

                var conversations = JsonSerializer.Deserialize<List<Conversation>>(value);
                _logger.LogDebug("Retrieved {count} conversations for user from cache: {userId}", 
                    conversations?.Count ?? 0, userId);
                return conversations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user conversations from cache: {userId}", userId);
                return null;
            }
        }

        public async Task CacheConversationAsync(Conversation conversation, TimeSpan? expiry = null)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot cache conversation: {conversationId}", conversation?.Id);
                return;
            }
            
            if (conversation == null)
            {
                throw new ArgumentNullException(nameof(conversation));
            }

            try
            {
                var convKey = $"{_convKeyPrefix}{conversation.Id}";
                var userKey = $"{_userConvKeyPrefix}{conversation.UserId}";
                var expiryTime = expiry ?? _defaultExpiry;
                
                // Cache individual conversation
                var serializedConversation = JsonSerializer.Serialize(conversation);
                await _cache.StringSetAsync(convKey, serializedConversation, expiryTime);
                
                // Update user's conversation list
                var userConversations = await GetUserConversationsAsync(conversation.UserId) ?? new List<Conversation>();
                
                // Remove existing version if present and add the updated one
                userConversations.RemoveAll(c => c.Id == conversation.Id);
                userConversations.Add(conversation);
                
                // Sort by UpdatedAt date (newest first)
                userConversations.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));
                
                var serializedUserConversations = JsonSerializer.Serialize(userConversations);
                await _cache.StringSetAsync(userKey, serializedUserConversations, expiryTime);
                
                _logger.LogDebug("Cached conversation: {conversationId} for user: {userId}", 
                    conversation.Id, conversation.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching conversation: {conversationId}", conversation.Id);
            }
        }

        public async Task RemoveConversationAsync(Guid conversationId)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot remove conversation: {conversationId}", conversationId);
                return;
            }
            
            try
            {
                // First get the conversation to know its user
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    _logger.LogDebug("Cannot remove conversation from cache - not found: {conversationId}", conversationId);
                    return;
                }
                
                // Remove the conversation
                var convKey = $"{_convKeyPrefix}{conversationId}";
                await _cache.KeyDeleteAsync(convKey);
                
                // Remove messages
                var msgKey = $"{_msgKeyPrefix}{conversationId}";
                await _cache.KeyDeleteAsync(msgKey);
                
                // Update user's conversation list
                var userKey = $"{_userConvKeyPrefix}{conversation.UserId}";
                var userConversations = await GetUserConversationsAsync(conversation.UserId);
                
                if (userConversations != null)
                {
                    userConversations.RemoveAll(c => c.Id == conversationId);
                    var serializedUserConversations = JsonSerializer.Serialize(userConversations);
                    await _cache.StringSetAsync(userKey, serializedUserConversations, _defaultExpiry);
                }
                
                _logger.LogDebug("Removed conversation from cache: {conversationId}", conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing conversation from cache: {conversationId}", conversationId);
            }
        }

        public async Task<bool> ConversationExistsAsync(Guid conversationId)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot check if conversation exists: {conversationId}", conversationId);
                return false;
            }
            
            try
            {
                var key = $"{_convKeyPrefix}{conversationId}";
                return await _cache.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if conversation exists in cache: {conversationId}", conversationId);
                return false;
            }
        }

        public async Task<List<Message>> GetConversationMessagesAsync(Guid conversationId)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot retrieve messages for conversation: {conversationId}", conversationId);
                return new List<Message>();
            }
            
            try
            {
                var key = $"{_msgKeyPrefix}{conversationId}";
                _logger.LogInformation("Attempting to retrieve messages from Redis with key: {key}", key);
                
                var data = await _cache.StringGetAsync(key);
                if (data.IsNullOrEmpty)
                {
                    _logger.LogInformation("No messages found in Redis cache for conversation: {conversationId}", conversationId);
                    return new List<Message>();
                }
                
                var messages = JsonSerializer.Deserialize<List<Message>>(data);
                _logger.LogInformation("Retrieved {count} messages from Redis cache for conversation: {conversationId}", 
                    messages?.Count ?? 0, conversationId);
                
                return messages ?? new List<Message>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages from Redis cache for conversation: {conversationId}", conversationId);
                return new List<Message>();
            }
        }

        public async Task CacheMessageAsync(Message message, TimeSpan? expiry = null)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot cache message: {messageId}", message.Id);
                return;
            }
            
            try
            {
                var key = $"{_msgKeyPrefix}{message.ConversationId}";
                _logger.LogInformation("Attempting to cache message with key: {key}", key);
                
                // Get existing messages
                var existingMessages = await GetConversationMessagesAsync(message.ConversationId);
                existingMessages.Add(message);
                
                // Sort messages by timestamp
                existingMessages.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                
                var serializedMessages = JsonSerializer.Serialize(existingMessages);
                var expiryTime = expiry ?? _defaultExpiry;
                
                await _cache.StringSetAsync(key, serializedMessages, expiryTime);
                _logger.LogInformation("Successfully cached message {messageId} for conversation: {conversationId}", 
                    message.Id, message.ConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching message: {messageId}", message.Id);
            }
        }

        public async Task CacheMessagesAsync(List<Message> messages, Guid conversationId, TimeSpan? expiry = null)
        {
            if (!await EnsureConnectionAsync())
            {
                _logger.LogWarning("Redis cache unavailable. Cannot cache messages for conversation: {conversationId}", conversationId);
                return;
            }
            
            if (messages == null || !messages.Any())
            {
                return;
            }

            try
            {
                var key = $"{_msgKeyPrefix}{conversationId}";
                var expiryTime = expiry ?? _defaultExpiry;
                
                // Sort by timestamp (oldest first)
                messages.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                
                var serializedMessages = JsonSerializer.Serialize(messages);
                await _cache.StringSetAsync(key, serializedMessages, expiryTime);
                
                _logger.LogDebug("Cached {count} messages for conversation: {conversationId}", 
                    messages.Count, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching messages for conversation: {conversationId}", conversationId);
            }
        }
    }
} 