using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;
using Tsintra.Domain.Models.NovaPost;

namespace Tsintra.Application.Interfaces
{
    public interface ICrmService
    {
        // Customer operations
        Task<Customer> GetCustomerAsync(Guid customerId);
        Task<IEnumerable<Customer>> GetCustomersAsync();
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task<bool> UpdateCustomerAsync(Customer customer);
        Task<bool> DeleteCustomerAsync(Guid customerId);
        
        // Order operations
        Task<Order> GetOrderAsync(Guid orderId);
        Task<IEnumerable<Order>> GetOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<Order>> GetCustomerOrdersAsync(Guid customerId);
        Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status);
        
        // Product operations
        Task<Product> GetProductAsync(Guid productId);
        Task<IEnumerable<Product>> GetProductsAsync();
        Task<Product> CreateProductAsync(Product product);
        Task<bool> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(Guid productId);
        
        // Marketplace synchronization
        Task<bool> SyncMarketplaceOrdersAsync(string marketplaceName);
        Task<bool> SyncMarketplaceProductsAsync(string marketplaceName);
        Task<bool> SyncMarketplaceCustomersAsync(string marketplaceName);
        
        // Reporting
        Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<int> GetTotalOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<int> GetTotalCustomersAsync();
        Task<Dictionary<string, decimal>> GetSalesByMarketplaceAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        // Залишаю лише один метод, який стосується оновлення замовлення (це CRM функціонал)
        Task<bool> UpdateOrderWithTrackingAsync(Guid orderId, string trackingNumber, string deliveryService = "Nova Poshta");
    }
} 