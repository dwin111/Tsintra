using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderRepository orderRepository, ILogger<OrdersController> logger)
        {
            _orderRepository = orderRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders()
        {
            try
            {
                var orders = await _orderRepository.GetAllAsync();
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all orders");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(Guid id)
        {
            try
            {
                var order = await _orderRepository.GetByIdAsync(id);
                if (order == null)
                {
                    return NotFound();
                }
                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order with ID: {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder(Order order)
        {
            try
            {
                var createdOrder = await _orderRepository.AddAsync(order);
                return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(Guid id, Order order)
        {
            try
            {
                if (id != order.Id)
                {
                    return BadRequest("Order ID mismatch");
                }

                var success = await _orderRepository.UpdateAsync(order);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order with ID: {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            try
            {
                var success = await _orderRepository.DeleteAsync(id);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order with ID: {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("marketplace/{marketplaceId}/{marketplaceType}")]
        public async Task<ActionResult<Order>> GetOrderByMarketplace(string marketplaceId, string marketplaceType)
        {
            try
            {
                var order = await _orderRepository.GetByMarketplaceIdAsync(marketplaceId, marketplaceType);
                if (order == null)
                {
                    return NotFound();
                }
                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order by marketplace ID: {MarketplaceId} and type: {MarketplaceType}", 
                    marketplaceId, marketplaceType);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("marketplace/{marketplaceType}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByMarketplaceType(string marketplaceType)
        {
            try
            {
                var orders = await _orderRepository.GetByMarketplaceTypeAsync(marketplaceType);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by marketplace type: {MarketplaceType}", marketplaceType);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByCustomer(Guid customerId)
        {
            try
            {
                var orders = await _orderRepository.GetByCustomerIdAsync(customerId);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for customer ID: {CustomerId}", customerId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("status/{status}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByStatus(string status)
        {
            try
            {
                var orders = await _orderRepository.GetByStatusAsync(status);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by status: {Status}", status);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                var success = await _orderRepository.UpdateStatusAsync(id, request.Status, request.Notes, request.ChangedBy);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order ID: {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("date-range")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                var orders = await _orderRepository.GetByDateRangeAsync(startDate, endDate);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for date range: {StartDate} to {EndDate}", startDate, endDate);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Order>>> SearchOrders([FromQuery] string searchTerm)
        {
            try
            {
                var orders = await _orderRepository.SearchAsync(searchTerm);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching orders with term: {SearchTerm}", searchTerm);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("statistics/revenue")]
        public async Task<ActionResult<decimal>> GetTotalRevenue([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var revenue = await _orderRepository.GetTotalRevenueAsync(startDate, endDate);
                return Ok(revenue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total revenue");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("statistics/count")]
        public async Task<ActionResult<int>> GetOrderCount([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var count = await _orderRepository.GetOrderCountAsync(startDate, endDate);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order count");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("statistics/average-value")]
        public async Task<ActionResult<decimal>> GetAverageOrderValue([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var avgValue = await _orderRepository.GetAverageOrderValueAsync(startDate, endDate);
                return Ok(avgValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting average order value");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? ChangedBy { get; set; }
    }
} 