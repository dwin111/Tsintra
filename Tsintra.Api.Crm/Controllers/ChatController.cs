using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Tsintra.Api.Crm.Services;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ISharedMemoryService _sharedMemory;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ISharedMemoryService sharedMemory, ILogger<ChatController> logger)
        {
            _sharedMemory = sharedMemory;
            _logger = logger;
        }

        [HttpPost("message")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto message)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // Отримати спільний контекст для відповіді
                var context = await _sharedMemory.GetUserContext(userId);
                
                // Тут буде звернення до сервісу чата з врахуванням контексту
                var chatData = new
                {
                    message = message,
                    timestamp = DateTime.UtcNow,
                    context = context
                };
                
                // Зберегти повідомлення та контекст у спільній пам'яті
                var chatId = await _sharedMemory.StoreChatMemory(userId, chatData);
                
                // Тут буде фактичний виклик моделі LLM для відповіді
                // Для прикладу просто повертаємо стандартну відповідь
                var response = new ChatResponseDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Message = GenerateResponse(message.Text, context),
                    Timestamp = DateTime.UtcNow
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message");
                return StatusCode(500, "Error processing chat message");
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetChatHistory()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // Тут повинна бути логіка отримання історії чату з бази даних/кешу
                // Для прикладу повертаємо порожній список
                var history = new List<ChatResponseDto>();
                
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history");
                return StatusCode(500, "Error retrieving chat history");
            }
        }

        [HttpGet("context")]
        public async Task<IActionResult> GetChatContext()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var context = await _sharedMemory.GetUserContext(userId);
                return Ok(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat context");
                return StatusCode(500, "Error retrieving chat context");
            }
        }

        // Допоміжний метод для генерування відповіді з урахуванням контексту
        private string GenerateResponse(string message, object context)
        {
            var contextJson = JsonSerializer.Serialize(context);
            
            // Перевіряємо, чи є в контексті інформація про продукт
            if (contextJson.Contains("product") && contextJson.Contains("data"))
            {
                return $"Я бачу, що ви нещодавно працювали з продуктом. Ваше повідомлення: {message}. Як я можу допомогти з цим продуктом?";
            }
            
            // Перевіряємо, чи є в контексті інформація про Instagram
            if (contextJson.Contains("instagram") && contextJson.Contains("data"))
            {
                return $"Я бачу, що ви нещодавно створювали контент для Instagram. Ваше повідомлення: {message}. Потрібна допомога з контентом для соціальних мереж?";
            }
            
            return $"Ваше повідомлення: {message}. Чим я можу допомогти?";
        }
    }

    // DTOs для чата
    public class ChatMessageDto
    {
        public string Text { get; set; } = string.Empty;
        public string? SessionId { get; set; }
    }

    public class ChatResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
} 