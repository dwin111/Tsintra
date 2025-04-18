using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Tsintra.Application.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.Domain.Models.NovaPost;
using Microsoft.AspNetCore.Authorization;
using Tsintra.Api.Crm.Services;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CrmController : ControllerBase
    {
        private readonly ICrmService _crmService;
        private readonly ILogger<CrmController> _logger;

        public CrmController(
            ICrmService crmService, 
            ILogger<CrmController> logger)
        {
            _crmService = crmService;
            _logger = logger;
        }

        // Helper method to get current user info
        private UserInfo GetCurrentUser()
        {
            return HttpContext.Items["UserInfo"] as UserInfo ?? new UserInfo();
        }

        // Customer endpoints
        [HttpGet("customers")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving customers", currentUser.Id);
            
            var customers = await _crmService.GetCustomersAsync();
            return Ok(customers);
        }

        [HttpGet("customers/{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving customer {CustomerId}", currentUser.Id, id);
            
            var customer = await _crmService.GetCustomerAsync(id);
            if (customer == null) return NotFound();
            return Ok(customer);
        }

        [HttpPost("customers")]
        public async Task<ActionResult<Customer>> CreateCustomer([FromBody] Customer customer)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} creating new customer", currentUser.Id);
            
            var createdCustomer = await _crmService.CreateCustomerAsync(customer);
            return CreatedAtAction(nameof(GetCustomer), new { id = createdCustomer.Id }, createdCustomer);
        }

        [HttpPut("customers/{id}")]
        public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] Customer customer)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} updating customer {CustomerId}", currentUser.Id, id);
            
            if (id != customer.Id) return BadRequest();
            var result = await _crmService.UpdateCustomerAsync(customer);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpDelete("customers/{id}")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} deleting customer {CustomerId}", currentUser.Id, id);
            
            var result = await _crmService.DeleteCustomerAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        // Order endpoints
        [HttpGet("orders")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving orders", currentUser.Id);
            
            var orders = await _crmService.GetOrdersAsync(startDate, endDate);
            return Ok(orders);
        }

        [HttpGet("orders/{id}")]
        public async Task<ActionResult<Order>> GetOrder(Guid id)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving order {OrderId}", currentUser.Id, id);
            
            var order = await _crmService.GetOrderAsync(id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        [HttpGet("customers/{customerId}/orders")]
        public async Task<ActionResult<IEnumerable<Order>>> GetCustomerOrders(Guid customerId)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving orders for customer {CustomerId}", currentUser.Id, customerId);
            
            var orders = await _crmService.GetCustomerOrdersAsync(customerId);
            return Ok(orders);
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] OrderStatus status)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} updating status for order {OrderId} to {Status}", 
                currentUser.Id, id, status);
            
            var result = await _crmService.UpdateOrderStatusAsync(id, status);
            if (!result) return NotFound();
            return NoContent();
        }

        // Product endpoints
        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving products", currentUser.Id);
            
            var products = await _crmService.GetProductsAsync();
            return Ok(products);
        }

        [HttpGet("products/{id}")]
        public async Task<ActionResult<Product>> GetProduct(Guid id)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving product {ProductId}", currentUser.Id, id);
            
            var product = await _crmService.GetProductAsync(id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        [HttpPost("products")]
        public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} creating new product", currentUser.Id);
            
            var createdProduct = await _crmService.CreateProductAsync(product);
            return CreatedAtAction(nameof(GetProduct), new { id = createdProduct.Id }, createdProduct);
        }

        [HttpPut("products/{id}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] Product product)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} updating product {ProductId}", currentUser.Id, id);
            
            if (id != product.Id) return BadRequest();
            var result = await _crmService.UpdateProductAsync(product);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} deleting product {ProductId}", currentUser.Id, id);
            
            var result = await _crmService.DeleteProductAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        // Marketplace synchronization endpoints
        [HttpPost("marketplace/{name}/sync/orders")]
        public async Task<IActionResult> SyncMarketplaceOrders(string name)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} syncing orders from marketplace {Marketplace}", 
                currentUser.Id, name);
            
            var result = await _crmService.SyncMarketplaceOrdersAsync(name);
            if (!result) return BadRequest();
            return NoContent();
        }

        [HttpPost("marketplace/{name}/sync/products")]
        public async Task<IActionResult> SyncMarketplaceProducts(string name)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} syncing products from marketplace {Marketplace}", 
                currentUser.Id, name);
            
            var result = await _crmService.SyncMarketplaceProductsAsync(name);
            if (!result) return BadRequest();
            return NoContent();
        }

        [HttpPost("marketplace/{name}/sync/customers")]
        public async Task<IActionResult> SyncMarketplaceCustomers(string name)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} syncing customers from marketplace {Marketplace}", 
                currentUser.Id, name);
            
            var result = await _crmService.SyncMarketplaceCustomersAsync(name);
            if (!result) return BadRequest();
            return NoContent();
        }

        // Reporting endpoints
        [HttpGet("reports/sales")]
        public async Task<ActionResult<decimal>> GetTotalSales([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving total sales report", currentUser.Id);
            
            var totalSales = await _crmService.GetTotalSalesAsync(startDate, endDate);
            return Ok(totalSales);
        }

        [HttpGet("reports/orders")]
        public async Task<ActionResult<int>> GetTotalOrders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving total orders report", currentUser.Id);
            
            var totalOrders = await _crmService.GetTotalOrdersAsync(startDate, endDate);
            return Ok(totalOrders);
        }

        [HttpGet("reports/customers")]
        public async Task<ActionResult<int>> GetTotalCustomers()
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving total customers report", currentUser.Id);
            
            var totalCustomers = await _crmService.GetTotalCustomersAsync();
            return Ok(totalCustomers);
        }

        [HttpGet("reports/sales-by-marketplace")]
        public async Task<ActionResult<Dictionary<string, decimal>>> GetSalesByMarketplace([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} retrieving sales by marketplace report", currentUser.Id);
            
            var salesByMarketplace = await _crmService.GetSalesByMarketplaceAsync(startDate, endDate);
            return Ok(salesByMarketplace);
        }

        // Update order with tracking
        [HttpPut("orders/{orderId}/tracking")]
        public async Task<IActionResult> UpdateOrderWithTracking(
            Guid orderId,
            [FromQuery] string trackingNumber,
            [FromQuery] string deliveryService = "Nova Poshta")
        {
            var currentUser = GetCurrentUser();
            _logger.LogInformation("User {UserId} updating tracking for order {OrderId}", 
                currentUser.Id, orderId);
            
            try
            {
                var result = await _crmService.UpdateOrderWithTrackingAsync(orderId, trackingNumber, deliveryService);
                if (!result)
                {
                    return NotFound($"Order with ID '{orderId}' not found");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order tracking: {Message}", ex.Message);
                return StatusCode(500, $"Error updating order tracking: {ex.Message}");
            }
        }
    }
} 