using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(Guid id);
        Task<IEnumerable<Order>> GetAllAsync();
        Task<Order> AddAsync(Order order);
        Task<bool> UpdateAsync(Order order);
        Task<bool> DeleteAsync(Guid id);
        
        // Marketplace specific
        Task<Order?> GetByMarketplaceIdAsync(string marketplaceId, string marketplaceType);
        Task<IEnumerable<Order>> GetByMarketplaceTypeAsync(string marketplaceType);
        
        // Customer related
        Task<IEnumerable<Order>> GetByCustomerIdAsync(Guid customerId);
        Task<IEnumerable<Order>> GetByCustomerEmailAsync(string email);
        
        // Status related
        Task<IEnumerable<Order>> GetByStatusAsync(string status);
        Task<bool> UpdateStatusAsync(Guid orderId, string status, string? notes = null, string? changedBy = null);
        
        // Date range
        Task<IEnumerable<Order>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
        
        // Search
        Task<IEnumerable<Order>> SearchAsync(string searchTerm);
        
        // Statistics
        Task<decimal> GetTotalRevenueAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<int> GetOrderCountAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<decimal> GetAverageOrderValueAsync(DateTime? startDate = null, DateTime? endDate = null);
    }
} 