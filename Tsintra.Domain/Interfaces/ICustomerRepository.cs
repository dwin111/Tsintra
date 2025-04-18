using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(Guid id);
        Task<IEnumerable<Customer>> GetAllAsync();
        Task<Customer> AddAsync(Customer customer);
        Task<bool> UpdateAsync(Customer customer);
        Task<bool> DeleteAsync(Guid id);
        
        // Marketplace specific
        Task<Customer?> GetByMarketplaceIdAsync(string marketplaceId, string marketplaceType);
        Task<IEnumerable<Customer>> GetByMarketplaceTypeAsync(string marketplaceType);
        
        // Search and filter
        Task<Customer?> GetByEmailAsync(string email);
        Task<Customer?> GetByPhoneAsync(string phone);
        Task<IEnumerable<Customer>> SearchAsync(string searchTerm);
        Task<IEnumerable<Customer>> GetByTagAsync(string tag);
        Task<IEnumerable<Customer>> GetByCustomerTypeAsync(string customerType);
        
        // Customer value
        Task<IEnumerable<Customer>> GetTopSpendersAsync(int count);
        Task<IEnumerable<Customer>> GetFrequentBuyersAsync(int count);
        Task<IEnumerable<Customer>> GetInactiveCustomersAsync(TimeSpan inactivityPeriod);
        
        // Statistics
        Task<int> GetCustomerCountAsync();
        Task<decimal> GetAverageOrderValueAsync(Guid customerId);
        Task<int> GetOrderCountAsync(Guid customerId);
        Task<decimal> GetTotalSpentAsync(Guid customerId);
        
        // Customer segments
        Task<IEnumerable<string>> GetAllTagsAsync();
        Task<IEnumerable<string>> GetAllCustomerTypesAsync();
    }
} 