using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories
{
    public class AgentLongTermMemoryRepository : IAgentLongTermMemoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AgentLongTermMemoryRepository> _logger;

        public AgentLongTermMemoryRepository(IConfiguration configuration, ILogger<AgentLongTermMemoryRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<AgentLongTermMemory> GetByKeyAsync(Guid userId, string key)
        {
            _logger.LogDebug("Отримання довгострокової пам'яті за ключем: {Key} для користувача: {UserId}", key, userId);
            const string sql = "SELECT * FROM agent_long_term_memories WHERE user_id = @UserId AND key = @Key;";
            
            try
            {
                using var connection = CreateConnection();
                var memory = await connection.QueryFirstOrDefaultAsync<AgentLongTermMemory>(sql, new { UserId = userId, Key = key });
                
                if (memory != null)
                {
                    // Оновлюємо час останнього доступу
                    await UpdateLastAccessedAsync(userId, key);
                }
                
                return memory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні довгострокової пам'яті за ключем: {Key} для користувача: {UserId}", key, userId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentLongTermMemory>> GetAllForUserAsync(Guid userId)
        {
            _logger.LogDebug("Отримання всіх записів довгострокової пам'яті для користувача: {UserId}", userId);
            const string sql = "SELECT * FROM agent_long_term_memories WHERE user_id = @UserId ORDER BY priority DESC, created_at DESC;";
            
            try
            {
                using var connection = CreateConnection();
                return await connection.QueryAsync<AgentLongTermMemory>(sql, new { UserId = userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні всіх записів довгострокової пам'яті для користувача: {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentLongTermMemory>> GetByUserAndCategoryAsync(Guid userId, string category)
        {
            _logger.LogDebug("Отримання записів довгострокової пам'яті за категорією: {Category} для користувача: {UserId}", category, userId);
            const string sql = "SELECT * FROM agent_long_term_memories WHERE user_id = @UserId AND category = @Category ORDER BY priority DESC, created_at DESC;";
            
            try
            {
                using var connection = CreateConnection();
                return await connection.QueryAsync<AgentLongTermMemory>(sql, new { UserId = userId, Category = category });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні записів довгострокової пам'яті за категорією: {Category} для користувача: {UserId}", category, userId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentLongTermMemory>> GetExpiredMemoriesAsync(DateTime currentTime)
        {
            _logger.LogDebug("Отримання застарілих записів довгострокової пам'яті на дату: {CurrentTime}", currentTime);
            const string sql = "SELECT * FROM agent_long_term_memories WHERE expires_at IS NOT NULL AND expires_at <= @CurrentTime;";
            
            try
            {
                using var connection = CreateConnection();
                var memories = await connection.QueryAsync<AgentLongTermMemory>(sql, new { CurrentTime = currentTime });
                _logger.LogDebug("Отримано {Count} застарілих записів довгострокової пам'яті", memories.AsList().Count);
                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні застарілих записів довгострокової пам'яті");
                throw;
            }
        }

        public async Task<IEnumerable<AgentLongTermMemory>> GetAllMemoriesAsync()
        {
            _logger.LogDebug("Отримання всіх записів довгострокової пам'яті");
            const string sql = "SELECT * FROM agent_long_term_memories;";
            
            try
            {
                using var connection = CreateConnection();
                return await connection.QueryAsync<AgentLongTermMemory>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні всіх записів довгострокової пам'яті");
                throw;
            }
        }

        public async Task<AgentLongTermMemory> CreateAsync(AgentLongTermMemory memory)
        {
            _logger.LogDebug("Створення нового запису довгострокової пам'яті для ключа: {Key}, користувач: {UserId}", memory.Key, memory.UserId);
            const string sql = @"
                INSERT INTO agent_long_term_memories 
                (id, user_id, key, content, priority, category, created_at, expires_at, last_accessed)
                VALUES 
                (@Id, @UserId, @Key, @Content, @Priority, @Category, @CreatedAt, @ExpiresAt, @LastAccessed)
                RETURNING *;";
            
            try
            {
                using var connection = CreateConnection();
                
                // Встановлюємо значення за замовчуванням
                memory.Id = memory.Id == Guid.Empty ? Guid.NewGuid() : memory.Id;
                memory.CreatedAt = memory.CreatedAt == default ? DateTime.UtcNow : memory.CreatedAt;
                memory.LastAccessed = memory.LastAccessed ?? memory.CreatedAt;
                
                return await connection.QueryFirstOrDefaultAsync<AgentLongTermMemory>(sql, memory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні запису довгострокової пам'яті для ключа: {Key}, користувач: {UserId}", memory.Key, memory.UserId);
                throw;
            }
        }

        public async Task UpdateAsync(AgentLongTermMemory memory)
        {
            _logger.LogDebug("Оновлення запису довгострокової пам'яті для ключа: {Key}, користувач: {UserId}", memory.Key, memory.UserId);
            const string sql = @"
                UPDATE agent_long_term_memories
                SET content = @Content,
                    priority = @Priority,
                    category = @Category,
                    expires_at = @ExpiresAt,
                    last_accessed = @LastAccessed
                WHERE user_id = @UserId AND key = @Key;";
            
            try
            {
                using var connection = CreateConnection();
                await connection.ExecuteAsync(sql, memory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні запису довгострокової пам'яті для ключа: {Key}, користувач: {UserId}", memory.Key, memory.UserId);
                throw;
            }
        }

        public async Task DeleteAsync(Guid userId, string key)
        {
            _logger.LogDebug("Видалення запису довгострокової пам'яті для ключа: {Key}, користувач: {UserId}", key, userId);
            const string sql = "DELETE FROM agent_long_term_memories WHERE user_id = @UserId AND key = @Key;";
            
            try
            {
                using var connection = CreateConnection();
                await connection.ExecuteAsync(sql, new { UserId = userId, Key = key });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при видаленні запису довгострокової пам'яті для ключа: {Key}, користувач: {UserId}", key, userId);
                throw;
            }
        }

        public async Task UpdateLastAccessedAsync(Guid userId, string key)
        {
            _logger.LogDebug("Оновлення часу останнього доступу для ключа: {Key}, користувач: {UserId}", key, userId);
            const string sql = "UPDATE agent_long_term_memories SET last_accessed = @LastAccessed WHERE user_id = @UserId AND key = @Key;";
            
            try
            {
                using var connection = CreateConnection();
                await connection.ExecuteAsync(sql, new { UserId = userId, Key = key, LastAccessed = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при оновленні часу останнього доступу для ключа: {Key}, користувач: {UserId}", key, userId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentLongTermMemory>> SearchByContentAsync(Guid userId, string searchTerm)
        {
            _logger.LogDebug("Пошук записів довгострокової пам'яті за текстом: {SearchTerm}, користувач: {UserId}", searchTerm, userId);
            const string sql = "SELECT * FROM agent_long_term_memories WHERE user_id = @UserId AND content ILIKE @SearchTerm ORDER BY priority DESC, created_at DESC;";
            
            try
            {
                using var connection = CreateConnection();
                return await connection.QueryAsync<AgentLongTermMemory>(sql, new { UserId = userId, SearchTerm = $"%{searchTerm}%" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при пошуку записів довгострокової пам'яті за текстом: {SearchTerm}, користувач: {UserId}", searchTerm, userId);
                throw;
            }
        }
    }
} 