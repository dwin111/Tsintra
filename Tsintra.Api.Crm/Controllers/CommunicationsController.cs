using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Models;
using Tsintra.Domain.Interfaces;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommunicationsController : ControllerBase
    {
        private readonly ICommunicationRepository _communicationRepository;
        private readonly ILogger<CommunicationsController> _logger;

        public CommunicationsController(ICommunicationRepository communicationRepository, ILogger<CommunicationsController> logger)
        {
            _communicationRepository = communicationRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Communication>>> GetAllCommunications([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var communications = await _communicationRepository.GetAllAsync(startDate, endDate);
                return Ok(communications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання комунікацій");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Communication>> GetCommunication(Guid id)
        {
            try
            {
                var communication = await _communicationRepository.GetByIdAsync(id);
                if (communication == null)
                {
                    return NotFound();
                }
                return Ok(communication);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання комунікації з ID: {CommunicationId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<Communication>>> GetCommunicationsByCustomer(Guid customerId)
        {
            try
            {
                var communications = await _communicationRepository.GetByCustomerIdAsync(customerId);
                return Ok(communications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання комунікацій для клієнта: {CustomerId}", customerId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Communication>> CreateCommunication(Communication communication)
        {
            try
            {
                var createdCommunication = await _communicationRepository.AddAsync(communication);
                return CreatedAtAction(nameof(GetCommunication), new { id = createdCommunication.Id }, createdCommunication);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка створення комунікації");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCommunication(Guid id, Communication communication)
        {
            try
            {
                if (id != communication.Id)
                {
                    return BadRequest("Невідповідність ID комунікації");
                }

                var success = await _communicationRepository.UpdateAsync(communication);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка оновлення комунікації з ID: {CommunicationId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCommunication(Guid id)
        {
            try
            {
                var success = await _communicationRepository.DeleteAsync(id);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка видалення комунікації з ID: {CommunicationId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("types")]
        public ActionResult<IEnumerable<string>> GetCommunicationTypes()
        {
            try
            {
                var types = Enum.GetNames(typeof(CommunicationType));
                return Ok(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання типів комунікацій");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("by-type/{type}")]
        public async Task<ActionResult<IEnumerable<Communication>>> GetCommunicationsByType(string type)
        {
            try
            {
                if (!Enum.TryParse<CommunicationType>(type, true, out var communicationType))
                {
                    return BadRequest("Невірний тип комунікації");
                }

                var communications = await _communicationRepository.GetByTypeAsync(communicationType);
                return Ok(communications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання комунікацій за типом: {Type}", type);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<Communication>>> GetRecentCommunications([FromQuery] int count = 10)
        {
            try
            {
                var communications = await _communicationRepository.GetRecentAsync(count);
                return Ok(communications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання останніх {Count} комунікацій", count);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }
    }
} 