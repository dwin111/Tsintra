using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories
{
    public class MemoryRepository : IAgentMemoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<MemoryRepository> _logger;

        public MemoryRepository(IConfiguration configuration, ILogger<MemoryRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<AgentMemory> GetByConversationIdAsync(Guid userId, string conversationId)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                const string sql = @"
                    SELECT * FROM ""AgentMemories""
                    WHERE ""UserId"" = @UserId AND ""ConversationId"" = @ConversationId
                    LIMIT 1;";

                using var connection = CreateConnection();
                var memory = await connection.QueryFirstOrDefaultAsync<AgentMemory>(sql, 
                    new { UserId = userId, ConversationId = conversationId });
                    
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > 50)
                {
                    _logger.LogInformation("GetByConversationIdAsync зайняло {ElapsedMs}мс для UserId: {UserId}, ConversationId: {ConversationId}", 
                        elapsed, userId, conversationId);
                }
                
                return memory;
            }
            catch (Exception ex)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Помилка при отриманні пам'яті з БД для UserId: {UserId}, ConversationId: {ConversationId}. Час: {ElapsedMs}мс", 
                    userId, conversationId, elapsed);
                throw;
            }
        }

        public async Task<AgentMemory> CreateAsync(AgentMemory memory)
        {
            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                const string sql = @"
                    INSERT INTO ""AgentMemories"" (""Id"", ""UserId"", ""ConversationId"", ""Content"", ""CreatedAt"", ""ExpiresAt"")
                    VALUES (@Id, @UserId, @ConversationId, @Content, @CreatedAt, @ExpiresAt)
                    RETURNING *;";

                using var connection = CreateConnection();
                
                // Ensure ID is set
                if (memory.Id == Guid.Empty)
                {
                    memory.Id = Guid.NewGuid();
                }
                
                // Ensure CreatedAt is set
                if (memory.CreatedAt == default)
                {
                    memory.CreatedAt = DateTime.UtcNow;
                }

                var result = await connection.QuerySingleAsync<AgentMemory>(sql, memory);
                
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > 50)
                {
                    _logger.LogInformation("CreateAsync зайняло {ElapsedMs}мс для ID: {MemoryId}, UserId: {UserId}", 
                        elapsed, memory.Id, memory.UserId);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Помилка при створенні запису пам'яті для UserId: {UserId}, ConversationId: {ConversationId}. Час: {ElapsedMs}мс", 
                    memory.UserId, memory.ConversationId, elapsed);
                throw;
            }
        }

        public async Task UpdateAsync(AgentMemory memory)
        {
            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                const string sql = @"
                    UPDATE ""AgentMemories""
                    SET ""Content"" = @Content,
                        ""ExpiresAt"" = @ExpiresAt
                    WHERE ""Id"" = @Id;";

                using var connection = CreateConnection();
                await connection.ExecuteAsync(sql, memory);
                
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > 50)
                {
                    _logger.LogInformation("UpdateAsync зайняло {ElapsedMs}мс для ID: {MemoryId}, UserId: {UserId}", 
                        elapsed, memory.Id, memory.UserId);
                }
            }
            catch (Exception ex)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Помилка при оновленні запису пам'яті для ID: {MemoryId}, UserId: {UserId}. Час: {ElapsedMs}мс", 
                    memory.Id, memory.UserId, elapsed);
                throw;
            }
        }

        public async Task DeleteAsync(Guid userId, string conversationId)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                const string sql = @"
                    DELETE FROM ""AgentMemories""
                    WHERE ""UserId"" = @UserId AND ""ConversationId"" = @ConversationId;";

                using var connection = CreateConnection();
                await connection.ExecuteAsync(sql, new { UserId = userId, ConversationId = conversationId });
                
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > 50)
                {
                    _logger.LogInformation("DeleteAsync зайняло {ElapsedMs}мс для UserId: {UserId}, ConversationId: {ConversationId}", 
                        elapsed, userId, conversationId);
                }
            }
            catch (Exception ex)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Помилка при видаленні запису пам'яті для UserId: {UserId}, ConversationId: {ConversationId}. Час: {ElapsedMs}мс", 
                    userId, conversationId, elapsed);
                throw;
            }
        }

        public async Task<IEnumerable<AgentMemory>> GetAllForUserAsync(Guid userId)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                const string sql = @"
                    SELECT * FROM ""AgentMemories""
                    WHERE ""UserId"" = @UserId
                    ORDER BY ""CreatedAt"" DESC;";

                using var connection = CreateConnection();
                var memories = await connection.QueryAsync<AgentMemory>(sql, new { UserId = userId });
                
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > 100)
                {
                    _logger.LogInformation("GetAllForUserAsync зайняло {ElapsedMs}мс для UserId: {UserId}. Знайдено записів: {Count}", 
                        elapsed, userId, memories.Count());
                }
                
                return memories;
            }
            catch (Exception ex)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Помилка при отриманні всіх записів пам'яті для UserId: {UserId}. Час: {ElapsedMs}мс", 
                    userId, elapsed);
                throw;
            }
        }

        public async Task<IEnumerable<AgentMemory>> GetExpiredMemoriesAsync(DateTime currentTime)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                const string sql = @"
                    SELECT * FROM ""AgentMemories""
                    WHERE ""ExpiresAt"" IS NOT NULL AND ""ExpiresAt"" < @CurrentTime;";

                using var connection = CreateConnection();
                var memories = await connection.QueryAsync<AgentMemory>(sql, new { CurrentTime = currentTime });
                
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > 100)
                {
                    _logger.LogInformation("GetExpiredMemoriesAsync зайняло {ElapsedMs}мс. Знайдено записів: {Count}", 
                        elapsed, memories.Count());
                }
                
                return memories;
            }
            catch (Exception ex)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Помилка при отриманні застарілих записів пам'яті. Час: {ElapsedMs}мс", elapsed);
                throw;
            }
        }

        public async Task<IEnumerable<AgentMemory>> GetAllMemoriesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                const string sql = @"
                    SELECT * FROM ""AgentMemories""
                    ORDER BY ""CreatedAt"" DESC;";

                using var connection = CreateConnection();
                var memories = await connection.QueryAsync<AgentMemory>(sql);
                
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > 100)
                {
                    _logger.LogInformation("GetAllMemoriesAsync зайняло {ElapsedMs}мс. Знайдено записів: {Count}", 
                        elapsed, memories.Count());
                }
                
                return memories;
            }
            catch (Exception ex)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Помилка при отриманні всіх записів пам'яті. Час: {ElapsedMs}мс", elapsed);
                throw;
            }
        }
    }
} 