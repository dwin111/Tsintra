using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using System.Text;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<StatisticsController> _logger;

        public StatisticsController(
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            ICustomerRepository customerRepository,
            ILogger<StatisticsController> logger)
        {
            _productRepository = productRepository;
            _orderRepository = orderRepository;
            _customerRepository = customerRepository;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardStatistics>> GetDashboardStatistics()
        {
            try
            {
                // Отримуємо загальні дані для дашборда
                var products = await _productRepository.GetAllAsync();
                var orders = await _orderRepository.GetAllAsync();
                var customers = await _customerRepository.GetAllAsync();

                // Розрахунок загальної виручки
                decimal totalRevenue = orders.Sum(o => o.TotalAmount);
                
                // Розрахунок середнього чеку
                decimal averageOrderValue = orders.Any() ? totalRevenue / orders.Count() : 0;

                // Отримуємо продажі за останній тиждень
                var lastWeekOrders = orders.Where(o => o.CreatedAt >= DateTime.UtcNow.AddDays(-7)).ToList();
                decimal lastWeekRevenue = lastWeekOrders.Sum(o => o.TotalAmount);

                // Кількість нових клієнтів за останній місяць
                int newCustomersLastMonth = customers.Count(c => c.CreatedAt >= DateTime.UtcNow.AddMonths(-1));

                var statistics = new DashboardStatistics
                {
                    TotalProducts = products.Count(),
                    TotalOrders = orders.Count(),
                    TotalCustomers = customers.Count(),
                    TotalRevenue = totalRevenue,
                    AverageOrderValue = averageOrderValue,
                    LastWeekRevenue = lastWeekRevenue,
                    NewCustomersLastMonth = newCustomersLastMonth
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard statistics");
                return StatusCode(500, "Error retrieving dashboard statistics");
            }
        }

        [HttpGet("products/top")]
        public async Task<ActionResult<List<TopProductStatistics>>> GetTopProducts([FromQuery] int count = 10)
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                var products = await _productRepository.GetAllAsync();
                
                // Створюємо словник для підрахунку кількості продаж та виручки по кожному продукту
                var productStats = new Dictionary<Guid, TopProductStatistics>();
                
                foreach (var order in orders)
                {
                    if (order.Items == null) continue;
                    
                    foreach (var item in order.Items)
                    {
                        Guid productId = Guid.Parse(item.ProductId);
                        if (!productStats.ContainsKey(productId))
                        {
                            var product = products.FirstOrDefault(p => p.Id == productId);
                            productStats[productId] = new TopProductStatistics
                            {
                                ProductId = productId,
                                ProductName = product?.Name ?? "Невідомий продукт",
                                QuantitySold = 0,
                                Revenue = 0
                            };
                        }
                        
                        productStats[productId].QuantitySold += item.Quantity;
                        productStats[productId].Revenue += item.UnitPrice * item.Quantity;
                    }
                }
                
                // Сортуємо продукти за кількістю та обмежуємо результат
                var topProducts = productStats.Values
                    .OrderByDescending(p => p.QuantitySold)
                    .Take(count)
                    .ToList();
                
                return Ok(topProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top products statistics");
                return StatusCode(500, "Error retrieving top products statistics");
            }
        }

        [HttpGet("revenue/monthly")]
        public async Task<ActionResult<List<RevenueByPeriod>>> GetMonthlyRevenue([FromQuery] int months = 12)
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                
                // Вираховуємо початкову дату для статистики
                var startDate = DateTime.UtcNow.AddMonths(-months + 1).Date;
                startDate = new DateTime(startDate.Year, startDate.Month, 1); // Перший день місяця
                
                // Групуємо замовлення по місяцях і рахуємо виручку
                var monthlyRevenue = orders
                    .Where(o => o.CreatedAt >= startDate)
                    .GroupBy(o => new { Year = o.CreatedAt.Year, Month = o.CreatedAt.Month })
                    .Select(g => new RevenueByPeriod
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Revenue = g.Sum(o => o.TotalAmount),
                        OrderCount = g.Count()
                    })
                    .OrderBy(r => r.Year)
                    .ThenBy(r => r.Month)
                    .ToList();
                
                // Додаємо місяці, в яких не було замовлень
                var currentDate = startDate;
                while (currentDate <= DateTime.UtcNow)
                {
                    var year = currentDate.Year;
                    var month = currentDate.Month;
                    
                    if (!monthlyRevenue.Any(r => r.Year == year && r.Month == month))
                    {
                        monthlyRevenue.Add(new RevenueByPeriod
                        {
                            Year = year,
                            Month = month,
                            Revenue = 0,
                            OrderCount = 0
                        });
                    }
                    
                    currentDate = currentDate.AddMonths(1);
                }
                
                // Сортуємо результат
                monthlyRevenue = monthlyRevenue
                    .OrderBy(r => r.Year)
                    .ThenBy(r => r.Month)
                    .ToList();
                
                return Ok(monthlyRevenue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving monthly revenue statistics");
                return StatusCode(500, "Error retrieving monthly revenue statistics");
            }
        }

        [HttpGet("revenue/daily")]
        public async Task<ActionResult<List<RevenueByDay>>> GetDailyRevenue([FromQuery] int days = 30)
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                
                // Вираховуємо початкову дату для статистики
                var startDate = DateTime.UtcNow.AddDays(-days + 1).Date;
                
                // Групуємо замовлення по днях і рахуємо виручку
                var dailyRevenue = orders
                    .Where(o => o.CreatedAt >= startDate)
                    .GroupBy(o => o.CreatedAt.Date)
                    .Select(g => new RevenueByDay
                    {
                        Date = g.Key,
                        Revenue = g.Sum(o => o.TotalAmount),
                        OrderCount = g.Count()
                    })
                    .OrderBy(r => r.Date)
                    .ToList();
                
                // Додаємо дні, в яких не було замовлень
                var currentDate = startDate;
                while (currentDate <= DateTime.UtcNow.Date)
                {
                    if (!dailyRevenue.Any(r => r.Date.Date == currentDate.Date))
                    {
                        dailyRevenue.Add(new RevenueByDay
                        {
                            Date = currentDate,
                            Revenue = 0,
                            OrderCount = 0
                        });
                    }
                    
                    currentDate = currentDate.AddDays(1);
                }
                
                // Сортуємо результат
                dailyRevenue = dailyRevenue.OrderBy(r => r.Date).ToList();
                
                return Ok(dailyRevenue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily revenue statistics");
                return StatusCode(500, "Error retrieving daily revenue statistics");
            }
        }

        [HttpGet("customers/top")]
        public async Task<ActionResult<List<TopCustomerStatistics>>> GetTopCustomers([FromQuery] int count = 10)
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                var customers = await _customerRepository.GetAllAsync();
                
                // Створюємо словник для підрахунку кількості замовлень та виручки по кожному клієнту
                var customerStats = new Dictionary<Guid, TopCustomerStatistics>();
                
                foreach (var order in orders)
                {
                    if (!customerStats.ContainsKey(order.CustomerId))
                    {
                        var customer = customers.FirstOrDefault(c => c.Id == order.CustomerId);
                        customerStats[order.CustomerId] = new TopCustomerStatistics
                        {
                            CustomerId = order.CustomerId,
                            CustomerName = customer != null ? $"{customer.Name}" : "Невідомий клієнт",
                            OrderCount = 0,
                            TotalSpent = 0
                        };
                    }
                    
                    customerStats[order.CustomerId].OrderCount++;
                    customerStats[order.CustomerId].TotalSpent += order.TotalAmount;
                }
                
                // Сортуємо клієнтів за сумою витрат та обмежуємо результат
                var topCustomers = customerStats.Values
                    .OrderByDescending(c => c.TotalSpent)
                    .Take(count)
                    .ToList();
                
                return Ok(topCustomers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top customers statistics");
                return StatusCode(500, "Error retrieving top customers statistics");
            }
        }

        [HttpGet("orders/status")]
        public async Task<ActionResult<OrderStatusStatistics>> GetOrderStatusStatistics()
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                
                // Підраховуємо кількість замовлень за статусами
                var pending = orders.Count(o => o.Status.ToString() == "Pending");
                var processing = orders.Count(o => o.Status.ToString() == "Processing");
                var shipped = orders.Count(o => o.Status.ToString() == "Shipped");
                var delivered = orders.Count(o => o.Status.ToString() == "Delivered");
                var cancelled = orders.Count(o => o.Status.ToString() == "Cancelled");
                
                var statistics = new OrderStatusStatistics
                {
                    Pending = pending,
                    Processing = processing,
                    Shipped = shipped,
                    Delivered = delivered,
                    Cancelled = cancelled
                };
                
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order status statistics");
                return StatusCode(500, "Error retrieving order status statistics");
            }
        }

        [HttpGet("product/{id}")]
        public async Task<ActionResult<ProductDetailedStatistics>> GetProductStatistics(Guid id)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    return NotFound();
                }
                
                var orders = await _orderRepository.GetAllAsync();
                
                // Рахуємо кількість проданих одиниць продукту та виручку
                int quantitySold = 0;
                decimal revenue = 0;
                List<DateTime> orderDates = new List<DateTime>();
                
                foreach (var order in orders)
                {
                    if (order.Items == null) continue;
                    
                    var productItems = order.Items.Where(item => Guid.Parse(item.ProductId) == id).ToList();
                    if (productItems.Any())
                    {
                        quantitySold += productItems.Sum(item => item.Quantity);
                        revenue += productItems.Sum(item => item.UnitPrice * item.Quantity);
                        orderDates.Add(order.CreatedAt);
                    }
                }
                
                // Визначаємо дату першого та останнього продажу
                DateTime? firstSaleDate = orderDates.Any() ? orderDates.Min() : null;
                DateTime? lastSaleDate = orderDates.Any() ? orderDates.Max() : null;
                
                // Кількість днів з першого продажу
                int daysSinceFirstSale = firstSaleDate.HasValue ? (int)(DateTime.UtcNow - firstSaleDate.Value).TotalDays : 0;
                
                // Середня кількість продажів на день
                double averageDailySales = daysSinceFirstSale > 0 ? (double)quantitySold / daysSinceFirstSale : 0;
                
                var statistics = new ProductDetailedStatistics
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    CurrentPrice = product.Price,
                    QuantitySold = quantitySold,
                    TotalRevenue = revenue,
                    FirstSaleDate = firstSaleDate,
                    LastSaleDate = lastSaleDate,
                    AverageDailySales = averageDailySales
                };
                
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product statistics for product {ProductId}", id);
                return StatusCode(500, "Error retrieving product statistics");
            }
        }

        [HttpGet("customer/{id}")]
        public async Task<ActionResult<CustomerDetailedStatistics>> GetCustomerStatistics(Guid id)
        {
            try
            {
                var customer = await _customerRepository.GetByIdAsync(id);
                if (customer == null)
                {
                    return NotFound();
                }
                
                var orders = await _orderRepository.GetAllAsync();
                var customerOrders = orders.Where(o => o.CustomerId == id).ToList();
                
                // Рахуємо загальну суму витрат клієнта
                decimal totalSpent = customerOrders.Sum(o => o.TotalAmount);
                
                // Визначаємо дату першого та останнього замовлення
                DateTime? firstOrderDate = customerOrders.Any() ? customerOrders.Min(o => o.CreatedAt) : null;
                DateTime? lastOrderDate = customerOrders.Any() ? customerOrders.Max(o => o.CreatedAt) : null;
                
                // Визначаємо середню суму замовлення
                decimal averageOrderValue = customerOrders.Any() ? totalSpent / customerOrders.Count() : 0;
                
                // Отримуємо список найбільш популярних продуктів клієнта
                var productCounts = new Dictionary<Guid, int>();
                foreach (var order in customerOrders)
                {
                    if (order.Items == null) continue;
                    
                    foreach (var item in order.Items)
                    {
                        Guid productId = Guid.Parse(item.ProductId);
                        if (!productCounts.ContainsKey(productId))
                        {
                            productCounts[productId] = 0;
                        }
                        
                        productCounts[productId] += item.Quantity;
                    }
                }
                
                var products = await _productRepository.GetAllAsync();
                var favouriteProducts = productCounts
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .Select(kv => new {
                        ProductId = kv.Key,
                        ProductName = products.FirstOrDefault(p => p.Id == kv.Key)?.Name ?? "Невідомий продукт",
                        Quantity = kv.Value
                    })
                    .ToList();
                
                var statistics = new CustomerDetailedStatistics
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Email = customer.Email,
                    Phone = customer.Phone,
                    TotalOrders = customerOrders.Count(),
                    TotalSpent = totalSpent,
                    FirstOrderDate = firstOrderDate,
                    LastOrderDate = lastOrderDate,
                    AverageOrderValue = averageOrderValue,
                    FavouriteProducts = favouriteProducts.Select(p => $"{p.ProductName} ({p.Quantity} шт.)").ToList()
                };
                
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer statistics for customer {CustomerId}", id);
                return StatusCode(500, "Error retrieving customer statistics");
            }
        }

        [HttpGet("by-period")]
        public async Task<ActionResult<PeriodStatistics>> GetStatisticsByPeriod(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                if (startDate > endDate)
                {
                    return BadRequest("Start date cannot be later than end date");
                }
                
                var orders = await _orderRepository.GetAllAsync();
                var products = await _productRepository.GetAllAsync();
                var customers = await _customerRepository.GetAllAsync();
                
                // Фільтруємо дані за вказаний період
                var periodOrders = orders.Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate).ToList();
                var newCustomers = customers.Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate).ToList();
                
                // Розраховуємо метрики
                decimal totalRevenue = periodOrders.Sum(o => o.TotalAmount);
                int totalOrderCount = periodOrders.Count();
                decimal averageOrderValue = totalOrderCount > 0 ? totalRevenue / totalOrderCount : 0;
                int newCustomerCount = newCustomers.Count();
                
                // Продукти, придбані за період
                var productQuantities = new Dictionary<Guid, int>();
                var productRevenue = new Dictionary<Guid, decimal>();
                
                foreach (var order in periodOrders)
                {
                    if (order.Items == null) continue;
                    
                    foreach (var item in order.Items)
                    {
                        Guid productId = Guid.Parse(item.ProductId);
                        if (!productQuantities.ContainsKey(productId))
                        {
                            productQuantities[productId] = 0;
                            productRevenue[productId] = 0;
                        }
                        
                        productQuantities[productId] += item.Quantity;
                        productRevenue[productId] += item.UnitPrice * item.Quantity;
                    }
                }
                
                // Знаходимо найпопулярніші продукти за період
                var topProducts = productQuantities
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .Select(kv => new 
                    {
                        ProductId = kv.Key,
                        ProductName = products.FirstOrDefault(p => p.Id == kv.Key)?.Name ?? "Невідомий продукт",
                        Quantity = kv.Value,
                        Revenue = productRevenue[kv.Key]
                    })
                    .ToList();
                
                var statistics = new PeriodStatistics
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalOrders = totalOrderCount,
                    TotalRevenue = totalRevenue,
                    AverageOrderValue = averageOrderValue,
                    NewCustomers = newCustomerCount,
                    TopProducts = topProducts.Select(p => new TopProductForPeriod
                    {
                        ProductId = p.ProductId,
                        ProductName = p.ProductName,
                        QuantitySold = p.Quantity,
                        Revenue = p.Revenue
                    }).ToList()
                };
                
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving statistics for period from {StartDate} to {EndDate}", 
                    startDate, endDate);
                return StatusCode(500, "Error retrieving period statistics");
            }
        }

        [HttpGet("export/csv/products")]
        public async Task<IActionResult> ExportProductsStatisticsCsv()
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                var products = await _productRepository.GetAllAsync();
                
                // Створюємо словник для підрахунку продаж по кожному продукту
                var productStats = new Dictionary<Guid, (int quantity, decimal revenue)>();
                
                foreach (var order in orders)
                {
                    if (order.Items == null) continue;
                    
                    foreach (var item in order.Items)
                    {
                        Guid productId = Guid.Parse(item.ProductId);
                        if (!productStats.ContainsKey(productId))
                        {
                            productStats[productId] = (0, 0);
                        }
                        
                        var currentStats = productStats[productId];
                        productStats[productId] = (
                            currentStats.quantity + item.Quantity, 
                            currentStats.revenue + (item.UnitPrice * item.Quantity)
                        );
                    }
                }
                
                // Створюємо CSV файл
                var csv = new StringBuilder();
                
                // Заголовок CSV файлу
                csv.AppendLine("Product ID,Product Name,SKU,Price,Quantity In Stock,Quantity Sold,Total Revenue");
                
                // Додаємо дані по кожному продукту
                foreach (var product in products)
                {
                    var stats = productStats.TryGetValue(product.Id, out var value) ? value : (0, 0);
                    
                    csv.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6}",
                        product.Id,
                        EscapeCsvField(product.Name),
                        EscapeCsvField(product.Sku),
                        product.Price,
                        product.QuantityInStock,
                        stats.quantity,
                        stats.revenue
                    ));
                }
                
                // Повертаємо CSV файл
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var result = new FileContentResult(bytes, "text/csv")
                {
                    FileDownloadName = $"products_statistics_{DateTime.UtcNow:yyyy-MM-dd}.csv"
                };
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting products statistics to CSV");
                return StatusCode(500, "Error exporting products statistics");
            }
        }

        [HttpGet("export/csv/customers")]
        public async Task<IActionResult> ExportCustomersStatisticsCsv()
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                var customers = await _customerRepository.GetAllAsync();
                
                // Створюємо словник для підрахунку замовлень по кожному клієнту
                var customerStats = new Dictionary<Guid, (int orderCount, decimal totalSpent)>();
                
                foreach (var order in orders)
                {
                    if (!customerStats.ContainsKey(order.CustomerId))
                    {
                        customerStats[order.CustomerId] = (0, 0);
                    }
                    
                    var currentStats = customerStats[order.CustomerId];
                    customerStats[order.CustomerId] = (
                        currentStats.orderCount + 1, 
                        currentStats.totalSpent + order.TotalAmount
                    );
                }
                
                // Створюємо CSV файл
                var csv = new StringBuilder();
                
                // Заголовок CSV файлу
                csv.AppendLine("Customer ID,Name,Email,Phone,Order Count,Total Spent,Average Order Value");
                
                // Додаємо дані по кожному клієнту
                foreach (var customer in customers)
                {
                    var stats = customerStats.TryGetValue(customer.Id, out var value) ? value : (0, 0);
                    var averageOrderValue = stats.orderCount > 0 ? stats.totalSpent / stats.orderCount : 0;
                    
                    csv.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6}",
                        customer.Id,
                        EscapeCsvField(customer.Name),
                        EscapeCsvField(customer.Email),
                        EscapeCsvField(customer.Phone),
                        stats.orderCount,
                        stats.totalSpent,
                        averageOrderValue
                    ));
                }
                
                // Повертаємо CSV файл
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var result = new FileContentResult(bytes, "text/csv")
                {
                    FileDownloadName = $"customers_statistics_{DateTime.UtcNow:yyyy-MM-dd}.csv"
                };
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting customers statistics to CSV");
                return StatusCode(500, "Error exporting customers statistics");
            }
        }

        // Допоміжний метод для екранування полів в CSV
        private string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }
            
            // Якщо поле містить кому, лапки або переноси рядків, обрамляємо його лапками
            if (field.Contains(',') || field.Contains('\"') || field.Contains('\n') || field.Contains('\r'))
            {
                // Дублюємо лапки для екранування
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            
            return field;
        }
    }
    
    // Моделі даних для статистики
    public class DashboardStatistics
    {
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal LastWeekRevenue { get; set; }
        public int NewCustomersLastMonth { get; set; }
    }
    
    public class TopProductStatistics
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
    
    public class RevenueByPeriod
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        
        public string PeriodName => $"{Year}-{Month:D2}";
    }
    
    public class RevenueByDay
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        
        public string FormattedDate => Date.ToString("yyyy-MM-dd");
    }
    
    public class TopCustomerStatistics
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
    }
    
    public class OrderStatusStatistics
    {
        public int Pending { get; set; }
        public int Processing { get; set; }
        public int Shipped { get; set; }
        public int Delivered { get; set; }
        public int Cancelled { get; set; }
    }
    
    public class ProductDetailedStatistics
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal CurrentPrice { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public DateTime? FirstSaleDate { get; set; }
        public DateTime? LastSaleDate { get; set; }
        public double AverageDailySales { get; set; }
    }
    
    public class CustomerDetailedStatistics
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime? FirstOrderDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<string> FavouriteProducts { get; set; } = new List<string>();
    }
    
    public class PeriodStatistics
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int NewCustomers { get; set; }
        public List<TopProductForPeriod> TopProducts { get; set; } = new List<TopProductForPeriod>();
    }
    
    public class TopProductForPeriod
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
} 