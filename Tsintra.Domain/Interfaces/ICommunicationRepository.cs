using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface ICommunicationRepository
    {
        Task<IEnumerable<Communication>> GetAllAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<Communication> GetByIdAsync(Guid id);
        Task<Communication> AddAsync(Communication communication);
        Task<bool> UpdateAsync(Communication communication);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<Communication>> GetByCustomerIdAsync(Guid customerId);
        Task<IEnumerable<Communication>> GetByTypeAsync(CommunicationType type);
        Task<IEnumerable<Communication>> GetRecentAsync(int count);
    }
} 