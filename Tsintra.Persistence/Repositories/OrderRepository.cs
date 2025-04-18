using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<OrderRepository> _logger;

        public OrderRepository(IConfiguration configuration, ILogger<OrderRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        private async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }

        private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters);
        }

        private async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, parameters);
        }

        private async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryMultipleAsync(sql, parameters);
        }

        private async Task<T> ExecuteScalarAsync<T>(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<T>(sql, parameters);
        }

        public async Task<Order?> GetByIdAsync(Guid id)
        {
            try
            {
                const string sql = @"
                    SELECT 
                        o.*,
                        oi.Id as OrderItemId, oi.ProductId, oi.Quantity, oi.Price, oi.TotalPrice, oi.Currency,
                        osh.Id as StatusHistoryId, osh.Status as HistoryStatus, osh.ChangedAt, osh.Notes, osh.ChangedBy
                    FROM Orders o
                    LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                    LEFT JOIN OrderStatusHistory osh ON o.Id = osh.OrderId
                    WHERE o.Id = @Id";

                using var connection = CreateConnection();
                var results = await connection.QueryAsync<dynamic>(sql, new { Id = id });
                
                if (!results.Any()) return null;

                var firstRow = results.First();
                var order = new Order
                {
                    Id = firstRow.Id,
                    ExternalId = firstRow.ExternalId,
                    MarketplaceType = firstRow.MarketplaceType,
                    MarketplaceId = firstRow.MarketplaceId,
                    MarketplaceName = firstRow.MarketplaceName,
                    MarketplaceOrderId = firstRow.MarketplaceOrderId,
                    CustomerId = firstRow.CustomerId,
                    Status = firstRow.Status,
                    TotalAmount = firstRow.TotalAmount,
                    Currency = firstRow.Currency,
                    CreatedAt = firstRow.CreatedAt,
                    UpdatedAt = firstRow.UpdatedAt,
                    CompletedAt = firstRow.CompletedAt,
                    ShippingMethod = firstRow.ShippingMethod,
                    ShippingAddress = firstRow.ShippingAddress,
                    ShippingCity = firstRow.ShippingCity,
                    ShippingRegion = firstRow.ShippingRegion,
                    ShippingCountry = firstRow.ShippingCountry,
                    ShippingPostalCode = firstRow.ShippingPostalCode,
                    ShippingPhone = firstRow.ShippingPhone,
                    PaymentMethod = firstRow.PaymentMethod,
                    PaymentStatus = firstRow.PaymentStatus,
                    PaymentDate = firstRow.PaymentDate,
                    TransactionId = firstRow.TransactionId,
                    TrackingNumber = firstRow.TrackingNumber,
                    TrackingUrl = firstRow.TrackingUrl,
                    DeliveryService = firstRow.DeliveryService,
                    Notes = firstRow.Notes
                };

                // Збираємо унікальні елементи замовлення
                order.Items = results
                    .Where(r => r.OrderItemId != null)
                    .Select(r => new OrderItem
                    {
                        Id = r.OrderItemId,
                        OrderId = r.Id,
                        ProductId = r.ProductId,
                        Quantity = r.Quantity,
                        UnitPrice = r.Price,
                        TotalPrice = r.TotalPrice,
                        Currency = r.Currency
                    })
                    .Distinct()
                    .ToList();

                // Збираємо унікальну історію статусів
                order.StatusHistory = results
                    .Where(r => r.StatusHistoryId != null)
                    .Select(r => new OrderStatusHistory
                    {
                        Id = r.StatusHistoryId,
                        OrderId = r.Id,
                        Status = r.HistoryStatus,
                        ChangedAt = r.ChangedAt,
                        Notes = r.Notes,
                        ChangedBy = r.ChangedBy
                    })
                    .Distinct()
                    .ToList();

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order by id {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetAllAsync()
        {
            try
            {
                const string sql = "SELECT * FROM Orders";
                return await QueryAsync<Order>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all orders");
                throw;
            }
        }

        public async Task<Order> AddAsync(Order order)
        {
            try
            {
                const string sql = @"
                    INSERT INTO Orders (Id, ExternalId, MarketplaceType, MarketplaceId, MarketplaceName, 
                        MarketplaceOrderId, CustomerId, Status, TotalAmount, Currency, CreatedAt, UpdatedAt, 
                        CompletedAt, ShippingMethod, ShippingAddress, ShippingCity, ShippingRegion, 
                        ShippingCountry, ShippingPostalCode, ShippingPhone, PaymentMethod, PaymentStatus, 
                        PaymentDate, TransactionId, TrackingNumber, TrackingUrl, DeliveryService, Notes)
                    VALUES (@Id, @ExternalId, @MarketplaceType, @MarketplaceId, @MarketplaceName, 
                        @MarketplaceOrderId, @CustomerId, @Status, @TotalAmount, @Currency, @CreatedAt, @UpdatedAt, 
                        @CompletedAt, @ShippingMethod, @ShippingAddress, @ShippingCity, @ShippingRegion, 
                        @ShippingCountry, @ShippingPostalCode, @ShippingPhone, @PaymentMethod, @PaymentStatus, 
                        @PaymentDate, @TransactionId, @TrackingNumber, @TrackingUrl, @DeliveryService, @Notes)
                    RETURNING *;";

                order.CreatedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;

                var result = await QueryFirstOrDefaultAsync<Order>(sql, order);
                
                // Save order items and status history
                await SaveOrderItems(order);
                await SaveOrderStatusHistory(order);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding order");
                throw;
            }
        }

        public async Task<bool> UpdateAsync(Order order)
        {
            try
            {
                const string sql = @"
                    UPDATE Orders 
                    SET ExternalId = @ExternalId, MarketplaceType = @MarketplaceType, 
                        MarketplaceId = @MarketplaceId, MarketplaceName = @MarketplaceName, 
                        MarketplaceOrderId = @MarketplaceOrderId, CustomerId = @CustomerId, 
                        Status = @Status, TotalAmount = @TotalAmount, Currency = @Currency, 
                        UpdatedAt = @UpdatedAt, CompletedAt = @CompletedAt, 
                        ShippingMethod = @ShippingMethod, ShippingAddress = @ShippingAddress, 
                        ShippingCity = @ShippingCity, ShippingRegion = @ShippingRegion, 
                        ShippingCountry = @ShippingCountry, ShippingPostalCode = @ShippingPostalCode, 
                        ShippingPhone = @ShippingPhone, PaymentMethod = @PaymentMethod, 
                        PaymentStatus = @PaymentStatus, PaymentDate = @PaymentDate, 
                        TransactionId = @TransactionId, TrackingNumber = @TrackingNumber, 
                        TrackingUrl = @TrackingUrl, DeliveryService = @DeliveryService, Notes = @Notes
                    WHERE Id = @Id;";

                order.UpdatedAt = DateTime.UtcNow;

                var result = await ExecuteAsync(sql, order) > 0;
                
                if (result)
                {
                    // Update order items and status history
                    await SaveOrderItems(order);
                    await SaveOrderStatusHistory(order);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order {Id}", order.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                const string sql = "DELETE FROM Orders WHERE Id = @Id";
                return await ExecuteAsync(sql, new { Id = id }) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order {Id}", id);
                throw;
            }
        }

        public async Task<Order?> GetByMarketplaceIdAsync(string marketplaceId, string marketplaceType)
        {
            try
            {
                const string sql = @"
                    SELECT * FROM Orders 
                    WHERE MarketplaceId = @MarketplaceId AND MarketplaceType = @MarketplaceType;";

                return await QueryFirstOrDefaultAsync<Order>(sql, new { MarketplaceId = marketplaceId, MarketplaceType = marketplaceType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order by marketplace id {MarketplaceId}", marketplaceId);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetByMarketplaceTypeAsync(string marketplaceType)
        {
            try
            {
                const string sql = "SELECT * FROM Orders WHERE MarketplaceType = @MarketplaceType";
                return await QueryAsync<Order>(sql, new { MarketplaceType = marketplaceType });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by marketplace type {MarketplaceType}", marketplaceType);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetByCustomerIdAsync(Guid customerId)
        {
            try
            {
                const string sql = "SELECT * FROM Orders WHERE CustomerId = @CustomerId";
                return await QueryAsync<Order>(sql, new { CustomerId = customerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by customer id {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetByCustomerEmailAsync(string email)
        {
            try
            {
                const string sql = @"
                    SELECT o.* FROM Orders o
                    JOIN Customers c ON o.CustomerId = c.Id
                    WHERE c.Email = @Email;";

                return await QueryAsync<Order>(sql, new { Email = email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by customer email {Email}", email);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetByStatusAsync(string status)
        {
            try
            {
                const string sql = "SELECT * FROM Orders WHERE Status = @Status";
                return await QueryAsync<Order>(sql, new { Status = status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by status {Status}", status);
                throw;
            }
        }

        public async Task<bool> UpdateStatusAsync(Guid orderId, string status, string? notes = null, string? changedBy = null)
        {
            try
            {
                const string sql = @"
                    UPDATE Orders 
                    SET Status = @Status, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id;
                    
                    INSERT INTO OrderStatusHistory (Id, OrderId, Status, ChangedAt, Notes, ChangedBy)
                    VALUES (@HistoryId, @Id, @Status, @ChangedAt, @Notes, @ChangedBy);";

                var parameters = new
                {
                    Id = orderId,
                    Status = status,
                    UpdatedAt = DateTime.UtcNow,
                    HistoryId = Guid.NewGuid(),
                    ChangedAt = DateTime.UtcNow,
                    Notes = notes,
                    ChangedBy = changedBy
                };

                return await ExecuteAsync(sql, parameters) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status {OrderId}", orderId);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                const string sql = @"
                    SELECT * FROM Orders 
                    WHERE CreatedAt BETWEEN @StartDate AND @EndDate
                    ORDER BY CreatedAt DESC";

                return await QueryAsync<Order>(sql, new { StartDate = startDate, EndDate = endDate });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by date range: {StartDate} - {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            // Використовуємо існуючий метод для отримання замовлень за датами
            return await GetByDateRangeAsync(startDate, endDate);
        }

        public async Task<IEnumerable<Order>> SearchAsync(string searchTerm)
        {
            try
            {
                const string sql = @"
                    SELECT DISTINCT o.* FROM Orders o
                    LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                    WHERE o.ExternalId ILIKE @SearchTerm
                    OR o.MarketplaceOrderId ILIKE @SearchTerm
                    OR o.Notes ILIKE @SearchTerm
                    OR oi.ProductName ILIKE @SearchTerm;";

                return await QueryAsync<Order>(sql, new { SearchTerm = $"%{searchTerm}%" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching orders with term {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<decimal> GetTotalRevenueAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var sql = "SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders";
                if (startDate.HasValue && endDate.HasValue)
                {
                    sql += " WHERE CreatedAt BETWEEN @StartDate AND @EndDate";
                    return await ExecuteScalarAsync<decimal>(sql, new { StartDate = startDate, EndDate = endDate });
                }
                return await ExecuteScalarAsync<decimal>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total revenue");
                throw;
            }
        }

        public async Task<int> GetOrderCountAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM Orders";
                if (startDate.HasValue && endDate.HasValue)
                {
                    sql += " WHERE CreatedAt BETWEEN @StartDate AND @EndDate";
                    return await ExecuteScalarAsync<int>(sql, new { StartDate = startDate, EndDate = endDate });
                }
                return await ExecuteScalarAsync<int>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order count");
                throw;
            }
        }

        public async Task<decimal> GetAverageOrderValueAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var sql = "SELECT COALESCE(AVG(TotalAmount), 0) FROM Orders";
                if (startDate.HasValue && endDate.HasValue)
                {
                    sql += " WHERE CreatedAt BETWEEN @StartDate AND @EndDate";
                    return await ExecuteScalarAsync<decimal>(sql, new { StartDate = startDate, EndDate = endDate });
                }
                return await ExecuteScalarAsync<decimal>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting average order value");
                throw;
            }
        }

        private async Task SaveOrderItems(Order order)
        {
            if (order.Items == null) return;

            const string deleteSql = "DELETE FROM OrderItems WHERE OrderId = @OrderId";
            await ExecuteAsync(deleteSql, new { OrderId = order.Id });

            const string insertSql = @"
                INSERT INTO OrderItems (Id, OrderId, ProductId, Quantity, UnitPrice, TotalPrice)
                VALUES (@Id, @OrderId, @ProductId, @Quantity, @UnitPrice, @TotalPrice);";

            foreach (var item in order.Items)
            {
                await ExecuteAsync(insertSql, new
                {
                    Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                    OrderId = order.Id,
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    item.TotalPrice
                });
            }
        }

        private async Task SaveOrderStatusHistory(Order order)
        {
            if (order.StatusHistory == null) return;

            const string deleteSql = "DELETE FROM OrderStatusHistory WHERE OrderId = @OrderId";
            await ExecuteAsync(deleteSql, new { OrderId = order.Id });

            const string insertSql = @"
                INSERT INTO OrderStatusHistory (Id, OrderId, Status, ChangedAt, Notes, ChangedBy)
                VALUES (@Id, @OrderId, @Status, @ChangedAt, @Notes, @ChangedBy);";

            foreach (var history in order.StatusHistory)
            {
                await ExecuteAsync(insertSql, new
                {
                    Id = history.Id == Guid.Empty ? Guid.NewGuid() : history.Id,
                    OrderId = order.Id,
                    history.Status,
                    history.ChangedAt,
                    history.Notes,
                    history.ChangedBy
                });
            }
        }
    }
} 