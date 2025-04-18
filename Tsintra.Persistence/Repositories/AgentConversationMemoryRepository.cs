using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories
{
    public class AgentConversationMemoryRepository : IAgentConversationMemoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AgentConversationMemoryRepository> _logger;

        public AgentConversationMemoryRepository(IConfiguration configuration, ILogger<AgentConversationMemoryRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        private IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public async Task<AgentConversationMemory> GetByIdAsync(Guid id)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT m.*, c.* 
                    FROM agent_conversation_memories m
                    LEFT JOIN conversations c ON m.conversation_id = c.id
                    WHERE m.id = @Id";

                var memories = await connection.QueryAsync<AgentConversationMemory, Conversation, AgentConversationMemory>(
                    sql,
                    (memory, conversation) =>
                    {
                        memory.Conversation = conversation;
                        return memory;
                    },
                    new { Id = id },
                    splitOn: "id");

                return memories.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving memory by ID: {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<AgentConversationMemory>> GetByConversationIdAsync(Guid conversationId)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT m.*, c.* 
                    FROM agent_conversation_memories m
                    LEFT JOIN conversations c ON m.conversation_id = c.id
                    WHERE m.conversation_id = @ConversationId
                    ORDER BY m.created_at DESC";

                var memories = await connection.QueryAsync<AgentConversationMemory, Conversation, AgentConversationMemory>(
                    sql,
                    (memory, conversation) =>
                    {
                        memory.Conversation = conversation;
                        return memory;
                    },
                    new { ConversationId = conversationId },
                    splitOn: "id");

                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving memories by conversation ID: {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentConversationMemory>> GetByKeyAsync(Guid conversationId, string key)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT m.*, c.* 
                    FROM agent_conversation_memories m
                    LEFT JOIN conversations c ON m.conversation_id = c.id
                    WHERE m.conversation_id = @ConversationId AND m.key = @Key
                    ORDER BY m.created_at DESC";

                var memories = await connection.QueryAsync<AgentConversationMemory, Conversation, AgentConversationMemory>(
                    sql,
                    (memory, conversation) =>
                    {
                        memory.Conversation = conversation;
                        return memory;
                    },
                    new { ConversationId = conversationId, Key = key },
                    splitOn: "id");

                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving memories by key: {Key} for conversation: {ConversationId}", key, conversationId);
                throw;
            }
        }

        public async Task<IEnumerable<AgentConversationMemory>> GetActiveMemoriesAsync(Guid conversationId)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT m.*, c.* 
                    FROM agent_conversation_memories m
                    LEFT JOIN conversations c ON m.conversation_id = c.id
                    WHERE m.conversation_id = @ConversationId 
                    AND m.is_active = true 
                    AND (m.expires_at IS NULL OR m.expires_at > CURRENT_TIMESTAMP)
                    ORDER BY m.priority DESC, m.created_at DESC";

                var memories = await connection.QueryAsync<AgentConversationMemory, Conversation, AgentConversationMemory>(
                    sql,
                    (memory, conversation) =>
                    {
                        memory.Conversation = conversation;
                        return memory;
                    },
                    new { ConversationId = conversationId },
                    splitOn: "id");

                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active memories for conversation: {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<AgentConversationMemory> AddAsync(AgentConversationMemory memory)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    INSERT INTO agent_conversation_memories 
                    (id, conversation_id, key, content, created_at, expires_at, is_active, priority, source) 
                    VALUES 
                    (@Id, @ConversationId, @Key, @Content, @CreatedAt, @ExpiresAt, @IsActive, @Priority, @Source) 
                    RETURNING *";

                if (memory.Id == Guid.Empty)
                {
                    memory.Id = Guid.NewGuid();
                }

                if (memory.CreatedAt == default)
                {
                    memory.CreatedAt = DateTime.UtcNow;
                }

                var result = await connection.QueryFirstOrDefaultAsync<AgentConversationMemory>(sql, new
                {
                    memory.Id,
                    memory.ConversationId,
                    memory.Key,
                    memory.Content,
                    memory.CreatedAt,
                    memory.ExpiresAt,
                    memory.IsActive,
                    memory.Priority,
                    memory.Source
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding memory for conversation: {ConversationId}", memory.ConversationId);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(AgentConversationMemory memory)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    UPDATE agent_conversation_memories 
                    SET content = @Content, 
                        expires_at = @ExpiresAt, 
                        is_active = @IsActive, 
                        priority = @Priority, 
                        source = @Source
                    WHERE id = @Id";

                var rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    memory.Id,
                    memory.Content,
                    memory.ExpiresAt,
                    memory.IsActive,
                    memory.Priority,
                    memory.Source
                });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating memory: {Id}", memory.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = "DELETE FROM agent_conversation_memories WHERE id = @Id";

                var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting memory: {Id}", id);
                throw;
            }
        }

        public async Task<bool> DeactivateAsync(Guid id)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = "UPDATE agent_conversation_memories SET is_active = false WHERE id = @Id";

                var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating memory: {Id}", id);
                throw;
            }
        }

        public async Task<bool> DeactivateByKeyAsync(Guid conversationId, string key)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = "UPDATE agent_conversation_memories SET is_active = false WHERE conversation_id = @ConversationId AND key = @Key";

                var rowsAffected = await connection.ExecuteAsync(sql, new { ConversationId = conversationId, Key = key });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating memories by key: {Key} for conversation: {ConversationId}", key, conversationId);
                throw;
            }
        }

        public async Task<bool> DeactivateAllAsync(Guid conversationId)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = "UPDATE agent_conversation_memories SET is_active = false WHERE conversation_id = @ConversationId";

                var rowsAffected = await connection.ExecuteAsync(sql, new { ConversationId = conversationId });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating all memories for conversation: {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<bool> CleanupExpiredMemoriesAsync()
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = "DELETE FROM agent_conversation_memories WHERE expires_at < CURRENT_TIMESTAMP";

                var rowsAffected = await connection.ExecuteAsync(sql);

                _logger.LogInformation("Cleaned up {RowsAffected} expired memories", rowsAffected);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired memories");
                throw;
            }
        }
        
        public async Task<int> GetMemoryCountAsync(Guid conversationId)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT COUNT(*) 
                    FROM agent_conversation_memories 
                    WHERE conversation_id = @ConversationId";

                return await connection.ExecuteScalarAsync<int>(sql, new { ConversationId = conversationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting memories for conversation: {ConversationId}", conversationId);
                throw;
            }
        }
        
        public async Task<IEnumerable<AgentConversationMemory>> GetByPriorityAsync(Guid conversationId, int minPriority = 0)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT m.*, c.* 
                    FROM agent_conversation_memories m
                    LEFT JOIN conversations c ON m.conversation_id = c.id
                    WHERE m.conversation_id = @ConversationId 
                    AND m.priority >= @MinPriority 
                    AND m.is_active = true 
                    AND (m.expires_at IS NULL OR m.expires_at > CURRENT_TIMESTAMP)
                    ORDER BY m.priority DESC, m.created_at DESC";

                var memories = await connection.QueryAsync<AgentConversationMemory, Conversation, AgentConversationMemory>(
                    sql,
                    (memory, conversation) =>
                    {
                        memory.Conversation = conversation;
                        return memory;
                    },
                    new { ConversationId = conversationId, MinPriority = minPriority },
                    splitOn: "id");

                return memories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving memories by priority for conversation: {ConversationId}", conversationId);
                throw;
            }
        }
    }
} 