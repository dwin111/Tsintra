using Microsoft.AspNetCore.Mvc;
using Tsintra.MarketplaceAgent.Agents;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Tsintra.Domain.Interfaces;
using Tsintra.Application.Interfaces;
using Tsintra.Domain.Models;
using System.Diagnostics;

namespace Tsintra.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
   // [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly ChatAgent _chatAgent;
        private readonly IUserRepository _userRepository;
        private readonly IChatService _chatService;
        private readonly ILLMServices _llmServices;

        public ChatController(
            ILogger<ChatController> logger,
            ChatAgent chatAgent,
            IUserRepository userRepository,
            IChatService chatService,
            ILLMServices llmServices)
        {
            _logger = logger;
            _chatAgent = chatAgent;
            _userRepository = userRepository;
            _chatService = chatService;
            _llmServices = llmServices;
        }

        private async Task<Guid?> GetAuthorizedUserIdAsync()
        {
            if (!User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("Спроба доступу від неавторизованого користувача");
                return null;
            }

            var googleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(googleId))
            {
                _logger.LogWarning("Не знайдено GoogleId (NameIdentifier) у клеймах користувача");
                return null;
            }

            _logger.LogInformation("Знайдено GoogleId: {GoogleId} у клеймах", googleId);

            var user = await _userRepository.GetByIdAsync(Guid.Parse(googleId));
            if (user == null)
            {
                _logger.LogWarning("Користувач з GoogleId {GoogleId} не знайдений у базі даних", googleId);
                return null;
            }

            _logger.LogInformation("Знайдено користувача з ID: {UserId} для GoogleId: {GoogleId}", user.Id, googleId);
            return user.Id;
        }

        private async Task<Guid> GetOrCreateGlobalConversationAsync(Guid userId)
        {
            try
            {
                // Спочатку перевіряємо, чи вже існує глобальна розмова для користувача
                // Шукаємо розмову з заголовком "Глобальна розмова"
                var userConversations = await _chatService.GetUserConversationsAsync(userId);
                var globalConversation = userConversations
                    .FirstOrDefault(c => c.Title == "Глобальна розмова");

                if (globalConversation != null)
                {
                    _logger.LogInformation("Знайдено існуючу глобальну розмову {ConversationId} для користувача {UserId}", 
                        globalConversation.Id, userId);
                    return globalConversation.Id;
                }

                // Якщо глобальної розмови немає, створюємо нову
                var newConversation = await _chatService.CreateConversationAsync(userId, "Глобальна розмова");
                _logger.LogInformation("Створено нову глобальну розмову {ConversationId} для користувача {UserId}", 
                    newConversation.Id, userId);
                return newConversation.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні/створенні глобальної розмови для користувача {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Ініціює або продовжує текстовий діалог з агентом.
        /// </summary>
        /// <param name="request">Дані запиту з повідомленням та ідентифікаторами</param>
        /// <param name="cancellationToken">Токен скасування</param>
        /// <returns>Відповідь агента</returns>
        [HttpPost("message")]
        public async Task<IActionResult> SendMessage(
            [FromForm] ChatMessageRequest request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var timings = new Dictionary<string, long>();
            var totalStopwatch = Stopwatch.StartNew();

            try
            {
                var userId = await GetAuthorizedUserIdAsync();
                timings.Add("GetAuthorizedUserId", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();
                
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                _logger.LogInformation("Отримано запит на обробку повідомлення. UserId: {UserId}", userId);

                if (string.IsNullOrEmpty(request.Message) && (request.Images == null || !request.Images.Any()))
                {
                    return BadRequest("Повідомлення не може бути порожнім, якщо не надіслані зображення");
                }

                // Create or get global conversation for user
                var conversationId = await GetOrCreateGlobalConversationAsync(userId.Value);
                timings.Add("GetOrCreateGlobalConversation", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();
                
                _logger.LogInformation("Використовуємо глобальну розмову {ConversationId} для користувача {UserId}", 
                    conversationId, userId.Value);

                // Save user message to database
                await _chatService.AddUserMessageAsync(conversationId, request.Message ?? string.Empty);
                timings.Add("SaveUserMessage", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();
                
                _logger.LogInformation("Збережено повідомлення користувача для розмови {ConversationId}", conversationId);

                // Process the message with the agent
                _chatAgent.SetClientUserId(userId.Value);
                timings.Add("SetClientUserId", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();
                
                var response = await _chatAgent.ProcessMessageAsync(
                    request.Message ?? string.Empty,
                    request.Images?.ToList() ?? new List<IFormFile>(),
                    conversationId.ToString(),
                    cancellationToken);
                timings.Add("ProcessMessageAsync", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();

                // Save agent response to database
                await _chatService.AddAssistantMessageAsync(conversationId, response);
                timings.Add("SaveAssistantMessage", stopwatch.ElapsedMilliseconds);
                stopwatch.Stop();
                
                _logger.LogInformation("Збережено відповідь агента для розмови {ConversationId}", conversationId);

                totalStopwatch.Stop();
                timings.Add("TotalProcessingTime", totalStopwatch.ElapsedMilliseconds);
                
                // Log performance metrics
                _logger.LogInformation("Заміри часу виконання (мс): {Timings}", 
                    string.Join(", ", timings.Select(t => $"{t.Key}={t.Value}")));

                return Ok(new
                {
                    Response = response
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                totalStopwatch.Stop();
                _logger.LogError(ex, "Помилка при обробці повідомлення. Загальний час: {TotalTime}мс", totalStopwatch.ElapsedMilliseconds);
                return StatusCode(500, $"Сталася помилка при обробці повідомлення: {ex.Message}");
            }
        }

        /// <summary>
        /// Генерує аналітику ринку на основі попереднього діалогу.
        /// </summary>
        /// <param name="cancellationToken">Токен скасування</param>
        /// <returns>Аналітика ринку у вигляді тексту</returns>
        [HttpPost("market-analysis")]
        public async Task<IActionResult> GenerateMarketAnalysis(
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                _logger.LogInformation("Отримано запит на генерацію аналітики ринку. UserId: {UserId}",
                    userId);

                _chatAgent.SetClientUserId(userId.Value);
                
                // Використовуємо глобальну розмову без передачі конкретного ConversationId
                var analysis = await _chatAgent.GenerateMarketAnalysisAsync(null, cancellationToken);

                return Ok(new
                {
                    Analysis = analysis
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при генерації аналітики ринку");
                return StatusCode(500, $"Сталася помилка при генерації аналітики: {ex.Message}");
            }
        }

        // Методи з Tsintra.WebApi.Controllers.ChatController

        [HttpGet("conversations")]
        public async Task<IActionResult> GetUserConversations()
        {
            try
            {
                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                var conversations = await _chatService.GetUserConversationsAsync(userId.Value);
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні розмов користувача");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("conversations/{conversationId}")]
        public async Task<IActionResult> GetConversation(Guid conversationId)
        {
            try
            {
                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                var conversation = await _chatService.GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    return NotFound($"Розмова з ID {conversationId} не знайдена");
                }

                if (conversation.UserId != userId.Value)
                {
                    return Forbid("У вас немає прав доступу до цієї розмови");
                }

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні розмови {ConversationId}", conversationId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost("conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest("Заголовок є обов'язковим");
                }

                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                var conversation = await _chatService.CreateConversationAsync(userId.Value, request.Title);
                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні розмови");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("conversations/{conversationId}/title")]
        public async Task<IActionResult> UpdateConversationTitle(Guid conversationId, [FromBody] UpdateTitleRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest("Заголовок є обов'язковим");
                }

                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                var conversation = await _chatService.GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    return NotFound($"Розмова з ID {conversationId} не знайдена");
                }

                if (conversation.UserId != userId.Value)
                {
                    return Forbid("У вас немає прав для зміни цієї розмови");
                }

                var updatedConversation = await _chatService.UpdateConversationTitleAsync(conversationId, request.Title);
                return Ok(updatedConversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні заголовка розмови {ConversationId}", conversationId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpDelete("conversations/{conversationId}")]
        public async Task<IActionResult> DeleteConversation(Guid conversationId)
        {
            try
            {
                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                var conversation = await _chatService.GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    return NotFound($"Розмова з ID {conversationId} не знайдена");
                }

                if (conversation.UserId != userId.Value)
                {
                    return Forbid("У вас немає прав для видалення цієї розмови");
                }

                await _chatService.DeleteConversationAsync(conversationId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при видаленні розмови {ConversationId}", conversationId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<IActionResult> GetConversationMessages(Guid conversationId)
        {
            try
            {
                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                var conversation = await _chatService.GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    return NotFound($"Розмова з ID {conversationId} не знайдена");
                }

                if (conversation.UserId != userId.Value)
                {
                    return Forbid("У вас немає прав для перегляду повідомлень цієї розмови");
                }

                var messages = await _chatService.GetConversationHistoryAsync(conversationId);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні повідомлень розмови {ConversationId}", conversationId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("conversations/{conversationId}/messages/paginated")]
        public async Task<IActionResult> GetPaginatedMessages(
            Guid conversationId, 
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string role = null,
            [FromQuery] string searchText = null,
            [FromQuery] string orderBy = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = await GetAuthorizedUserIdAsync();
                if (!userId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                var conversation = await _chatService.GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    return NotFound($"Розмова з ID {conversationId} не знайдена");
                }

                if (conversation.UserId != userId.Value)
                {
                    return Forbid("У вас немає прав для перегляду повідомлень цієї розмови");
                }

                var paginatedMessages = await _chatService.GetPaginatedMessagesAsync(
                    conversationId, new MessageQueryOptions
                    {
                        FromDate = fromDate,
                        ToDate = toDate,
                        Role = role,
                        SearchText = searchText,
                        OrderBy = orderBy,
                        Skip = (page - 1) * pageSize,
                        Take = pageSize
                    });
                    
                return Ok(paginatedMessages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні сторінки повідомлень розмови {ConversationId}", conversationId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("history/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserChatHistory(Guid userId)
        {
            try
            {
                // Тільки адміністратори можуть переглядати історію чату інших користувачів
                var currentUserId = await GetAuthorizedUserIdAsync();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
                }

                // Отримання всіх розмов користувача безпосередньо
                var conversations = await _chatService.GetUserConversationsAsync(userId);
                if (conversations == null || !conversations.Any())
                {
                    return NotFound($"Розмови користувача з ID {userId} не знайдені");
                }

                // Підготовка результату з деталями про кожну розмову
                var result = new List<object>();
                foreach (var conversation in conversations)
                {
                    var messages = await _chatService.GetConversationHistoryAsync(conversation.Id);
                    result.Add(new
                    {
                        Conversation = conversation,
                        Messages = messages,
                        MessageCount = messages.Count
                    });
                }

                return Ok(new 
                {
                    UserId = userId,
                    ConversationsCount = conversations.Count,
                    Conversations = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні історії чату користувача {UserId}", userId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }
    }

    /// <summary>
    /// Запит для надсилання повідомлення агенту
    /// </summary>
    public class ChatMessageRequest
    {
        /// <summary>
        /// Текст повідомлення
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Зображення (опціонально)
        /// </summary>
        public IFormFile[]? Images { get; set; }
    }

    // Класи запитів з WebApi
    public class CreateConversationRequest
    {
        public string Title { get; set; }
    }

    public class UpdateTitleRequest
    {
        public string Title { get; set; }
    }

    public class SendMessageRequest
    {
        public string Content { get; set; }
    }
} 