using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces;

public interface IAgentMemoryService
{
    Task<AgentMemory> GetMemoryAsync(Guid userId, string conversationId);
    Task SaveMemoryAsync(AgentMemory memory);
    Task DeleteMemoryAsync(Guid userId, string conversationId);
    Task<bool> ExistsAsync(Guid userId, string conversationId);
    Task<string> GetMemoryPromptContextAsync(Guid userId, string conversationId);
} 