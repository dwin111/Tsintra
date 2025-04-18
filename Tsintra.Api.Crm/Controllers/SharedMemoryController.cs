using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using System.Security.Claims;

namespace Tsintra.Api.Crm.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SharedMemoryController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<SharedMemoryController> _logger;
        
        // Префікси для різних типів даних у пам'яті
        private const string PRODUCT_PREFIX = "product:";
        private const string CHAT_PREFIX = "chat:";
        private const string INSTAGRAM_PREFIX = "instagram:";
        private const string CROSS_REFERENCE_PREFIX = "xref:";

        public SharedMemoryController(IDistributedCache cache, ILogger<SharedMemoryController> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        [HttpPost("store/product")]
        [Authorize]
        public async Task<IActionResult> StoreProductMemory([FromBody] object productData)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var productJson = JsonSerializer.Serialize(productData);
                var productId = Guid.NewGuid().ToString();
                var key = $"{PRODUCT_PREFIX}{userId}:{productId}";
                
                // Зберегти інформацію про продукт
                await _cache.SetStringAsync(key, productJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // Зберігати місяць
                });
                
                // Зберегти перехресне посилання для швидкого пошуку всіх продуктів користувача
                await _cache.SetStringAsync($"{CROSS_REFERENCE_PREFIX}user:{userId}:products", 
                    productId, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                    });
                
                return Ok(new { id = productId, message = "Product memory stored successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing product memory");
                return StatusCode(500, "Error storing product memory");
            }
        }

        [HttpPost("store/chat")]
        [Authorize]
        public async Task<IActionResult> StoreChatMemory([FromBody] object chatData)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var chatJson = JsonSerializer.Serialize(chatData);
                var chatId = Guid.NewGuid().ToString();
                var key = $"{CHAT_PREFIX}{userId}:{chatId}";
                
                await _cache.SetStringAsync(key, chatJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) // Зберігати тиждень
                });
                
                return Ok(new { id = chatId, message = "Chat memory stored successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing chat memory");
                return StatusCode(500, "Error storing chat memory");
            }
        }

        [HttpPost("store/instagram")]
        [Authorize]
        public async Task<IActionResult> StoreInstagramMemory([FromBody] object instagramData)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var instagramJson = JsonSerializer.Serialize(instagramData);
                var instagramId = Guid.NewGuid().ToString();
                var key = $"{INSTAGRAM_PREFIX}{userId}:{instagramId}";
                
                await _cache.SetStringAsync(key, instagramJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) // Зберігати два тижні
                });
                
                return Ok(new { id = instagramId, message = "Instagram memory stored successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing Instagram memory");
                return StatusCode(500, "Error storing Instagram memory");
            }
        }

        [HttpGet("retrieve/product/{productId}")]
        [Authorize]
        public async Task<IActionResult> GetProductMemory(string productId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var key = $"{PRODUCT_PREFIX}{userId}:{productId}";
                var productJson = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(productJson))
                {
                    return NotFound("Product memory not found");
                }
                
                var productData = JsonSerializer.Deserialize<object>(productJson);
                return Ok(productData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product memory");
                return StatusCode(500, "Error retrieving product memory");
            }
        }

        [HttpGet("retrieve/chat/{chatId}")]
        [Authorize]
        public async Task<IActionResult> GetChatMemory(string chatId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var key = $"{CHAT_PREFIX}{userId}:{chatId}";
                var chatJson = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(chatJson))
                {
                    return NotFound("Chat memory not found");
                }
                
                var chatData = JsonSerializer.Deserialize<object>(chatJson);
                return Ok(chatData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat memory");
                return StatusCode(500, "Error retrieving chat memory");
            }
        }

        [HttpGet("retrieve/instagram/{instagramId}")]
        [Authorize]
        public async Task<IActionResult> GetInstagramMemory(string instagramId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var key = $"{INSTAGRAM_PREFIX}{userId}:{instagramId}";
                var instagramJson = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(instagramJson))
                {
                    return NotFound("Instagram memory not found");
                }
                
                var instagramData = JsonSerializer.Deserialize<object>(instagramJson);
                return Ok(instagramData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Instagram memory");
                return StatusCode(500, "Error retrieving Instagram memory");
            }
        }

        [HttpGet("list/all")]
        [Authorize]
        public async Task<IActionResult> GetAllUserMemory()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // Тут потрібно буде додати логіку для отримання всіх записів користувача
                // Ця реалізація буде спрощеною для прикладу
                
                var result = new
                {
                    products = await GetUserProductIds(userId),
                    chats = await GetUserChatIds(userId),
                    instagram = await GetUserInstagramIds(userId)
                };
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all user memory");
                return StatusCode(500, "Error retrieving all user memory");
            }
        }

        [HttpGet("context")]
        [Authorize]
        public async Task<IActionResult> GetUserContext()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                // Отримуємо останні дані з усіх підсистем для формування контексту
                var latestProduct = await GetLatestProductMemory(userId);
                var latestChat = await GetLatestChatMemory(userId);
                var latestInstagram = await GetLatestInstagramMemory(userId);
                
                var context = new
                {
                    userId,
                    lastActivity = DateTime.UtcNow,
                    latestProduct,
                    latestChat,
                    latestInstagram
                };
                
                return Ok(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user context");
                return StatusCode(500, "Error retrieving user context");
            }
        }

        // Допоміжні методи для отримання даних
        private async Task<List<string>> GetUserProductIds(string userId)
        {
            // Спрощена реалізація. В реальному проекті це буде складніша логіка пошуку за префіксом
            return new List<string>();
        }

        private async Task<List<string>> GetUserChatIds(string userId)
        {
            return new List<string>();
        }

        private async Task<List<string>> GetUserInstagramIds(string userId)
        {
            return new List<string>();
        }

        private async Task<object> GetLatestProductMemory(string userId)
        {
            // Заглушка. У реальній реалізації тут буде отримання останнього продукту з кешу
            return new { };
        }

        private async Task<object> GetLatestChatMemory(string userId)
        {
            return new { };
        }

        private async Task<object> GetLatestInstagramMemory(string userId)
        {
            return new { };
        }
    }
} 