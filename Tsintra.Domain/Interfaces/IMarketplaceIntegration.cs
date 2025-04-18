using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface IMarketplaceIntegration
    {
        string MarketplaceName { get; }
        
        // Product operations
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<Product> GetProductAsync(string marketplaceProductId);
        Task<bool> UpdateProductAsync(Product product);
        Task<bool> CreateProductAsync(Product product);
        Task<bool> DeleteProductAsync(string marketplaceProductId);
        
        // Order operations
        Task<IEnumerable<Order>> GetOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<Order> GetOrderAsync(string marketplaceOrderId);
        Task<bool> UpdateOrderStatusAsync(string marketplaceOrderId, OrderStatus status);
        
        // Customer operations
        Task<Customer> GetCustomerAsync(string marketplaceCustomerId);
        Task<IEnumerable<Customer>> GetCustomersAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        // Inventory operations
        Task<bool> UpdateStockQuantityAsync(string marketplaceProductId, int quantity);
        Task<int> GetStockQuantityAsync(string marketplaceProductId);
        
        // Authentication and configuration
        Task<bool> ValidateCredentialsAsync();
        Task<bool> ConfigureAsync(Dictionary<string, string> settings);
    }
} 