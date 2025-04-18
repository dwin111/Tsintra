using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces;

public interface IConversationRepository
{
    // Conversation operations
    Task<Conversation> GetByIdAsync(Guid id);
    Task<List<Conversation>> GetByUserIdAsync(Guid userId);
    Task<Conversation> CreateAsync(Conversation conversation);
    Task UpdateAsync(Conversation conversation);
    Task DeleteAsync(Guid id);
    Task<List<Conversation>> GetAllAsync(CancellationToken cancellationToken = default);
    
    // Message operations
    Task<Message> AddMessageAsync(Message message);
    Task<List<Message>> GetConversationMessagesAsync(Guid conversationId);
    
    // Enhanced message operations with filtering and pagination
    Task<PaginatedResult<Message>> GetPaginatedMessagesAsync(Guid conversationId, MessageQueryOptions options);
    Task<int> GetMessageCountAsync(Guid conversationId, MessageQueryOptions options = null);
    Task<ChatSummaryDto> GetConversationSummaryAsync(Guid conversationId);
} 