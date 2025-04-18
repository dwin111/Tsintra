using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Tsintra.Api.Crm.Services;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InstagramController : ControllerBase
    {
        private readonly ISharedMemoryService _sharedMemory;
        private readonly ILogger<InstagramController> _logger;

        public InstagramController(ISharedMemoryService sharedMemory, ILogger<InstagramController> logger)
        {
            _sharedMemory = sharedMemory;
            _logger = logger;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateInstagramContent([FromBody] InstagramGenerationRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // Отримуємо спільний контекст для генерації контенту
                var context = await _sharedMemory.GetUserContext(userId);
                
                // Тут буде звернення до сервісу генерації контенту з врахуванням контексту
                var generatedContent = new InstagramContent
                {
                    Id = Guid.NewGuid().ToString(),
                    Caption = GenerateCaption(request, context),
                    Hashtags = GenerateHashtags(request),
                    SuggestedImages = new List<string>(),
                    CreatedAt = DateTime.UtcNow
                };
                
                // Зберігаємо згенерований контент в спільній пам'яті
                var instagramId = await _sharedMemory.StoreInstagramMemory(userId, generatedContent);
                
                // Якщо в контексті є продукт, створюємо зв'язок між продуктом та контентом
                if (context.GetType().GetProperty("product") != null)
                {
                    var productProperty = context.GetType().GetProperty("product");
                    var productValue = productProperty?.GetValue(context);
                    if (productValue != null && productValue.GetType().GetProperty("id") != null)
                    {
                        var productIdProperty = productValue.GetType().GetProperty("id");
                        var productId = productIdProperty?.GetValue(productValue)?.ToString();
                        
                        if (!string.IsNullOrEmpty(productId))
                        {
                            await _sharedMemory.LinkMemories(userId, productId, instagramId, "product", "instagram");
                            _logger.LogInformation("Linked product {ProductId} with Instagram content {InstagramId}", productId, instagramId);
                        }
                    }
                }
                
                return Ok(generatedContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Instagram content");
                return StatusCode(500, "Error generating Instagram content");
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetInstagramHistory()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // Тут повинна бути логіка отримання історії згенерованого контенту з бази даних/кешу
                // Для прикладу повертаємо порожній список
                var history = new List<InstagramContent>();
                
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Instagram content history");
                return StatusCode(500, "Error retrieving Instagram content history");
            }
        }
        
        [HttpGet("context")]
        public async Task<IActionResult> GetInstagramContext()
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
                _logger.LogError(ex, "Error retrieving Instagram context");
                return StatusCode(500, "Error retrieving Instagram context");
            }
        }
        
        [HttpGet("shared/{productId}")]
        public async Task<IActionResult> GenerateInstagramForProduct(string productId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // Отримуємо інформацію про продукт зі спільної пам'яті
                var product = await _sharedMemory.GetProductMemory<object>(userId, productId);
                if (product == null)
                {
                    return NotFound("Product not found in shared memory");
                }
                
                // Створюємо запит на основі даних про продукт
                var request = new InstagramGenerationRequest
                {
                    ProductInfo = product.ToString() ?? "",
                    Style = "professional",
                    Tone = "friendly"
                };
                
                // Генеруємо контент для Instagram
                var generatedContent = new InstagramContent
                {
                    Id = Guid.NewGuid().ToString(),
                    Caption = $"Чудовий товар для вашого дому! Перегляньте наш каталог для більшої інформації. #товари #дім #якість",
                    Hashtags = new List<string> { "#товари", "#дім", "#якість" },
                    SuggestedImages = new List<string>(),
                    CreatedAt = DateTime.UtcNow
                };
                
                // Зберігаємо згенерований контент в спільній пам'яті
                var instagramId = await _sharedMemory.StoreInstagramMemory(userId, generatedContent);
                
                // Створюємо зв'язок між продуктом та контентом
                await _sharedMemory.LinkMemories(userId, productId, instagramId, "product", "instagram");
                
                return Ok(generatedContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Instagram content for product");
                return StatusCode(500, "Error generating Instagram content for product");
            }
        }

        // Допоміжні методи для генерації контенту
        private string GenerateCaption(InstagramGenerationRequest request, object context)
        {
            // Для демонстрації використовуємо прості шаблони
            // В реальній системі тут буде виклик LLM моделі
            
            // Перевіряємо, чи є в контексті інформація про продукт
            // і використовуємо її для персоналізації підпису
            if (context != null)
            {
                var contextType = context.GetType();
                var productProperty = contextType.GetProperty("product");
                
                if (productProperty != null)
                {
                    var product = productProperty.GetValue(context);
                    if (product != null)
                    {
                        return $"Погляньте на наш чудовий товар! {request.ProductInfo} ✨ Замовляйте зараз і отримайте знижку!";
                    }
                }
            }
            
            return $"Новий день, нові можливості! {request.ProductInfo} #інстаграм #контент";
        }
        
        private List<string> GenerateHashtags(InstagramGenerationRequest request)
        {
            // В реальній системі тут буде аналіз запиту та генерація відповідних хештегів
            return new List<string>
            {
                "#instagram",
                "#content",
                "#marketing",
                "#social",
                "#business"
            };
        }
    }

    // DTOs для Instagram
    public class InstagramGenerationRequest
    {
        public string ProductInfo { get; set; } = string.Empty;
        public string Style { get; set; } = "casual";
        public string Tone { get; set; } = "friendly";
        public List<string>? Keywords { get; set; }
    }

    public class InstagramContent
    {
        public string Id { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public List<string> Hashtags { get; set; } = new List<string>();
        public List<string> SuggestedImages { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
    }
} 