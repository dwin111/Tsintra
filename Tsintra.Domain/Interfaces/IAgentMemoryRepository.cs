using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces;

public interface IAgentMemoryRepository
{
    Task<AgentMemory> GetByConversationIdAsync(Guid userId, string conversationId);
    Task<IEnumerable<AgentMemory>> GetAllForUserAsync(Guid userId);
    Task<IEnumerable<AgentMemory>> GetExpiredMemoriesAsync(DateTime currentTime);
    Task<IEnumerable<AgentMemory>> GetAllMemoriesAsync();
    Task<AgentMemory> CreateAsync(AgentMemory memory);
    Task UpdateAsync(AgentMemory memory);
    Task DeleteAsync(Guid userId, string conversationId);
} 