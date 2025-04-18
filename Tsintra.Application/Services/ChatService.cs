using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tsintra.Application.Interfaces;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.Domain.Enums;

namespace Tsintra.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IConversationRepository _conversationRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILLMServices _llmServices;
        private readonly IRedisChatCacheService _cacheService;
        private readonly ILogger<ChatService> _logger;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromDays(7);
        private readonly IChatRepository _chatRepository;

        public ChatService(
            IConversationRepository conversationRepository,
            IUserRepository userRepository,
            ILLMServices llmServices,
            IRedisChatCacheService cacheService,
            ILogger<ChatService> logger,
            IChatRepository chatRepository)
        {
            _conversationRepository = conversationRepository;
            _userRepository = userRepository;
            _llmServices = llmServices;
            _cacheService = cacheService;
            _logger = logger;
            _chatRepository = chatRepository;
        }

        // Helper method to check if Redis is available
        private bool IsRedisAvailable()
        {
            if (_cacheService == null || !_cacheService.IsConnected)
            {
                try
                {
                    // Try to reconnect if not connected
                    if (_cacheService != null && !_cacheService.IsConnected)
                    {
                        var reconnected = _cacheService.TryReconnectAsync().GetAwaiter().GetResult();
                        if (!reconnected)
                        {
                            _logger.LogWarning("Failed to reconnect to Redis. Will use database directly.");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking Redis availability. Will use database directly.");
                    return false;
                }
            }
            return _cacheService != null && _cacheService.IsConnected;
        }

        public async Task<List<Conversation>> GetUserConversationsAsync(Guid userId)
        {
            // Try to get from cache first if Redis is available
            if (IsRedisAvailable())
            {
                try
                {
                    var cachedConversations = await _cacheService.GetUserConversationsAsync(userId);
                    if (cachedConversations != null)
                    {
                        _logger.LogDebug("Retrieved user conversations from cache: {userId}", userId);
                        return cachedConversations;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve user conversations from cache: {userId}. Falling back to database.", userId);
                }
            }

            // Fallback to database
            var conversations = await _chatRepository.GetUserConversationsAsync(userId);
            _logger.LogDebug("Retrieved user conversations from database: {userId}", userId);
            
            // Try to cache the results for future use if Redis is available
            if (IsRedisAvailable() && conversations != null && conversations.Any())
            {
                try
                {
                    foreach (var conversation in conversations)
                    {
                        await _cacheService.CacheConversationAsync(conversation);
                    }
                    _logger.LogDebug("Cached {count} conversations for user: {userId}", 
                        conversations.Count, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache user conversations: {userId}", userId);
                }
            }
            
            return conversations;
        }

        public async Task<Conversation> GetConversationAsync(Guid conversationId)
        {
            // Try to get from cache first if Redis is available
            if (IsRedisAvailable())
            {
                try
                {
                    var cachedConversation = await _cacheService.GetConversationAsync(conversationId);
                    if (cachedConversation != null)
                    {
                        _logger.LogDebug("Retrieved conversation from cache: {conversationId}", conversationId);
                        return cachedConversation;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve conversation from cache: {conversationId}. Falling back to database.", conversationId);
                }
            }
            
            // Fallback to database
            var conversation = await _chatRepository.GetConversationAsync(conversationId);
            _logger.LogDebug("Retrieved conversation from database: {conversationId}", conversationId);
            
            // Try to cache the result for future use if Redis is available
            if (IsRedisAvailable() && conversation != null)
            {
                try
                {
                    await _cacheService.CacheConversationAsync(conversation);
                    _logger.LogDebug("Cached conversation: {conversationId}", conversationId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache conversation: {conversationId}", conversationId);
                }
            }
            
            return conversation;
        }

        public async Task<Conversation> CreateConversationAsync(Guid userId, string title)
        {
            // Create in database first
            var conversation = await _chatRepository.CreateConversationAsync(userId, title);
            _logger.LogInformation("Created new conversation: {conversationId} for user: {userId}", 
                conversation.Id, userId);
            
            // Try to cache the new conversation if Redis is available
            if (IsRedisAvailable())
            {
                try
                {
                    await _cacheService.CacheConversationAsync(conversation);
                    _logger.LogDebug("Cached new conversation: {conversationId}", conversation.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache new conversation: {conversationId}", conversation.Id);
                }
            }
            
            return conversation;
        }

        public async Task<bool> UpdateConversationTitleAsync(Guid conversationId, string newTitle)
        {
            // Update in database first
            var result = await _chatRepository.UpdateConversationTitleAsync(conversationId, newTitle);
            if (!result)
            {
                _logger.LogWarning("Failed to update conversation title in database: {conversationId}", conversationId);
                return false;
            }
            
            _logger.LogInformation("Updated conversation title in database: {conversationId}", conversationId);
            
            // Try to update in cache if Redis is available
            if (IsRedisAvailable())
            {
                try
                {
                    var conversation = await _cacheService.GetConversationAsync(conversationId);
                    if (conversation != null)
                    {
                        conversation.Title = newTitle;
                        conversation.UpdatedAt = DateTime.UtcNow;
                        await _cacheService.CacheConversationAsync(conversation);
                        _logger.LogDebug("Updated conversation title in cache: {conversationId}", conversationId);
                    }
                    else
                    {
                        // Refresh cache from database
                        conversation = await _chatRepository.GetConversationAsync(conversationId);
                        if (conversation != null)
                        {
                            await _cacheService.CacheConversationAsync(conversation);
                            _logger.LogDebug("Refreshed conversation in cache after title update: {conversationId}", conversationId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update conversation title in cache: {conversationId}", conversationId);
                }
            }
            
            return true;
        }

        public async Task<bool> DeleteConversationAsync(Guid conversationId)
        {
            // Delete from database first
            var result = await _chatRepository.DeleteConversationAsync(conversationId);
            if (!result)
            {
                _logger.LogWarning("Failed to delete conversation from database: {conversationId}", conversationId);
                return false;
            }
            
            _logger.LogInformation("Deleted conversation from database: {conversationId}", conversationId);
            
            // Try to remove from cache if Redis is available
            if (IsRedisAvailable())
            {
                try
                {
                    await _cacheService.RemoveConversationAsync(conversationId);
                    _logger.LogDebug("Removed conversation from cache: {conversationId}", conversationId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove conversation from cache: {conversationId}", conversationId);
                }
            }
            
            return true;
        }

        public async Task<Message> AddUserMessageAsync(Guid conversationId, string content)
        {
            // Check if conversation exists
            var conversation = await GetConversationAsync(conversationId);
            if (conversation == null)
            {
                _logger.LogWarning("Attempted to add message to non-existent conversation: {conversationId}", conversationId);
                throw new InvalidOperationException($"Conversation with ID {conversationId} not found");
            }
            
            // Create message in database
            var message = await _chatRepository.AddMessageAsync(conversationId, content, MessageRole.User);
            
            // Update conversation's UpdatedAt time
            await _chatRepository.UpdateConversationTimestampAsync(conversationId);
            _logger.LogInformation("Added user message to database: {messageId} for conversation: {conversationId}", 
                message.Id, conversationId);
            
            // Try to cache the message and update conversation timestamp if Redis is available
            if (IsRedisAvailable())
            {
                try
                {
                    await _cacheService.CacheMessageAsync(message);
                    
                    // Update conversation timestamp in cache too
                    conversation.UpdatedAt = DateTime.UtcNow;
                    await _cacheService.CacheConversationAsync(conversation);
                    
                    _logger.LogDebug("Cached user message: {messageId} for conversation: {conversationId}", 
                        message.Id, conversationId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache user message: {messageId}", message.Id);
                }
            }
            
            return message;
        }

        public async Task<Message> AddAssistantMessageAsync(Guid conversationId, string content)
        {
            try
            {
                // Check if conversation exists
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    _logger.LogWarning("Attempted to add assistant message to non-existent conversation: {conversationId}", conversationId);
                    throw new InvalidOperationException($"Conversation with ID {conversationId} not found");
                }
                
                // Create message in database
                var message = await _chatRepository.AddMessageAsync(conversationId, content, MessageRole.Assistant);
                
                // Update conversation's UpdatedAt time
                await _chatRepository.UpdateConversationTimestampAsync(conversationId);
                _logger.LogInformation("Added assistant message to database: {messageId} for conversation: {conversationId}", 
                    message.Id, conversationId);
                
                // Try to cache the message and update conversation timestamp if Redis is available
                if (IsRedisAvailable())
                {
                    try
                    {
                        await _cacheService.CacheMessageAsync(message);
                        
                        // Update conversation timestamp in cache too
                        conversation.UpdatedAt = DateTime.UtcNow;
                        await _cacheService.CacheConversationAsync(conversation);
                        
                        _logger.LogDebug("Cached assistant message: {messageId} for conversation: {conversationId}", 
                            message.Id, conversationId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache assistant message: {messageId}", message.Id);
                    }
                }
                
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding assistant message to conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<List<Message>> GetConversationHistoryAsync(Guid conversationId)
        {
            // First try the cache if Redis is available
            if (IsRedisAvailable())
            {
                try
                {
                    var cachedMessages = await _cacheService.GetConversationMessagesAsync(conversationId);
                    if (cachedMessages != null)
                    {
                        _logger.LogDebug("Retrieved {count} messages from cache for conversation: {conversationId}", 
                            cachedMessages.Count, conversationId);
                        return cachedMessages;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve messages from cache: {conversationId}. Falling back to database.", 
                        conversationId);
                }
            }
            
            // Fallback to database
            var messages = await _chatRepository.GetConversationMessagesAsync(conversationId);
            _logger.LogDebug("Retrieved {count} messages from database for conversation: {conversationId}", 
                messages.Count, conversationId);
            
            // Try to cache the messages for future use if Redis is available
            if (IsRedisAvailable() && messages.Any())
            {
                try
                {
                    await _cacheService.CacheMessagesAsync(messages, conversationId);
                    _logger.LogDebug("Cached {count} messages for conversation: {conversationId}", 
                        messages.Count, conversationId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache messages for conversation: {conversationId}", 
                        conversationId);
                }
            }
            
            return messages;
        }

        public async Task<PaginatedResult<Message>> GetPaginatedMessagesAsync(Guid conversationId, MessageQueryOptions options)
        {
            try
            {
                // Try to get all messages from cache if Redis is available
                if (IsRedisAvailable())
                {
                    try
                    {
                        var cachedMessages = await _cacheService.GetConversationMessagesAsync(conversationId);
                        
                        if (cachedMessages != null)
                        {
                            _logger.LogDebug("Retrieved messages from cache for conversation {conversationId}", conversationId);
                            
                            // Apply filtering and pagination in memory
                            var filteredMessages = cachedMessages;
                            
                            // Apply filters
                            if (options.FromDate.HasValue)
                            {
                                filteredMessages = filteredMessages.Where(m => m.Timestamp >= options.FromDate.Value).ToList();
                            }
                            
                            if (options.ToDate.HasValue)
                            {
                                filteredMessages = filteredMessages.Where(m => m.Timestamp <= options.ToDate.Value).ToList();
                            }
                            
                            if (!string.IsNullOrEmpty(options.Role))
                            {
                                filteredMessages = filteredMessages.Where(m => m.Role == options.Role).ToList();
                            }
                            
                            if (!string.IsNullOrEmpty(options.SearchText))
                            {
                                filteredMessages = filteredMessages.Where(m => 
                                    m.Content.Contains(options.SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
                            }
                            
                            // Apply sorting
                            if (options.OrderBy?.ToLower() == "desc")
                            {
                                filteredMessages = filteredMessages.OrderByDescending(m => m.Timestamp).ToList();
                            }
                            else
                            {
                                filteredMessages = filteredMessages.OrderBy(m => m.Timestamp).ToList();
                            }
                            
                            // Get total count after filtering
                            int totalCount = filteredMessages.Count;
                            
                            // Apply pagination
                            var pagedMessages = filteredMessages
                                .Skip(options.Skip)
                                .Take(options.Take)
                                .ToList();
                            
                            // Calculate pagination values
                            int pageSize = options.Take;
                            int currentPage = (options.Skip / pageSize) + 1;
                            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                            
                            return new PaginatedResult<Message>
                            {
                                Items = pagedMessages,
                                TotalCount = totalCount,
                                CurrentPage = currentPage,
                                TotalPages = totalPages,
                                PageSize = pageSize
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to retrieve paginated messages from cache: {conversationId}. Falling back to database.", conversationId);
                    }
                }
                
                // If not in cache or Redis is unavailable, get from database with pagination
                _logger.LogDebug("Getting paginated messages from database: {conversationId}", conversationId);
                return await _chatRepository.GetPaginatedMessagesAsync(conversationId, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated messages for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<ConversationWithMessagesDto> GetConversationWithMessagesAsync(Guid conversationId, MessageQueryOptions options)
        {
            try
            {
                // Get the conversation
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    return null;
                }
                
                // Get paginated messages
                var messages = await GetPaginatedMessagesAsync(conversationId, options);
                
                // Create the result DTO
                return new ConversationWithMessagesDto
                {
                    Id = conversation.Id,
                    UserId = conversation.UserId,
                    Title = conversation.Title,
                    CreatedAt = conversation.CreatedAt,
                    UpdatedAt = conversation.UpdatedAt,
                    Messages = messages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation with messages for {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<List<ChatSummaryDto>> GetChatSummariesAsync(Guid userId)
        {
            try
            {
                // Get all conversations for the user
                var conversations = await GetUserConversationsAsync(userId);
                var summaries = new List<ChatSummaryDto>();
                
                foreach (var conversation in conversations)
                {
                    // Get the summary for each conversation
                    ChatSummaryDto summary;

                    // Try to get summary from database
                    summary = await _chatRepository.GetConversationSummaryAsync(conversation.Id);
                    if (summary != null)
                    {
                        summaries.Add(summary);
                    }
                }
                
                // Sort by last message time (newest first)
                return summaries.OrderByDescending(s => s.LastMessageTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat summaries for user {UserId}", userId);
                throw;
            }
        }
    }
} 