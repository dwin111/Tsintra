using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using System.Text.Json;

namespace Tsintra.Persistence.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<CustomerRepository> _logger;

        public CustomerRepository(IConfiguration configuration, ILogger<CustomerRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<Customer> GetByIdAsync(Guid id)
        {
            _logger.LogDebug("Attempting to find customer by ID: {CustomerId}", id);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.Id = @Id";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { Id = id },
                    splitOn: "Id,Id,Id,Id"
                );

                var customer = customerDictionary.Values.FirstOrDefault();
                if (customer == null)
                {
                    _logger.LogDebug("Customer not found for ID: {CustomerId}", id);
                }
                else
                {
                    _logger.LogDebug("Customer found for ID: {CustomerId}", id);
                }
                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding customer by ID: {CustomerId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            _logger.LogDebug("Attempting to get all customers");
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    splitOn: "Id,Id,Id,Id"
                );

                _logger.LogDebug("Retrieved {Count} customers", customerDictionary.Count);
                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all customers");
                throw;
            }
        }

        public async Task<Customer> AddAsync(Customer customer)
        {
            _logger.LogDebug("Attempting to add new customer");
            
            // Serialize MarketplaceSpecificData to JSON string to avoid hstore issues
            string marketplaceSpecificDataJson = null;
            if (customer.MarketplaceSpecificData != null)
            {
                marketplaceSpecificDataJson = JsonSerializer.Serialize(customer.MarketplaceSpecificData);
            }
            
            const string sql = @"
                INSERT INTO Customers (Id, Name, Email, Phone, MarketplaceIdentifiers, MarketplaceType, MarketplaceId, MarketplaceSpecificData)
                VALUES (@Id, @Name, @Email, @Phone, @MarketplaceIdentifiers, @MarketplaceType, @MarketplaceId, @MarketplaceSpecificDataJson)
                RETURNING *";

            try
            {
                using var connection = CreateConnection();
                var parameters = new
                {
                    customer.Id,
                    customer.Name,
                    customer.Email,
                    customer.Phone,
                    customer.MarketplaceIdentifiers,
                    customer.MarketplaceType,
                    customer.MarketplaceId,
                    MarketplaceSpecificDataJson = marketplaceSpecificDataJson
                };
                
                var result = await connection.QuerySingleAsync<Customer>(sql, parameters);
                _logger.LogInformation("Successfully added new customer with ID: {CustomerId}", result.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new customer");
                throw;
            }
        }

        public async Task<bool> UpdateAsync(Customer customer)
        {
            _logger.LogDebug("Attempting to update customer with ID: {CustomerId}", customer.Id);
            
            // Serialize MarketplaceSpecificData to JSON string to avoid hstore issues
            string marketplaceSpecificDataJson = null;
            if (customer.MarketplaceSpecificData != null)
            {
                marketplaceSpecificDataJson = System.Text.Json.JsonSerializer.Serialize(customer.MarketplaceSpecificData);
            }
            
            const string sql = @"
                UPDATE Customers 
                SET Name = @Name,
                    Email = @Email,
                    Phone = @Phone,
                    MarketplaceIdentifiers = @MarketplaceIdentifiers,
                    MarketplaceType = @MarketplaceType,
                    MarketplaceId = @MarketplaceId,
                    MarketplaceSpecificData = @MarketplaceSpecificDataJson
                WHERE Id = @Id";

            try
            {
                using var connection = CreateConnection();
                var parameters = new
                {
                    customer.Id,
                    customer.Name,
                    customer.Email,
                    customer.Phone,
                    customer.MarketplaceIdentifiers,
                    customer.MarketplaceType,
                    customer.MarketplaceId,
                    MarketplaceSpecificDataJson = marketplaceSpecificDataJson
                };
                
                var affected = await connection.ExecuteAsync(sql, parameters);
                _logger.LogDebug("Customer update executed for ID: {CustomerId}. Rows affected: {AffectedRows}", customer.Id, affected);
                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer with ID: {CustomerId}", customer.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            _logger.LogDebug("Attempting to delete customer with ID: {CustomerId}", id);
            const string sql = "DELETE FROM Customers WHERE Id = @Id";

            try
            {
                using var connection = CreateConnection();
                var affected = await connection.ExecuteAsync(sql, new { Id = id });
                _logger.LogDebug("Customer delete executed for ID: {CustomerId}. Rows affected: {AffectedRows}", id, affected);
                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer with ID: {CustomerId}", id);
                throw;
            }
        }

        public async Task<Customer> GetByMarketplaceIdAsync(string marketplaceId)
        {
            _logger.LogDebug("Attempting to find customer by Marketplace ID: {MarketplaceId}", marketplaceId);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.MarketplaceIdentifiers LIKE @MarketplaceId";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { MarketplaceId = $"%{marketplaceId}%" },
                    splitOn: "Id,Id,Id,Id"
                );

                var customer = customerDictionary.Values.FirstOrDefault();
                if (customer == null)
                {
                    _logger.LogDebug("Customer not found for Marketplace ID: {MarketplaceId}", marketplaceId);
                }
                else
                {
                    _logger.LogDebug("Customer found for Marketplace ID: {MarketplaceId}", marketplaceId);
                }
                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding customer by Marketplace ID: {MarketplaceId}", marketplaceId);
                throw;
            }
        }

        public async Task<Customer?> GetByMarketplaceIdAsync(string marketplaceId, string marketplaceType)
        {
            _logger.LogDebug("Attempting to find customer by Marketplace ID: {MarketplaceId} and type: {MarketplaceType}", 
                marketplaceId, marketplaceType);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.MarketplaceId = @MarketplaceId AND c.MarketplaceType = @MarketplaceType";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { MarketplaceId = marketplaceId, MarketplaceType = marketplaceType },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding customer by Marketplace ID: {MarketplaceId} and type: {MarketplaceType}", 
                    marketplaceId, marketplaceType);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetByMarketplaceTypeAsync(string marketplaceType)
        {
            _logger.LogDebug("Attempting to get customers by Marketplace type: {MarketplaceType}", marketplaceType);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.MarketplaceType = @MarketplaceType";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { MarketplaceType = marketplaceType },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers by Marketplace type: {MarketplaceType}", marketplaceType);
                throw;
            }
        }

        public async Task<Customer?> GetByEmailAsync(string email)
        {
            _logger.LogDebug("Attempting to find customer by email: {Email}", email);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.Email = @Email";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { Email = email },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding customer by email: {Email}", email);
                throw;
            }
        }

        public async Task<Customer?> GetByPhoneAsync(string phone)
        {
            _logger.LogDebug("Attempting to find customer by phone: {Phone}", phone);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.Phone = @Phone";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { Phone = phone },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding customer by phone: {Phone}", phone);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> SearchAsync(string searchTerm)
        {
            _logger.LogDebug("Attempting to search customers with term: {SearchTerm}", searchTerm);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.Name ILIKE @SearchTerm 
                   OR c.Email ILIKE @SearchTerm 
                   OR c.Phone ILIKE @SearchTerm";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { SearchTerm = $"%{searchTerm}%" },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetByTagAsync(string tag)
        {
            _logger.LogDebug("Attempting to get customers by tag: {Tag}", tag);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.Tags @> @Tag";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { Tag = new[] { tag } },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers by tag: {Tag}", tag);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetByCustomerTypeAsync(string customerType)
        {
            _logger.LogDebug("Attempting to get customers by type: {CustomerType}", customerType);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.CustomerType = @CustomerType";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { CustomerType = customerType },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers by type: {CustomerType}", customerType);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetTopSpendersAsync(int count)
        {
            _logger.LogDebug("Attempting to get top {Count} spenders", count);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.TotalSpent > 0
                ORDER BY c.TotalSpent DESC
                LIMIT @Count";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { Count = count },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top {Count} spenders", count);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetFrequentBuyersAsync(int count)
        {
            _logger.LogDebug("Attempting to get top {Count} frequent buyers", count);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.OrderCount > 0
                ORDER BY c.OrderCount DESC
                LIMIT @Count";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { Count = count },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top {Count} frequent buyers", count);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetInactiveCustomersAsync(TimeSpan inactivityPeriod)
        {
            _logger.LogDebug("Attempting to get inactive customers for period: {InactivityPeriod}", inactivityPeriod);
            const string sql = @"
                SELECT c.*, o.*, oi.*, p.*, pv.*
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
                LEFT JOIN Products p ON oi.ProductId = p.Id
                LEFT JOIN ProductVariants pv ON oi.ProductVariantId = pv.Id
                WHERE c.LastOrderDate < @LastOrderDate OR c.LastOrderDate IS NULL";

            try
            {
                using var connection = CreateConnection();
                var customerDictionary = new Dictionary<Guid, Customer>();

                await connection.QueryAsync<Customer, Order, OrderItem, Product, ProductVariant, Customer>(
                    sql,
                    (customer, order, orderItem, product, productVariant) =>
                    {
                        if (!customerDictionary.TryGetValue(customer.Id, out var customerEntry))
                        {
                            customerEntry = customer;
                            customerEntry.Orders = new List<Order>();
                            customerDictionary.Add(customerEntry.Id, customerEntry);
                        }

                        if (order != null && !customerEntry.Orders.Any(o => o.Id == order.Id))
                        {
                            order.Items = new List<OrderItem>();
                            customerEntry.Orders.Add(order);
                        }

                        if (orderItem != null && order != null)
                        {
                            orderItem.Product = product;
                            orderItem.ProductVariant = productVariant;
                            order.Items.Add(orderItem);
                        }

                        return customerEntry;
                    },
                    new { LastOrderDate = DateTime.UtcNow - inactivityPeriod },
                    splitOn: "Id,Id,Id,Id"
                );

                return customerDictionary.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inactive customers for period: {InactivityPeriod}", inactivityPeriod);
                throw;
            }
        }

        public async Task<int> GetCustomerCountAsync()
        {
            _logger.LogDebug("Attempting to get total customer count");
            const string sql = "SELECT COUNT(*) FROM Customers";

            try
            {
                using var connection = CreateConnection();
                var count = await connection.ExecuteScalarAsync<int>(sql);
                _logger.LogDebug("Total customer count: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total customer count");
                throw;
            }
        }

        public async Task<decimal> GetAverageOrderValueAsync(Guid customerId)
        {
            _logger.LogDebug("Attempting to get average order value for customer: {CustomerId}", customerId);
            const string sql = @"
                SELECT AVG(o.TotalAmount)
                FROM Orders o
                WHERE o.CustomerId = @CustomerId";

            try
            {
                using var connection = CreateConnection();
                var average = await connection.ExecuteScalarAsync<decimal?>(sql, new { CustomerId = customerId });
                _logger.LogDebug("Average order value for customer {CustomerId}: {Average}", customerId, average);
                return average ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting average order value for customer: {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<int> GetOrderCountAsync(Guid customerId)
        {
            _logger.LogDebug("Attempting to get order count for customer: {CustomerId}", customerId);
            const string sql = @"
                SELECT COUNT(*)
                FROM Orders o
                WHERE o.CustomerId = @CustomerId";

            try
            {
                using var connection = CreateConnection();
                var count = await connection.ExecuteScalarAsync<int>(sql, new { CustomerId = customerId });
                _logger.LogDebug("Order count for customer {CustomerId}: {Count}", customerId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order count for customer: {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<decimal> GetTotalSpentAsync(Guid customerId)
        {
            _logger.LogDebug("Attempting to get total spent for customer: {CustomerId}", customerId);
            const string sql = @"
                SELECT SUM(o.TotalAmount)
                FROM Orders o
                WHERE o.CustomerId = @CustomerId";

            try
            {
                using var connection = CreateConnection();
                var total = await connection.ExecuteScalarAsync<decimal?>(sql, new { CustomerId = customerId });
                _logger.LogDebug("Total spent for customer {CustomerId}: {Total}", customerId, total);
                return total ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total spent for customer: {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetAllTagsAsync()
        {
            _logger.LogDebug("Attempting to get all customer tags");
            const string sql = @"
                SELECT DISTINCT unnest(tags) as tag
                FROM Customers
                WHERE tags IS NOT NULL";

            try
            {
                using var connection = CreateConnection();
                var tags = await connection.QueryAsync<string>(sql);
                _logger.LogDebug("Retrieved {Count} unique tags", tags.Count());
                return tags;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all customer tags");
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetAllCustomerTypesAsync()
        {
            _logger.LogDebug("Attempting to get all customer types");
            const string sql = @"
                SELECT DISTINCT CustomerType
                FROM Customers
                WHERE CustomerType IS NOT NULL";

            try
            {
                using var connection = CreateConnection();
                var types = await connection.QueryAsync<string>(sql);
                _logger.LogDebug("Retrieved {Count} unique customer types", types.Count());
                return types;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all customer types");
                throw;
            }
        }
    }
} 