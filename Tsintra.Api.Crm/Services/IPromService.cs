using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Api.Crm.Models;
using Tsintra.Domain.Models;
using Prom = Tsintra.Api.Crm.Models.Prom;

namespace Tsintra.Api.Crm.Services
{
    /// <summary>
    /// Напрямок синхронізації продуктів
    /// </summary>
    public enum SyncDirection
    {
        /// <summary>
        /// З Prom.ua в базу даних
        /// </summary>
        Import,
        
        /// <summary>
        /// З бази даних на Prom.ua
        /// </summary>
        Export,
        
        /// <summary>
        /// Двостороння синхронізація
        /// </summary>
        Both
    }

    /// <summary>
    /// Interface for Prom.ua integration service
    /// </summary>
    public interface IPromService
    {
        /// <summary>
        /// Get all products from Prom.ua
        /// </summary>
        Task<IEnumerable<Product>> GetProductsAsync();
        
        /// <summary>
        /// Get product details from Prom.ua by its marketplace ID
        /// </summary>
        Task<Product> GetProductByMarketplaceIdAsync(string marketplaceProductId);
        
        /// <summary>
        /// Create a new product in Prom.ua
        /// </summary>
        Task<Product> CreateProductAsync(Product product);
        
        /// <summary>
        /// Update an existing product in Prom.ua
        /// </summary>
        Task<bool> UpdateProductAsync(Product product);
        
        /// <summary>
        /// Delete a product from Prom.ua
        /// </summary>
        Task<bool> DeleteProductAsync(string marketplaceProductId);
        
        /// <summary>
        /// Import products from Prom.ua to local CRM database
        /// </summary>
        Task<int> ImportProductsAsync();
        
        /// <summary>
        /// Export products from local CRM database to Prom.ua
        /// </summary>
        Task<int> ExportProductsAsync(IEnumerable<Guid> productIds = null);
        
        /// <summary>
        /// Sync product inventory between CRM and Prom.ua
        /// </summary>
        Task<bool> SyncProductInventoryAsync(string marketplaceProductId, int quantity);
        
        /// <summary>
        /// Get orders from Prom.ua with optional date filtering
        /// </summary>
        Task<IEnumerable<Order>> GetOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        /// <summary>
        /// Import orders from Prom.ua to local CRM database
        /// </summary>
        Task<int> ImportOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        /// <summary>
        /// Update order status in Prom.ua
        /// </summary>
        Task<bool> UpdateOrderStatusAsync(string marketplaceOrderId, OrderStatus status);
        
        /// <summary>
        /// Publish a product to Prom.ua by its ID
        /// </summary>
        Task<bool> PublishProductByIdAsync(Guid productId);
        
        /// <summary>
        /// Зберігає продукти в базу даних
        /// </summary>
        /// <param name="products">Список продуктів для збереження</param>
        /// <returns>Кількість збережених продуктів</returns>
        Task<int> SaveProductsToDatabaseAsync(IEnumerable<Product> products);
        
        /// <summary>
        /// Зберігає продукти з файлу в базу даних
        /// </summary>
        /// <param name="filePath">Шлях до файлу з продуктами (CSV, Excel, тощо)</param>
        /// <returns>Кількість збережених продуктів</returns>
        Task<int> SaveProductsFromFileAsync(string filePath);
        
        /// <summary>
        /// Синхронізує товари між Prom.ua і базою даних
        /// </summary>
        /// <param name="syncDirection">Напрямок синхронізації</param>
        /// <param name="productIds">Опціональний список ID товарів для синхронізації</param>
        /// <returns>Інформація про результат синхронізації</returns>
        Task<(int Imported, int Exported, int Failed)> SyncProductsWithDatabaseAsync(
            SyncDirection syncDirection = SyncDirection.Both, 
            IEnumerable<Guid> productIds = null);
            
        /// <summary>
        /// Отримує список груп товарів з Prom.ua
        /// </summary>
        /// <returns>Список груп товарів</returns>
        Task<IEnumerable<Prom.PromGroup>> GetGroupsAsync();
        
        /// <summary>
        /// Отримує групу товарів з Prom.ua за ID
        /// </summary>
        /// <param name="groupId">ID групи товарів</param>
        /// <param name="language">Мова перекладу (необов'язковий параметр)</param>
        /// <returns>Група товарів</returns>
        Task<Prom.PromGroup> GetGroupByIdAsync(string groupId, string language = null);
        
        /// <summary>
        /// Метод для отримання сирих даних замовлень з Prom.ua
        /// </summary>
        /// <param name="startDate">Дата початку періоду</param>
        /// <param name="endDate">Дата кінця періоду</param>
        /// <returns>Список замовлень у форматі API Prom.ua</returns>
        Task<IEnumerable<Prom.PromOrder>> GetRawOrdersAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        /// <summary>
        /// Отримує замовлення з Prom.ua за ID
        /// </summary>
        /// <param name="orderId">ID замовлення</param>
        /// <returns>Замовлення у форматі домену</returns>
        Task<Order> GetOrderByIdAsync(string orderId);
        
        /// <summary>
        /// Отримує сирі дані замовлення з Prom.ua за ID без конвертації у доменну модель
        /// </summary>
        /// <param name="orderId">ID замовлення</param>
        /// <returns>Замовлення у форматі API Prom.ua</returns>
        Task<Prom.PromOrder> GetRawOrderByIdAsync(string orderId);
    }
} 