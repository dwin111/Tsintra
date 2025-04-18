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
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ICustomerRepository customerRepository, ILogger<CustomersController> logger)
        {
            _customerRepository = customerRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetAllCustomers()
        {
            try
            {
                var customers = await _customerRepository.GetAllAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all customers");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            try
            {
                var customer = await _customerRepository.GetByIdAsync(id);
                if (customer == null)
                {
                    return NotFound();
                }
                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer with ID: {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Customer>> CreateCustomer(Customer customer)
        {
            try
            {
                var createdCustomer = await _customerRepository.AddAsync(customer);
                return CreatedAtAction(nameof(GetCustomer), new { id = createdCustomer.Id }, createdCustomer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(Guid id, Customer customer)
        {
            try
            {
                if (id != customer.Id)
                {
                    return BadRequest("Customer ID mismatch");
                }

                var success = await _customerRepository.UpdateAsync(customer);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer with ID: {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
            try
            {
                var success = await _customerRepository.DeleteAsync(id);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer with ID: {CustomerId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("marketplace/{marketplaceId}/{marketplaceType}")]
        public async Task<ActionResult<Customer>> GetCustomerByMarketplace(string marketplaceId, string marketplaceType)
        {
            try
            {
                var customer = await _customerRepository.GetByMarketplaceIdAsync(marketplaceId, marketplaceType);
                if (customer == null)
                {
                    return NotFound();
                }
                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer by marketplace ID: {MarketplaceId} and type: {MarketplaceType}", 
                    marketplaceId, marketplaceType);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Customer>>> SearchCustomers([FromQuery] string searchTerm)
        {
            try
            {
                var customers = await _customerRepository.SearchAsync(searchTerm);
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with term: {SearchTerm}", searchTerm);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("top-spenders")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetTopSpenders([FromQuery] int count = 10)
        {
            try
            {
                var customers = await _customerRepository.GetTopSpendersAsync(count);
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top {Count} spenders", count);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("frequent-buyers")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetFrequentBuyers([FromQuery] int count = 10)
        {
            try
            {
                var customers = await _customerRepository.GetFrequentBuyersAsync(count);
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top {Count} frequent buyers", count);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("inactive")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetInactiveCustomers([FromQuery] int days = 90)
        {
            try
            {
                var customers = await _customerRepository.GetInactiveCustomersAsync(TimeSpan.FromDays(days));
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inactive customers for {Days} days", days);
                return StatusCode(500, "Internal server error");
            }
        }
    }
} 