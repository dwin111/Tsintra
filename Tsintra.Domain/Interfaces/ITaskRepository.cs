using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface ITaskRepository
    {
        Task<IEnumerable<CrmTask>> GetAllAsync();
        Task<CrmTask> GetByIdAsync(Guid id);
        Task<CrmTask> AddAsync(CrmTask task);
        Task<bool> UpdateAsync(CrmTask task);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<CrmTask>> GetTasksByStatusAsync(Tsintra.Domain.Models.TaskStatus status);
        Task<IEnumerable<CrmTask>> GetTasksByUserAsync(Guid userId);
        Task<IEnumerable<CrmTask>> GetTasksByCustomerAsync(Guid customerId);
        Task<bool> UpdateTaskStatusAsync(Guid id, Tsintra.Domain.Models.TaskStatus status);
        Task<IEnumerable<CrmTask>> GetTasksDueTodayAsync();
        Task<IEnumerable<CrmTask>> GetOverdueTasksAsync();
    }
} 