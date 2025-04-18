using Tsintra.Domain.Models;
using Tsintra.Domain.Enums;

namespace Tsintra.Domain.Interfaces;

public interface IChatRepository
{
    // Conversation operations
    Task<Conversation> GetConversationAsync(Guid id);
    Task<List<Conversation>> GetUserConversationsAsync(Guid userId);
    Task<Conversation> CreateConversationAsync(Guid userId, string title);
    Task<bool> UpdateConversationTitleAsync(Guid conversationId, string newTitle);
    Task<bool> UpdateConversationTimestampAsync(Guid conversationId);
    Task<bool> DeleteConversationAsync(Guid id);
    
    // Message operations
    Task<Message> AddMessageAsync(Guid conversationId, string content, MessageRole role);
    Task<List<Message>> GetConversationMessagesAsync(Guid conversationId);
    
    // Enhanced message operations with filtering and pagination
    Task<PaginatedResult<Message>> GetPaginatedMessagesAsync(Guid conversationId, MessageQueryOptions options);
    Task<int> GetMessageCountAsync(Guid conversationId, MessageQueryOptions options = null);
    Task<ChatSummaryDto> GetConversationSummaryAsync(Guid conversationId);
} 