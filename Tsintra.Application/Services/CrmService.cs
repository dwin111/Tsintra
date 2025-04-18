using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tsintra.Application.Interfaces;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.Domain.Models.NovaPost;

namespace Tsintra.Application.Services
{
    public class CrmService : ICrmService
    {
        private readonly IMarketplaceIntegration _marketplaceIntegration;
        private readonly ICustomerRepository _customerRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly INovaPoshtaService _novaPoshtaService;

        public CrmService(
            IMarketplaceIntegration marketplaceIntegration,
            ICustomerRepository customerRepository,
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            INovaPoshtaService novaPoshtaService)
        {
            _marketplaceIntegration = marketplaceIntegration;
            _customerRepository = customerRepository;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _novaPoshtaService = novaPoshtaService;
        }

        public async Task<Customer> GetCustomerAsync(Guid customerId)
        {
            return await _customerRepository.GetByIdAsync(customerId);
        }

        public async Task<IEnumerable<Customer>> GetCustomersAsync()
        {
            return await _customerRepository.GetAllAsync();
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            customer.CreatedAt = DateTime.UtcNow;
            return await _customerRepository.AddAsync(customer);
        }

        public async Task<bool> UpdateCustomerAsync(Customer customer)
        {
            return await _customerRepository.UpdateAsync(customer);
        }

        public async Task<bool> DeleteCustomerAsync(Guid customerId)
        {
            return await _customerRepository.DeleteAsync(customerId);
        }

        public async Task<Order> GetOrderAsync(Guid orderId)
        {
            return await _orderRepository.GetByIdAsync(orderId);
        }

        public async Task<IEnumerable<Order>> GetOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                return await _orderRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
            }
            return await _orderRepository.GetAllAsync();
        }

        public async Task<IEnumerable<Order>> GetCustomerOrdersAsync(Guid customerId)
        {
            return await _orderRepository.GetByCustomerIdAsync(customerId);
        }

        public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return false;

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            return await _orderRepository.UpdateAsync(order);
        }

        public async Task<Product> GetProductAsync(Guid productId)
        {
            return await _productRepository.GetByIdAsync(productId);
        }

        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            return await _productRepository.GetAllAsync();
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            product.CreatedAt = DateTime.UtcNow;
            var id = await _productRepository.CreateAsync(product);
            return await _productRepository.GetByIdAsync(id);
        }

        public async Task<bool> UpdateProductAsync(Product product)
        {
            product.UpdatedAt = DateTime.UtcNow;
            await _productRepository.UpdateAsync(product);
            return true;
        }

        public async Task<bool> DeleteProductAsync(Guid productId)
        {
            await _productRepository.DeleteAsync(productId);
            return true;
        }

        public async Task<bool> SyncMarketplaceOrdersAsync(string marketplaceName)
        {
            var orders = await _marketplaceIntegration.GetOrdersAsync();
            foreach (var order in orders)
            {
                var existingOrder = await _orderRepository.GetByMarketplaceIdAsync(order.MarketplaceOrderId, marketplaceName);
                if (existingOrder == null)
                {
                    await _orderRepository.AddAsync(order);
                }
                else
                {
                    await _orderRepository.UpdateAsync(order);
                }
            }
            return true;
        }

        public async Task<bool> SyncMarketplaceProductsAsync(string marketplaceName)
        {
            var products = await _marketplaceIntegration.GetProductsAsync();
            foreach (var product in products)
            {
                var existingProduct = await _productRepository.GetByMarketplaceIdAsync(product.MarketplaceMappings[marketplaceName], marketplaceName);
                if (existingProduct == null)
                {
                    await _productRepository.CreateAsync(product);
                }
                else
                {
                    await _productRepository.UpdateAsync(product);
                }
            }
            return true;
        }

        public async Task<bool> SyncMarketplaceCustomersAsync(string marketplaceName)
        {
            var customers = await _marketplaceIntegration.GetCustomersAsync();
            foreach (var customer in customers)
            {
                var existingCustomer = await _customerRepository.GetByMarketplaceIdAsync(customer.MarketplaceIdentifiers[marketplaceName], marketplaceName);
                if (existingCustomer == null)
                {
                    await _customerRepository.AddAsync(customer);
                }
                else
                {
                    await _customerRepository.UpdateAsync(customer);
                }
            }
            return true;
        }

        public async Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var orders = await GetOrdersAsync(startDate, endDate);
            return orders.Sum(o => o.TotalAmount);
        }

        public async Task<int> GetTotalOrdersAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var orders = await GetOrdersAsync(startDate, endDate);
            return orders.Count();
        }

        public async Task<int> GetTotalCustomersAsync()
        {
            var customers = await GetCustomersAsync();
            return customers.Count();
        }

        public async Task<Dictionary<string, decimal>> GetSalesByMarketplaceAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var orders = await GetOrdersAsync(startDate, endDate);
            return orders
                .GroupBy(o => o.MarketplaceName)
                .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));
        }

        #region Nova Poshta Integration

        public async Task<bool> UpdateOrderWithTrackingAsync(Guid orderId, string trackingNumber, string deliveryService = "Nova Poshta")
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null) return false;

            order.TrackingNumber = trackingNumber;
            order.DeliveryService = deliveryService;
            order.UpdatedAt = DateTime.UtcNow;
            
            // Якщо трекінг-номер від Нової Пошти, можемо отримати інформацію про відправлення
            if (deliveryService == "Nova Poshta")
            {
                var trackingData = await _novaPoshtaService.TrackDocumentAsync(trackingNumber);
                if (trackingData != null)
                {
                    var trackingUrl = $"https://tracking.novaposhta.ua/#/uk/tracking/{trackingNumber}";
                    order.TrackingUrl = trackingUrl;
                    
                    // Оновлюємо адресу доставки, якщо вона порожня
                    if (string.IsNullOrEmpty(order.ShippingCity))
                    {
                        order.ShippingCity = trackingData.CityRecipient;
                    }
                    
                    if (string.IsNullOrEmpty(order.ShippingAddress))
                    {
                        order.ShippingAddress = trackingData.WarehouseRecipient;
                    }
                }
            }

            return await _orderRepository.UpdateAsync(order);
        }

        #endregion
    }
} 