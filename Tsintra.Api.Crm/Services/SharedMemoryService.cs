using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Tsintra.Api.Crm.Services
{
    public interface ISharedMemoryService
    {
        Task<string> StoreProductMemory(string userId, object productData);
        Task<string> StoreChatMemory(string userId, object chatData);
        Task<string> StoreInstagramMemory(string userId, object instagramData);
        Task<T?> GetProductMemory<T>(string userId, string productId);
        Task<T?> GetChatMemory<T>(string userId, string chatId);
        Task<T?> GetInstagramMemory<T>(string userId, string instagramId);
        Task<object> GetUserContext(string userId);
        Task<bool> LinkMemories(string userId, string sourceId, string targetId, string sourceType, string targetType);
    }

    public class SharedMemoryService : ISharedMemoryService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<SharedMemoryService> _logger;
        
        // Префікси для різних типів даних у пам'яті
        private const string PRODUCT_PREFIX = "product:";
        private const string CHAT_PREFIX = "chat:";
        private const string INSTAGRAM_PREFIX = "instagram:";
        private const string CROSS_REFERENCE_PREFIX = "xref:";
        private const string LINK_PREFIX = "link:";

        public SharedMemoryService(IDistributedCache cache, ILogger<SharedMemoryService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<string> StoreProductMemory(string userId, object productData)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                var productJson = JsonSerializer.Serialize(productData);
                var productId = Guid.NewGuid().ToString();
                var key = $"{PRODUCT_PREFIX}{userId}:{productId}";
                
                // Зберегти інформацію про продукт
                await _cache.SetStringAsync(key, productJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // Зберігати місяць
                });
                
                // Зберегти перехресне посилання для швидкого пошуку всіх продуктів користувача
                var userProductsKey = $"{CROSS_REFERENCE_PREFIX}user:{userId}:products";
                var existingProductIdsJson = await _cache.GetStringAsync(userProductsKey);
                List<string> productIds = new List<string>();
                
                if (!string.IsNullOrEmpty(existingProductIdsJson))
                {
                    productIds = JsonSerializer.Deserialize<List<string>>(existingProductIdsJson) ?? new List<string>();
                }
                
                productIds.Add(productId);
                await _cache.SetStringAsync(userProductsKey, JsonSerializer.Serialize(productIds), 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                    });
                
                // Зберегти запис про останню активність
                await _cache.SetStringAsync($"last:product:{userId}", productId, 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                    });
                
                return productId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing product memory for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> StoreChatMemory(string userId, object chatData)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                var chatJson = JsonSerializer.Serialize(chatData);
                var chatId = Guid.NewGuid().ToString();
                var key = $"{CHAT_PREFIX}{userId}:{chatId}";
                
                await _cache.SetStringAsync(key, chatJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) // Зберігати тиждень
                });
                
                // Зберегти перехресне посилання для швидкого пошуку всіх чатів користувача
                var userChatsKey = $"{CROSS_REFERENCE_PREFIX}user:{userId}:chats";
                var existingChatIdsJson = await _cache.GetStringAsync(userChatsKey);
                List<string> chatIds = new List<string>();
                
                if (!string.IsNullOrEmpty(existingChatIdsJson))
                {
                    chatIds = JsonSerializer.Deserialize<List<string>>(existingChatIdsJson) ?? new List<string>();
                }
                
                chatIds.Add(chatId);
                await _cache.SetStringAsync(userChatsKey, JsonSerializer.Serialize(chatIds), 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                    });
                
                // Зберегти запис про останню активність
                await _cache.SetStringAsync($"last:chat:{userId}", chatId, 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                    });
                
                return chatId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing chat memory for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> StoreInstagramMemory(string userId, object instagramData)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                var instagramJson = JsonSerializer.Serialize(instagramData);
                var instagramId = Guid.NewGuid().ToString();
                var key = $"{INSTAGRAM_PREFIX}{userId}:{instagramId}";
                
                await _cache.SetStringAsync(key, instagramJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) // Зберігати два тижні
                });
                
                // Зберегти перехресне посилання
                var userInstagramKey = $"{CROSS_REFERENCE_PREFIX}user:{userId}:instagram";
                var existingInstagramIdsJson = await _cache.GetStringAsync(userInstagramKey);
                List<string> instagramIds = new List<string>();
                
                if (!string.IsNullOrEmpty(existingInstagramIdsJson))
                {
                    instagramIds = JsonSerializer.Deserialize<List<string>>(existingInstagramIdsJson) ?? new List<string>();
                }
                
                instagramIds.Add(instagramId);
                await _cache.SetStringAsync(userInstagramKey, JsonSerializer.Serialize(instagramIds), 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14)
                    });
                
                // Зберегти запис про останню активність
                await _cache.SetStringAsync($"last:instagram:{userId}", instagramId, 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(3)
                    });
                
                return instagramId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing Instagram memory for user {UserId}", userId);
                throw;
            }
        }

        public async Task<T?> GetProductMemory<T>(string userId, string productId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                var key = $"{PRODUCT_PREFIX}{userId}:{productId}";
                var productJson = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(productJson))
                {
                    return default;
                }
                
                return JsonSerializer.Deserialize<T>(productJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product memory for user {UserId} and product {ProductId}", userId, productId);
                throw;
            }
        }

        public async Task<T?> GetChatMemory<T>(string userId, string chatId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                var key = $"{CHAT_PREFIX}{userId}:{chatId}";
                var chatJson = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(chatJson))
                {
                    return default;
                }
                
                return JsonSerializer.Deserialize<T>(chatJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat memory for user {UserId} and chat {ChatId}", userId, chatId);
                throw;
            }
        }

        public async Task<T?> GetInstagramMemory<T>(string userId, string instagramId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                var key = $"{INSTAGRAM_PREFIX}{userId}:{instagramId}";
                var instagramJson = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(instagramJson))
                {
                    return default;
                }
                
                return JsonSerializer.Deserialize<T>(instagramJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Instagram memory for user {UserId} and Instagram {InstagramId}", userId, instagramId);
                throw;
            }
        }

        public async Task<object> GetUserContext(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                // Отримуємо останню активність з кожної підсистеми
                var lastProductId = await _cache.GetStringAsync($"last:product:{userId}");
                var lastChatId = await _cache.GetStringAsync($"last:chat:{userId}");
                var lastInstagramId = await _cache.GetStringAsync($"last:instagram:{userId}");
                
                // Отримуємо останні дані з усіх підсистем для формування контексту
                var latestProduct = string.IsNullOrEmpty(lastProductId) ? null : 
                    await GetProductMemory<object>(userId, lastProductId);
                var latestChat = string.IsNullOrEmpty(lastChatId) ? null : 
                    await GetChatMemory<object>(userId, lastChatId);
                var latestInstagram = string.IsNullOrEmpty(lastInstagramId) ? null : 
                    await GetInstagramMemory<object>(userId, lastInstagramId);
                
                // Формуємо загальний контекст користувача
                var context = new
                {
                    userId,
                    lastActivity = DateTime.UtcNow,
                    product = new
                    {
                        id = lastProductId,
                        data = latestProduct
                    },
                    chat = new
                    {
                        id = lastChatId,
                        data = latestChat
                    },
                    instagram = new
                    {
                        id = lastInstagramId,
                        data = latestInstagram
                    }
                };
                
                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user context for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> LinkMemories(string userId, string sourceId, string targetId, string sourceType, string targetType)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            try
            {
                // Створюємо двосторонні зв'язки між елементами пам'яті
                var linkKey1 = $"{LINK_PREFIX}{userId}:{sourceType}:{sourceId}:{targetType}";
                var linkKey2 = $"{LINK_PREFIX}{userId}:{targetType}:{targetId}:{sourceType}";
                
                await _cache.SetStringAsync(linkKey1, targetId, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                });
                
                await _cache.SetStringAsync(linkKey2, sourceId, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking memories for user {UserId}", userId);
                return false;
            }
        }
    }
} 