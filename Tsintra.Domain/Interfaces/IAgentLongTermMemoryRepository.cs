using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces;

public interface IAgentLongTermMemoryRepository
{
    Task<AgentLongTermMemory> GetByKeyAsync(Guid userId, string key);
    Task<IEnumerable<AgentLongTermMemory>> GetAllForUserAsync(Guid userId);
    Task<IEnumerable<AgentLongTermMemory>> GetByUserAndCategoryAsync(Guid userId, string category);
    Task<IEnumerable<AgentLongTermMemory>> GetExpiredMemoriesAsync(DateTime currentTime);
    Task<IEnumerable<AgentLongTermMemory>> GetAllMemoriesAsync();
    Task<AgentLongTermMemory> CreateAsync(AgentLongTermMemory memory);
    Task UpdateAsync(AgentLongTermMemory memory);
    Task DeleteAsync(Guid userId, string key);
    Task UpdateLastAccessedAsync(Guid userId, string key);
    Task<IEnumerable<AgentLongTermMemory>> SearchByContentAsync(Guid userId, string searchTerm);
} 