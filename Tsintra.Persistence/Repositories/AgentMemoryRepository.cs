using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories
{
    public class AgentMemoryRepository : IAgentMemoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AgentMemoryRepository> _logger;

        public AgentMemoryRepository(IConfiguration configuration, ILogger<AgentMemoryRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<AgentMemory> GetByConversationIdAsync(Guid userId, string conversationId)
        {
            _logger.LogDebug("Спроба знайти пам'ять агента за ConversationId: {ConversationId} та UserId: {UserId}", conversationId, userId);
            const string sql = "SELECT * FROM \"AgentMemories\" WHERE \"UserId\" = @UserId AND \"ConversationId\" = @ConversationId LIMIT 1;";
            
            try
            {
                using var connection = CreateConnection();
                var memory = await connection.QuerySingleOrDefaultAsync<AgentMemory>(
                    sql, 
                    new { UserId = userId, ConversationId = conversationId }
                );
                
                if (memory == null)
                {
                    _logger.LogDebug("Пам'ять агента не знайдена для ConversationId: {ConversationId}", conversationId);
                }
                else
                {
                    _logger.LogDebug("Пам'ять агента знайдена для ConversationId: {ConversationId}", conversationId);
                }
                
                return memory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при пошуку пам'яті агента за ConversationId: {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentMemory>> GetAllForUserAsync(Guid userId)
        {
            _logger.LogDebug("Отримання всіх записів пам'яті агента для користувача: {UserId}", userId);
            const string sql = "SELECT * FROM \"AgentMemories\" WHERE \"UserId\" = @UserId ORDER BY \"CreatedAt\" DESC;";
            
            try
            {
                using var connection = CreateConnection();
                var memories = await connection.QueryAsync<AgentMemory>(sql, new { UserId = userId });
                _logger.LogDebug("Отримано {Count} записів пам'яті агента для користувача: {UserId}", memories.Count(), userId);
                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні записів пам'яті агента для користувача: {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentMemory>> GetAllMemoriesAsync()
        {
            _logger.LogDebug("Отримання всіх записів пам'яті агента");
            const string sql = "SELECT * FROM \"AgentMemories\" ORDER BY \"CreatedAt\" DESC;";
            
            try
            {
                using var connection = CreateConnection();
                var memories = await connection.QueryAsync<AgentMemory>(sql);
                _logger.LogDebug("Отримано {Count} записів пам'яті агента", memories.Count());
                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні всіх записів пам'яті агента");
                throw;
            }
        }

        public async Task<IEnumerable<AgentMemory>> GetExpiredMemoriesAsync(DateTime currentTime)
        {
            _logger.LogDebug("Отримання застарілих записів пам'яті агента на дату: {CurrentTime}", currentTime);
            const string sql = "SELECT * FROM \"AgentMemories\" WHERE \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" <= @CurrentTime;";
            
            try
            {
                using var connection = CreateConnection();
                var memories = await connection.QueryAsync<AgentMemory>(sql, new { CurrentTime = currentTime });
                _logger.LogDebug("Отримано {Count} застарілих записів пам'яті агента", memories.Count());
                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні застарілих записів пам'яті агента");
                throw;
            }
        }

        public async Task<AgentMemory> CreateAsync(AgentMemory memory)
        {
            _logger.LogDebug("Створення нового запису пам'яті агента для ConversationId: {ConversationId}", memory.ConversationId);
            const string sql = @"
                INSERT INTO ""AgentMemories"" (""Id"", ""UserId"", ""ConversationId"", ""Content"", ""CreatedAt"", ""ExpiresAt"")
                VALUES (@Id, @UserId, @ConversationId, @Content, @CreatedAt, @ExpiresAt)
                RETURNING *;";
            
            try
            {
                using var connection = CreateConnection();
                memory.Id = memory.Id == Guid.Empty ? Guid.NewGuid() : memory.Id;
                memory.CreatedAt = memory.CreatedAt == default ? DateTime.UtcNow : memory.CreatedAt;

                var createdMemory = await connection.QuerySingleAsync<AgentMemory>(sql, memory);
                _logger.LogInformation("Успішно створено запис пам'яті агента з ID: {Id}", createdMemory.Id);
                return createdMemory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні запису пам'яті агента для ConversationId: {ConversationId}", memory.ConversationId);
                throw;
            }
        }

        public async Task UpdateAsync(AgentMemory memory)
        {
            _logger.LogDebug("Оновлення запису пам'яті агента з ID: {Id}", memory.Id);
            const string sql = @"
                UPDATE ""AgentMemories""
                SET ""Content"" = @Content,
                    ""ExpiresAt"" = @ExpiresAt
                WHERE ""Id"" = @Id;";
            
            try
            {
                using var connection = CreateConnection();
                int affectedRows = await connection.ExecuteAsync(sql, memory);
                _logger.LogDebug("Запис пам'яті агента оновлено для ID: {Id}. Кількість змінених рядків: {AffectedRows}", memory.Id, affectedRows);
                
                if (affectedRows == 0)
                {
                    _logger.LogWarning("Операція оновлення зачепила 0 рядків для ID пам'яті агента: {Id}. Запис може не існувати.", memory.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні запису пам'яті агента з ID: {Id}", memory.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Guid userId, string conversationId)
        {
            _logger.LogDebug("Видалення запису пам'яті агента за ConversationId: {ConversationId} та UserId: {UserId}", conversationId, userId);
            const string sql = "DELETE FROM \"AgentMemories\" WHERE \"UserId\" = @UserId AND \"ConversationId\" = @ConversationId;";
            
            try
            {
                using var connection = CreateConnection();
                int affectedRows = await connection.ExecuteAsync(sql, new { UserId = userId, ConversationId = conversationId });
                _logger.LogDebug("Запис пам'яті агента видалено. Кількість змінених рядків: {AffectedRows}", affectedRows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при видаленні запису пам'яті агента за ConversationId: {ConversationId}", conversationId);
                throw;
            }
        }
    }
} 