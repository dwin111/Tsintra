using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces;

public interface IAgentConversationMemoryRepository
{
    Task<IEnumerable<AgentConversationMemory>> GetByConversationIdAsync(Guid conversationId);
    Task<AgentConversationMemory> GetByIdAsync(Guid id);
    Task<IEnumerable<AgentConversationMemory>> GetByKeyAsync(Guid conversationId, string key);
    Task<IEnumerable<AgentConversationMemory>> GetActiveMemoriesAsync(Guid conversationId);
    Task<AgentConversationMemory> AddAsync(AgentConversationMemory memory);
    Task<bool> UpdateAsync(AgentConversationMemory memory);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> DeactivateAsync(Guid id);
    Task<bool> DeactivateByKeyAsync(Guid conversationId, string key);
    Task<bool> DeactivateAllAsync(Guid conversationId);
    Task<bool> CleanupExpiredMemoriesAsync();
    Task<int> GetMemoryCountAsync(Guid conversationId);
    Task<IEnumerable<AgentConversationMemory>> GetByPriorityAsync(Guid conversationId, int minPriority = 0);
} 