using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tsintra.Application.DTOs;
using Tsintra.Application.Services;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentMemoryController : ControllerBase
{
    private readonly IAgentMemoryService _memoryService;
    private readonly ILogger<AgentMemoryController> _logger;

    public AgentMemoryController(IAgentMemoryService memoryService, ILogger<AgentMemoryController> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    [HttpGet("{conversationId}")]
    public async Task<ActionResult<AgentMemoryDto>> GetMemory(string conversationId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID claim when attempting to get memory");
            return Unauthorized();
        }

        try
        {
            var memory = await _memoryService.GetMemoryAsync(userId, conversationId);
            if (memory == null)
            {
                return NotFound();
            }

            var dto = new AgentMemoryDto
            {
                Id = memory.Id,
                ConversationId = memory.ConversationId,
                Content = memory.Content,
                CreatedAt = memory.CreatedAt,
                ExpiresAt = memory.ExpiresAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving memory for conversation {ConversationId}", conversationId);
            return StatusCode(500, "An error occurred while retrieving the memory");
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveMemory(CreateAgentMemoryRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID claim when attempting to save memory");
            return Unauthorized();
        }

        try
        {
            var memory = new AgentMemory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConversationId = request.ConversationId,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt
            };

            await _memoryService.SaveMemoryAsync(memory);
            
            var dto = new AgentMemoryDto
            {
                Id = memory.Id,
                ConversationId = memory.ConversationId,
                Content = memory.Content,
                CreatedAt = memory.CreatedAt,
                ExpiresAt = memory.ExpiresAt
            };
            
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving memory for conversation {ConversationId}", request.ConversationId);
            return StatusCode(500, "An error occurred while saving the memory");
        }
    }

    [HttpDelete("{conversationId}")]
    public async Task<IActionResult> DeleteMemory(string conversationId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID claim when attempting to delete memory");
            return Unauthorized();
        }

        try
        {
            await _memoryService.DeleteMemoryAsync(userId, conversationId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting memory for conversation {ConversationId}", conversationId);
            return StatusCode(500, "An error occurred while deleting the memory");
        }
    }
} 