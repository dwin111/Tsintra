using System;
using System.Threading.Tasks;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace Tsintra.Application.Services;

public class AgentMemoryService : IAgentMemoryService
{
    private readonly ICacheService _cacheService;
    private readonly IAgentMemoryRepository _memoryRepository;
    private readonly ILogger<AgentMemoryService> _logger;
    private readonly string _keyPrefix = "agent:memory:";
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromDays(30);
    private readonly IDistributedCache _cache;

    public AgentMemoryService(
        ICacheService cacheService, 
        IAgentMemoryRepository memoryRepository,
        ILogger<AgentMemoryService> logger,
        IDistributedCache cache)
    {
        _cacheService = cacheService;
        _memoryRepository = memoryRepository;
        _logger = logger;
        _cache = cache;
    }

    public async Task<AgentMemory> GetMemoryAsync(Guid userId, string conversationId)
    {
        var stopwatch = Stopwatch.StartNew();
        var timings = new Dictionary<string, long>();
        
        try
        {
            var key = GetKey(userId, conversationId);
            _logger.LogDebug("Отримання пам'яті за ключем: {Key}", key);
            
            // Спочатку перевіряємо кеш
            try
            {
                var cachedMemory = await _cacheService.GetAsync<AgentMemory>(key);
                timings.Add("GetFromCache", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();
                
                if (cachedMemory != null)
                {
                    _logger.LogDebug("Знайдено запис пам'яті в кеші для ключа: {Key}", key);
                    return cachedMemory;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Помилка при отриманні пам'яті з кешу для ключа: {Key}", key);
                // Продовжуємо виконання і намагаємося отримати з бази даних
            }
            
            // Якщо в кеші немає, звертаємося до бази даних
            var memory = await _memoryRepository.GetByConversationIdAsync(userId, conversationId);
            timings.Add("GetFromDatabase", stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
            
            if (memory != null)
            {
                _logger.LogDebug("Знайдено запис пам'яті в базі даних для UserId: {UserId}, ConversationId: {ConversationId}", 
                    userId, conversationId);
                    
                // Кешуємо на майбутнє
                try
                {
                    await _cacheService.SetAsync(key, memory, _defaultExpiry);
                    _logger.LogDebug("Збережено запис пам'яті в кеш для ключа: {Key}", key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Помилка при збереженні пам'яті в кеш для ключа: {Key}", key);
                }
            }
            else
            {
                _logger.LogDebug("Запис пам'яті не знайдено для UserId: {UserId}, ConversationId: {ConversationId}", 
                    userId, conversationId);
            }
            
            // Логуємо продуктивність, якщо операція зайняла більше 50мс
            var totalTime = timings.Values.Sum();
            if (totalTime > 50)
            {
                _logger.LogInformation("Заміри часу GetMemoryAsync (мс): {Timings}, Загалом: {Total}мс", 
                    string.Join(", ", timings.Select(t => $"{t.Key}={t.Value}")), totalTime);
            }
            
            return memory;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Помилка при отриманні пам'яті для UserId: {UserId}, ConversationId: {ConversationId}. Час: {ElapsedTime}мс", 
                userId, conversationId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task SaveMemoryAsync(AgentMemory memory)
    {
        if (memory == null)
        {
            throw new ArgumentNullException(nameof(memory));
        }
        
        var stopwatch = Stopwatch.StartNew();
        var timings = new Dictionary<string, long>();
        
        _logger.LogDebug("Збереження пам'яті для UserId: {UserId}, ConversationId: {ConversationId}",
            memory.UserId, memory.ConversationId);
            
        try 
        {
            // Зберегти в кеш
            var key = GetKey(memory.UserId, memory.ConversationId);
            var expiryTime = memory.ExpiresAt.HasValue 
                ? memory.ExpiresAt.Value - DateTime.UtcNow 
                : _defaultExpiry;
                
            _logger.LogDebug("Зберігаємо пам'ять у кеш з ключем {Key} і терміном {ExpiryTime}", 
                key, expiryTime);
                
            try {
                await _cacheService.SetAsync(key, memory, expiryTime);
                _logger.LogDebug("Пам'ять успішно збережена в кеш");
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Помилка збереження пам'яті в кеш для ключа: {Key}", key);
            }
            
            timings.Add("SaveToCache", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();
            
            // Спочатку перевірити, чи існує запис у базі даних
            _logger.LogDebug("Перевіряємо, чи існує пам'ять у БД для UserId: {UserId}, ConversationId: {ConversationId}",
                memory.UserId, memory.ConversationId);
                
            var existingMemory = await _memoryRepository.GetByConversationIdAsync(memory.UserId, memory.ConversationId);
            
            timings.Add("CheckExistingMemory", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();
            
            if (existingMemory != null)
            {
                // Оновлюємо існуючий запис
                _logger.LogDebug("Знайдено існуючий запис пам'яті, оновлюємо його");
                
                existingMemory.Content = memory.Content;
                existingMemory.ExpiresAt = memory.ExpiresAt;
                
                await _memoryRepository.UpdateAsync(existingMemory);
                _logger.LogDebug("Оновлено запис пам'яті з ID: {MemoryId}", existingMemory.Id);
            }
            else
            {
                // Створюємо новий запис
                _logger.LogDebug("Не знайдено існуючого запису пам'яті, створюємо новий");
                
                await _memoryRepository.CreateAsync(memory);
                _logger.LogDebug("Створено новий запис пам'яті з ID: {MemoryId}", memory.Id);
            }
            
            timings.Add("SaveToDatabase", stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
            
            // Логуємо продуктивність, якщо операція зайняла більше 50мс
            var totalTime = timings.Values.Sum();
            if (totalTime > 50)
            {
                _logger.LogInformation("Заміри часу SaveMemoryAsync (мс): {Timings}, Загалом: {Total}мс", 
                    string.Join(", ", timings.Select(t => $"{t.Key}={t.Value}")), totalTime);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Помилка при збереженні пам'яті для UserId: {UserId}, ConversationId: {ConversationId}. Час: {ElapsedTime}мс", 
                memory.UserId, memory.ConversationId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task DeleteMemoryAsync(Guid userId, string conversationId)
    {
        _logger.LogDebug("Видалення пам'яті для UserId: {UserId}, ConversationId: {ConversationId}",
            userId, conversationId);
            
        // Видалити з кешу
        var key = GetKey(userId, conversationId);
        _logger.LogDebug("Видаляємо пам'ять з кешу за ключем: {Key}", key);
        
        try {
            await _cacheService.RemoveAsync(key);
            _logger.LogDebug("Пам'ять успішно видалена з кешу");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Помилка видалення пам'яті з кешу для ключа: {Key}", key);
        }
        
        // Видалити з бази даних
        _logger.LogDebug("Видаляємо пам'ять з БД для UserId: {UserId}, ConversationId: {ConversationId}",
            userId, conversationId);
            
        try {
            await _memoryRepository.DeleteAsync(userId, conversationId);
            _logger.LogDebug("Пам'ять успішно видалена з БД");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Помилка видалення пам'яті з БД для UserId: {UserId}, ConversationId: {ConversationId}",
                userId, conversationId);
            throw; // Прокидаємо помилку далі
        }
    }

    public async Task<bool> ExistsAsync(Guid userId, string conversationId)
    {
        var key = GetKey(userId, conversationId);
        _logger.LogDebug("Перевірка наявності пам'яті для UserId: {UserId}, ConversationId: {ConversationId}, Key: {Key}",
            userId, conversationId, key);
            
        // Спочатку перевірити кеш
        _logger.LogTrace("Перевіряємо наявність у кеші для ключа: {Key}", key);
        
        try {
            var existsInCache = await _cacheService.ExistsAsync(key);
            
            if (existsInCache)
            {
                _logger.LogDebug("Пам'ять знайдена в кеші");
                return true;
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Помилка перевірки наявності в кеші для ключа: {Key}", key);
        }
        
        // Якщо немає в кеші, перевірити базу даних
        _logger.LogDebug("Пам'ять відсутня в кеші, перевіряємо БД для UserId: {UserId}, ConversationId: {ConversationId}",
            userId, conversationId);
            
        try {
            var memoryFromDb = await _memoryRepository.GetByConversationIdAsync(userId, conversationId);
            var exists = memoryFromDb != null;
            _logger.LogDebug("Результат перевірки наявності в БД: {Exists}", exists);
            return exists;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Помилка перевірки наявності в БД для UserId: {UserId}, ConversationId: {ConversationId}",
                userId, conversationId);
            throw; // Прокидаємо помилку далі
        }
    }

    private string GetKey(Guid userId, string conversationId)
    {
        return $"{_keyPrefix}{userId}:{conversationId}";
    }
    
    /// <summary>
    /// Отримує всі записи пам'яті для конкретної розмови та структурує їх для використання в промті
    /// </summary>
    /// <param name="userId">ID користувача</param>
    /// <param name="conversationId">ID розмови</param>
    /// <returns>Структурований опис вмісту пам'яті для промту</returns>
    public async Task<string> GetMemoryPromptContextAsync(Guid userId, string conversationId)
    {
        _logger.LogDebug("Формування контексту пам'яті для промту. UserId: {UserId}, ConversationId: {ConversationId}", 
            userId, conversationId);
        
        try
        {
            // Отримуємо запис пам'яті для цієї розмови
            var memory = await GetMemoryAsync(userId, conversationId);
            
            if (memory == null || string.IsNullOrEmpty(memory.Content))
            {
                _logger.LogDebug("Не знайдено запису пам'яті для розмови {ConversationId}", conversationId);
                return string.Empty;
            }
            
            _logger.LogDebug("Знайдено запис пам'яті для розмови, розпочинаю обробку");
            
            // Будуємо контекст для LLM
            var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine("=== ІСТОРІЯ РОБОТИ АГЕНТА ===");
            
            try
            {
                // Спробуємо десеріалізувати як список дій (новий формат)
                var actions = System.Text.Json.JsonSerializer.Deserialize<List<object>>(memory.Content);
                
                if (actions != null && actions.Any())
                {
                    _logger.LogDebug("Знайдено {Count} дій у пам'яті", actions.Count);
                    
                    // Знаходимо всі повідомлення для фільтрації
                    var messageActions = actions
                        .Where(a => {
                            try {
                                var actionJson = System.Text.Json.JsonSerializer.Serialize(a);
                                var actionElement = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(actionJson);
                                return actionElement.TryGetProperty("Action", out var actionNameElem) && 
                                      actionNameElem.GetString() == "Message";
                            } catch {
                                return false;
                            }
                        })
                        .ToList();
                    
                    // Якщо повідомлень більше 15, залишаємо лише останні
                    int messageLimit = 15;
                    if (messageActions.Count > messageLimit)
                    {
                        _logger.LogDebug("Обмежуємо історію повідомлень до {Limit} останніх (всього: {Total})", 
                            messageLimit, messageActions.Count);
                        
                        // Отримуємо ID повідомлень, які треба зберегти (останні 15)
                        var messageActionsToKeep = messageActions
                            .Skip(Math.Max(0, messageActions.Count - messageLimit))
                            .ToList();
                        
                        // Фільтруємо всі дії - залишаємо не-повідомлення та останні N повідомлень
                        actions = actions
                            .Where(a => {
                                try {
                                    var actionJson = System.Text.Json.JsonSerializer.Serialize(a);
                                    var actionElement = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(actionJson);
                                    if (actionElement.TryGetProperty("Action", out var actionNameElem) && 
                                        actionNameElem.GetString() == "Message") {
                                        // Це повідомлення - перевіряємо чи воно в списку для збереження
                                        return messageActionsToKeep.Contains(a);
                                    }
                                    // Це не повідомлення - залишаємо
                                    return true;
                                } catch {
                                    return true; // У разі помилки залишаємо дію
                                }
                            })
                            .ToList();
                    }
                    
                    foreach (var actionObj in actions)
                    {
                        try
                        {
                            // Конвертуємо об'єкт у JsonElement для доступу до властивостей
                            var actionJson = System.Text.Json.JsonSerializer.Serialize(actionObj);
                            var actionElement = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(actionJson);
                            
                            if (actionElement.TryGetProperty("Action", out var actionNameElem) && 
                                actionElement.TryGetProperty("Timestamp", out var timestampElem))
                            {
                                var actionName = actionNameElem.GetString() ?? "Unknown";
                                DateTime timestamp = timestampElem.ValueKind == JsonValueKind.String
                                    ? DateTime.Parse(timestampElem.GetString())
                                    : timestampElem.GetDateTime();
                                    
                                // Додаємо інформацію про дію
                                promptBuilder.AppendLine($"--- {actionName} ({timestamp:yyyy-MM-dd HH:mm:ss}) ---");
                                
                                // Обробка специфічних даних
                                if (actionElement.TryGetProperty("Data", out var dataElem))
                                {
                                    ProcessActionData(promptBuilder, actionName, dataElem);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Помилка обробки дії з пам'яті");
                            promptBuilder.AppendLine("[Помилка обробки дії]");
                        }
                    }
                }
                else
                {
                    // Запис є, але не у форматі списку - можливо старий формат одиночної дії
                    _logger.LogDebug("Не вдалося десеріалізувати як список дій, спроба обробки як одиночної дії");
                    
                    // Пробуємо обробити як одиночну дію (старий формат)
                    try
                    {
                        var singleAction = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(memory.Content);
                        if (singleAction.TryGetProperty("Action", out var actionNameElem) && 
                            singleAction.TryGetProperty("Timestamp", out var timestampElem))
                        {
                            var actionName = actionNameElem.GetString() ?? "Unknown";
                            DateTime timestamp = timestampElem.ValueKind == JsonValueKind.String
                                ? DateTime.Parse(timestampElem.GetString())
                                : timestampElem.GetDateTime();
                                
                            promptBuilder.AppendLine($"--- {actionName} ({timestamp:yyyy-MM-dd HH:mm:ss}) ---");
                            
                            if (singleAction.TryGetProperty("Data", out var dataElem))
                            {
                                ProcessActionData(promptBuilder, actionName, dataElem);
                            }
                        }
                        else
                        {
                            promptBuilder.AppendLine("[Формат пам'яті не розпізнано]");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Помилка обробки одиночної дії");
                        promptBuilder.AppendLine("[Помилка обробки пам'яті]");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Помилка десеріалізації контенту пам'яті");
                promptBuilder.AppendLine("[Помилка обробки вмісту пам'яті]");
            }
            
            promptBuilder.AppendLine("=== КІНЕЦЬ ІСТОРІЇ АГЕНТА ===");
            
            var result = promptBuilder.ToString();
            _logger.LogDebug("Сформовано контекст пам'яті розміром {Size} символів", result.Length);
            
            // Логування повного вмісту промпту для налагодження
            _logger.LogInformation("Повний сформований промпт пам'яті:\n{Prompt}", result);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка формування контексту пам'яті для промту");
            return string.Empty;
        }
    }

    // Допоміжний метод для обробки даних дії
    private void ProcessActionData(StringBuilder builder, string actionName, JsonElement dataElement)
    {
        // Особлива обробка для різних типів даних
        if (actionName == "Message")
        {
            // Особлива обробка повідомлень в чаті
            if (dataElement.TryGetProperty("Text", out var textElem) || 
                dataElement.TryGetProperty("Message", out textElem) ||
                dataElement.TryGetProperty("Content", out textElem))
            {
                var messageText = textElem.GetString();
                if (!string.IsNullOrEmpty(messageText))
                {
                    // Визначаємо відправника повідомлення (користувач чи асистент)
                    string sender = "Повідомлення";
                    if (dataElement.TryGetProperty("IsUserMessage", out var isUserElem) && isUserElem.GetBoolean())
                    {
                        sender = "Користувач";
                    }
                    else if (dataElement.TryGetProperty("IsFromAssistant", out var isAssistantElem) && isAssistantElem.GetBoolean())
                    {
                        sender = "Асистент";
                    }
                    
                    builder.AppendLine($"{sender}: {messageText}");
                }
            }
        }
        else if (actionName == "CompleteProductGeneration")
        {
            // Додаємо загальний підсумок генерації
            if (dataElement.TryGetProperty("ProductName", out var prodNameElem))
            {
                builder.AppendLine($"Успішно згенеровано продукт: {prodNameElem.GetString()}");
            }
            
            if (dataElement.TryGetProperty("TotalTimeMs", out var timeElem))
            {
                builder.AppendLine($"Загальний час: {timeElem.GetInt64()} мс");
            }
            
            if (dataElement.TryGetProperty("ImagesCount", out var imgCountElem))
            {
                builder.AppendLine($"Кількість зображень: {imgCountElem.GetInt32()}");
            }
        }
        else if (actionName.StartsWith("CompletedTool_"))
        {
            // Витягуємо назву інструменту
            var toolName = actionName.Substring("CompletedTool_".Length);
            
            // Додаємо результати інструменту
            builder.AppendLine($"Інструмент: {toolName}");
            
            if (dataElement.TryGetProperty("Result", out JsonElement toolResult))
            {
                // Перевіряємо тип результату і форматуємо відповідно
                if (toolResult.ValueKind == JsonValueKind.String)
                {
                    var resultStr = toolResult.GetString();
                    // Якщо це JSON, спробуємо його розпарсити
                    if (resultStr != null && (resultStr.StartsWith("{") || resultStr.StartsWith("[")))
                    {
                        try
                        {
                            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(resultStr);
                            builder.AppendLine(System.Text.Json.JsonSerializer.Serialize(jsonObj, 
                                new JsonSerializerOptions { WriteIndented = true }));
                        }
                        catch
                        {
                            // Якщо не вдалося розпарсити, додаємо як є
                            builder.AppendLine(resultStr);
                        }
                    }
                    else if (resultStr != null)
                    {
                        // Для звичайного тексту обмежуємо довжину
                        builder.AppendLine(resultStr.Length > 500 ? resultStr.Substring(0, 500) + "..." : resultStr);
                    }
                }
                else
                {
                    // Для інших типів - серіалізуємо з відступами
                    builder.AppendLine(System.Text.Json.JsonSerializer.Serialize(toolResult, 
                        new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            else if (dataElement.TryGetProperty("ElapsedMs", out var elapsedElem))
            {
                builder.AppendLine($"Час виконання: {elapsedElem.GetInt64()} мс");
            }
        }
        else if (actionName.StartsWith("ErrorTool_"))
        {
            // Витягуємо назву інструменту з помилкою
            var toolName = actionName.Substring("ErrorTool_".Length);
            
            builder.AppendLine($"Помилка в інструменті: {toolName}");
            
            if (dataElement.TryGetProperty("ErrorMessage", out var errorMsgElem))
            {
                builder.AppendLine($"Повідомлення: {errorMsgElem.GetString()}");
            }
        }
        else
        {
            // Для інших типів дій - просто серіалізуємо дані
            builder.AppendLine(System.Text.Json.JsonSerializer.Serialize(dataElement, 
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
} 