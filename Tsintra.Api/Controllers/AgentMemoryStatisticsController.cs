using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tsintra.Application.Services;

namespace Tsintra.Api.Controllers;

[ApiController]
[Route("api/agent-memory/statistics")]
[Authorize(Roles = "Admin")] // Тільки для адміністраторів
public class AgentMemoryStatisticsController : ControllerBase
{
    private readonly IAgentMemoryStatisticsService _statisticsService;
    private readonly ILogger<AgentMemoryStatisticsController> _logger;

    public AgentMemoryStatisticsController(
        IAgentMemoryStatisticsService statisticsService,
        ILogger<AgentMemoryStatisticsController> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<MemoryStatistics>> GetStatistics()
    {
        try
        {
            var statistics = await _statisticsService.GetStatisticsAsync();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні статистики пам'яті агента");
            return StatusCode(500, "Виникла помилка під час отримання статистики");
        }
    }

    [HttpGet("users/{userId}")]
    public async Task<ActionResult<MemoryStatistics>> GetUserStatistics(Guid userId)
    {
        try
        {
            var statistics = await _statisticsService.GetStatisticsForUserAsync(userId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні статистики пам'яті агента для користувача {UserId}", userId);
            return StatusCode(500, "Виникла помилка під час отримання статистики");
        }
    }

    [HttpGet("top-conversations")]
    public async Task<ActionResult<Dictionary<string, int>>> GetTopConversations([FromQuery] int count = 10)
    {
        try
        {
            var topConversations = await _statisticsService.GetTopConversationsAsync(count);
            return Ok(topConversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні топ розмов за кількістю записів пам'яті");
            return StatusCode(500, "Виникла помилка під час отримання топ розмов");
        }
    }

    [HttpGet("my")]
    [Authorize] // Доступно будь-якому авторизованому користувачу
    public async Task<ActionResult<MemoryStatistics>> GetMyStatistics()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Невірний ідентифікатор користувача при спробі отримання статистики");
            return Unauthorized();
        }

        try
        {
            var statistics = await _statisticsService.GetStatisticsForUserAsync(userId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні статистики пам'яті агента для поточного користувача");
            return StatusCode(500, "Виникла помилка під час отримання статистики");
        }
    }
} 