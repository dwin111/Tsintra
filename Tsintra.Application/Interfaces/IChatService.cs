using Tsintra.Domain.Models;

namespace Tsintra.Application.Interfaces;

public interface IChatService
{
    Task<List<Conversation>> GetUserConversationsAsync(Guid userId);
    Task<Conversation> GetConversationAsync(Guid conversationId);
    Task<Conversation> CreateConversationAsync(Guid userId, string title);
    Task<bool> UpdateConversationTitleAsync(Guid conversationId, string newTitle);
    Task<bool> DeleteConversationAsync(Guid conversationId);
    
    Task<Message> AddUserMessageAsync(Guid conversationId, string content);
    Task<Message> AddAssistantMessageAsync(Guid conversationId, string content);
    Task<List<Message>> GetConversationHistoryAsync(Guid conversationId);
    
    // Enhanced chat retrieval methods
    Task<PaginatedResult<Message>> GetPaginatedMessagesAsync(Guid conversationId, MessageQueryOptions options);
    Task<ConversationWithMessagesDto> GetConversationWithMessagesAsync(Guid conversationId, MessageQueryOptions options);
    Task<List<ChatSummaryDto>> GetChatSummariesAsync(Guid userId);
} 