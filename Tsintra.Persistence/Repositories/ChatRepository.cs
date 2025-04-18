using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Tsintra.Domain.Enums;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Dapper;

namespace Tsintra.Persistence.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ChatRepository> _logger;

        public ChatRepository(string connectionString, ILogger<ChatRepository> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        #region Database Operations

        private async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();
                return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing QueryFirstOrDefaultAsync with SQL: {Sql}", sql);
                throw;
            }
        }

        private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();
                return await connection.QueryAsync<T>(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing QueryAsync with SQL: {Sql}", sql);
                throw;
            }
        }

        private async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();
                return await connection.ExecuteAsync(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing ExecuteAsync with SQL: {Sql}", sql);
                throw;
            }
        }

        private async Task<T> ExecuteScalarAsync<T>(string sql, object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();
                return await connection.ExecuteScalarAsync<T>(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing ExecuteScalarAsync with SQL: {Sql}", sql);
                throw;
            }
        }

        #endregion

        #region Conversation Operations

        public async Task<Conversation> GetConversationAsync(Guid id)
        {
            try
            {
                const string sql = @"
                    SELECT * FROM conversations
                    WHERE id = @Id";
                
                return await QueryFirstOrDefaultAsync<Conversation>(sql, new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation with ID {ConversationId}", id);
                throw;
            }
        }

        public async Task<List<Conversation>> GetUserConversationsAsync(Guid userId)
        {
            try
            {
                const string sql = @"
                    SELECT * FROM conversations
                    WHERE user_id = @UserId
                    ORDER BY updated_at DESC";
                
                var conversations = await QueryAsync<Conversation>(sql, new { UserId = userId });
                return conversations?.ToList() ?? new List<Conversation>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversations for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Conversation> CreateConversationAsync(Guid userId, string title)
        {
            try
            {
                var conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = title ?? "New Chat",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                const string sql = @"
                    INSERT INTO conversations (id, user_id, title, created_at, updated_at)
                    VALUES (@Id, @UserId, @Title, @CreatedAt, @UpdatedAt)";
                
                await ExecuteAsync(sql, conversation);
                
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateConversationTitleAsync(Guid conversationId, string newTitle)
        {
            try
            {
                const string sql = @"
                    UPDATE conversations
                    SET title = @NewTitle, updated_at = @UpdatedAt
                    WHERE id = @ConversationId";
                
                var updatedRows = await ExecuteAsync(sql, new 
                { 
                    ConversationId = conversationId,
                    NewTitle = newTitle,
                    UpdatedAt = DateTime.UtcNow
                });
                
                return updatedRows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating title for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<bool> UpdateConversationTimestampAsync(Guid conversationId)
        {
            try
            {
                const string sql = @"
                    UPDATE conversations
                    SET updated_at = @UpdatedAt
                    WHERE id = @ConversationId";
                
                var updatedRows = await ExecuteAsync(sql, new 
                { 
                    ConversationId = conversationId,
                    UpdatedAt = DateTime.UtcNow
                });
                
                return updatedRows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timestamp for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<bool> DeleteConversationAsync(Guid id)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();
                
                using var transaction = await connection.BeginTransactionAsync();
                
                try
                {
                    // Delete messages first (due to foreign key constraint)
                    const string deleteMsgSql = @"
                        DELETE FROM messages
                        WHERE conversation_id = @ConversationId";
                    
                    await connection.ExecuteAsync(deleteMsgSql, new { ConversationId = id }, transaction);
                    
                    // Then delete the conversation
                    const string deleteConvSql = @"
                        DELETE FROM conversations
                        WHERE id = @ConversationId";
                    
                    var result = await connection.ExecuteAsync(deleteConvSql, new { ConversationId = id }, transaction);
                    
                    await transaction.CommitAsync();
                    
                    return result > 0;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction error deleting conversation {ConversationId}", id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation {ConversationId}", id);
                throw;
            }
        }

        #endregion

        #region Message Operations

        public async Task<Message> AddMessageAsync(Guid conversationId, string content, MessageRole role)
        {
            try
            {
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = role.ToString().ToLower(), // Convert enum to "user" or "assistant" string
                    Content = content,
                    Timestamp = DateTime.UtcNow
                };
                
                const string sql = @"
                    INSERT INTO messages (id, conversation_id, role, content, timestamp)
                    VALUES (@Id, @ConversationId, @Role, @Content, @Timestamp)";
                
                await ExecuteAsync(sql, message);
                
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding message to conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<List<Message>> GetConversationMessagesAsync(Guid conversationId)
        {
            try
            {
                const string sql = @"
                    SELECT * FROM messages
                    WHERE conversation_id = @ConversationId
                    ORDER BY timestamp ASC";
                
                var messages = await QueryAsync<Message>(sql, new { ConversationId = conversationId });
                return messages?.ToList() ?? new List<Message>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        #endregion

        #region Enhanced Message Operations

        public async Task<PaginatedResult<Message>> GetPaginatedMessagesAsync(Guid conversationId, MessageQueryOptions options)
        {
            try
            {
                var sqlBuilder = new StringBuilder(@"
                    SELECT * FROM messages
                    WHERE conversation_id = @ConversationId");

                // Apply filters
                if (options.FromDate.HasValue)
                {
                    sqlBuilder.Append(" AND timestamp >= @FromDate");
                }
                
                if (options.ToDate.HasValue)
                {
                    sqlBuilder.Append(" AND timestamp <= @ToDate");
                }
                
                if (!string.IsNullOrEmpty(options.Role))
                {
                    sqlBuilder.Append(" AND role = @Role");
                }
                
                if (!string.IsNullOrEmpty(options.SearchText))
                {
                    sqlBuilder.Append(" AND content ILIKE @SearchText");
                }

                // Apply ordering
                sqlBuilder.Append(" ORDER BY timestamp ");
                sqlBuilder.Append(options.OrderBy?.ToLower() == "desc" ? "DESC" : "ASC");
                
                // Apply pagination
                sqlBuilder.Append(" LIMIT @Take OFFSET @Skip");

                var parameters = new
                {
                    ConversationId = conversationId,
                    FromDate = options.FromDate,
                    ToDate = options.ToDate,
                    Role = options.Role,
                    SearchText = !string.IsNullOrEmpty(options.SearchText) ? $"%{options.SearchText}%" : null,
                    Skip = options.Skip,
                    Take = options.Take
                };

                // Execute query for messages
                var messages = await QueryAsync<Message>(sqlBuilder.ToString(), parameters);
                
                // Get total count for pagination
                var countBuilder = new StringBuilder(@"
                    SELECT COUNT(*) FROM messages
                    WHERE conversation_id = @ConversationId");
                
                // Apply the same filters to the count query
                if (options.FromDate.HasValue)
                {
                    countBuilder.Append(" AND timestamp >= @FromDate");
                }
                
                if (options.ToDate.HasValue)
                {
                    countBuilder.Append(" AND timestamp <= @ToDate");
                }
                
                if (!string.IsNullOrEmpty(options.Role))
                {
                    countBuilder.Append(" AND role = @Role");
                }
                
                if (!string.IsNullOrEmpty(options.SearchText))
                {
                    countBuilder.Append(" AND content ILIKE @SearchText");
                }
                
                var totalCount = await ExecuteScalarAsync<int>(countBuilder.ToString(), parameters);
                
                // Calculate pagination values
                int pageSize = options.Take;
                int currentPage = (options.Skip / pageSize) + 1;
                int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                
                return new PaginatedResult<Message>
                {
                    Items = messages?.ToList() ?? new List<Message>(),
                    TotalCount = totalCount,
                    CurrentPage = currentPage,
                    TotalPages = totalPages,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated messages for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<int> GetMessageCountAsync(Guid conversationId, MessageQueryOptions options = null)
        {
            try
            {
                var sqlBuilder = new StringBuilder(@"
                    SELECT COUNT(*) FROM messages
                    WHERE conversation_id = @ConversationId");

                // Apply filters
                if (options != null)
                {
                    if (options.FromDate.HasValue)
                    {
                        sqlBuilder.Append(" AND timestamp >= @FromDate");
                    }
                    
                    if (options.ToDate.HasValue)
                    {
                        sqlBuilder.Append(" AND timestamp <= @ToDate");
                    }
                    
                    if (!string.IsNullOrEmpty(options.Role))
                    {
                        sqlBuilder.Append(" AND role = @Role");
                    }
                    
                    if (!string.IsNullOrEmpty(options.SearchText))
                    {
                        sqlBuilder.Append(" AND content ILIKE @SearchText");
                    }
                }

                var parameters = new
                {
                    ConversationId = conversationId,
                    FromDate = options?.FromDate,
                    ToDate = options?.ToDate,
                    Role = options?.Role,
                    SearchText = options != null && !string.IsNullOrEmpty(options.SearchText) ? $"%{options.SearchText}%" : null
                };

                return await ExecuteScalarAsync<int>(sqlBuilder.ToString(), parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message count for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<ChatSummaryDto> GetConversationSummaryAsync(Guid conversationId)
        {
            try
            {
                // Get conversation
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null)
                {
                    return null;
                }

                // Get first message
                const string firstMessageSql = @"
                    SELECT * FROM messages
                    WHERE conversation_id = @ConversationId
                    ORDER BY timestamp ASC
                    LIMIT 1";

                var firstMessage = await QueryFirstOrDefaultAsync<Message>(firstMessageSql, new { ConversationId = conversationId });

                // Get last message
                const string lastMessageSql = @"
                    SELECT * FROM messages
                    WHERE conversation_id = @ConversationId
                    ORDER BY timestamp DESC
                    LIMIT 1";

                var lastMessage = await QueryFirstOrDefaultAsync<Message>(lastMessageSql, new { ConversationId = conversationId });

                // Get message count
                const string countSql = @"
                    SELECT COUNT(*) FROM messages
                    WHERE conversation_id = @ConversationId";

                var messageCount = await ExecuteScalarAsync<int>(countSql, new { ConversationId = conversationId });

                return new ChatSummaryDto
                {
                    Conversation = conversation,
                    FirstMessage = firstMessage,
                    LastMessage = lastMessage,
                    MessageCount = messageCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat summary for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        #endregion
    }
} 