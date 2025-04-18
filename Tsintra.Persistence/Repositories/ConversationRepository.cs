using Microsoft.Extensions.Logging;
using Npgsql;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using System.Threading;
using System.Text;

namespace Tsintra.Persistence.Repositories
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ConversationRepository> _logger;

        public ConversationRepository(string connectionString, ILogger<ConversationRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        private NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public async Task<List<Conversation>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT * FROM conversations 
                    ORDER BY updated_at DESC";

                var conversations = await connection.QueryAsync<Conversation>(sql);
                return conversations.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all conversations");
                throw;
            }
        }

        public async Task<Conversation> GetByIdAsync(Guid id)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT * FROM conversations 
                    WHERE id = @Id";

                var conversation = await connection.QueryFirstOrDefaultAsync<Conversation>(sql, new { Id = id });
                
                if (conversation != null)
                {
                    // Load messages for this conversation
                    conversation.Messages = (await GetConversationMessagesAsync(id)).ToList();
                }
                
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation by ID {ConversationId}", id);
                throw;
            }
        }

        public async Task<List<Conversation>> GetByUserIdAsync(Guid userId)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT * FROM conversations 
                    WHERE user_id = @UserId
                    ORDER BY updated_at DESC";

                var conversations = await connection.QueryAsync<Conversation>(sql, new { UserId = userId });
                var result = conversations.ToList();
                
                // We don't load messages here to prevent too much data
                // Messages will be loaded when a specific conversation is selected
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Conversation> CreateAsync(Conversation conversation)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    INSERT INTO conversations (id, user_id, title, created_at, updated_at)
                    VALUES (@Id, @UserId, @Title, @CreatedAt, @UpdatedAt)
                    RETURNING *";

                if (conversation.Id == Guid.Empty)
                {
                    conversation.Id = Guid.NewGuid();
                }
                
                conversation.CreatedAt = DateTime.UtcNow;
                conversation.UpdatedAt = conversation.CreatedAt;

                return await connection.QueryFirstOrDefaultAsync<Conversation>(sql, conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation for user {UserId}", conversation.UserId);
                throw;
            }
        }

        public async Task UpdateAsync(Conversation conversation)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    UPDATE conversations
                    SET title = @Title, updated_at = @UpdatedAt
                    WHERE id = @Id";

                conversation.UpdatedAt = DateTime.UtcNow;

                await connection.ExecuteAsync(sql, conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating conversation {ConversationId}", conversation.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();
                
                using var transaction = await connection.BeginTransactionAsync();
                
                try
                {
                    // First delete all messages in this conversation
                    const string deleteMessagesSQL = @"
                        DELETE FROM messages
                        WHERE conversation_id = @ConversationId";
                        
                    await connection.ExecuteAsync(deleteMessagesSQL, new { ConversationId = id }, transaction);
                    
                    // Then delete the conversation
                    const string deleteConversationSQL = @"
                        DELETE FROM conversations
                        WHERE id = @Id";
                        
                    await connection.ExecuteAsync(deleteConversationSQL, new { Id = id }, transaction);
                    
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation {ConversationId}", id);
                throw;
            }
        }

        public async Task<Message> AddMessageAsync(Message message)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    INSERT INTO messages (id, conversation_id, role, content, timestamp)
                    VALUES (@Id, @ConversationId, @Role, @Content, @Timestamp)
                    RETURNING *";

                if (message.Id == Guid.Empty)
                {
                    message.Id = Guid.NewGuid();
                }
                
                message.Timestamp = DateTime.UtcNow;

                var result = await connection.QueryFirstOrDefaultAsync<Message>(sql, message);
                
                // Update the conversation's updated_at timestamp
                const string updateConversationSql = @"
                    UPDATE conversations 
                    SET updated_at = @Timestamp
                    WHERE id = @ConversationId";
                    
                await connection.ExecuteAsync(updateConversationSql, 
                    new { Timestamp = message.Timestamp, ConversationId = message.ConversationId });
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding message to conversation {ConversationId}", message.ConversationId);
                throw;
            }
        }

        public async Task<List<Message>> GetConversationMessagesAsync(Guid conversationId)
        {
            try
            {
                using var connection = CreateConnection();
                const string sql = @"
                    SELECT * FROM messages
                    WHERE conversation_id = @ConversationId
                    ORDER BY timestamp ASC";

                var messages = await connection.QueryAsync<Message>(sql, new { ConversationId = conversationId });
                return messages.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<PaginatedResult<Message>> GetPaginatedMessagesAsync(Guid conversationId, MessageQueryOptions options)
        {
            try
            {
                StringBuilder sqlBuilder = new StringBuilder(@"
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

                using var connection = CreateConnection();
                
                // Get total count (for pagination info)
                int totalCount = await GetMessageCountAsync(conversationId, options);
                
                // Get paginated results
                var messages = await connection.QueryAsync<Message>(sqlBuilder.ToString(), parameters);
                
                // Calculate pagination values
                int pageSize = options.Take;
                int currentPage = (options.Skip / pageSize) + 1;
                int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                return new PaginatedResult<Message>
                {
                    Items = messages.ToList(),
                    TotalCount = totalCount,
                    CurrentPage = currentPage,
                    TotalPages = totalPages,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated messages for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<int> GetMessageCountAsync(Guid conversationId, MessageQueryOptions options = null)
        {
            try
            {
                StringBuilder sqlBuilder = new StringBuilder(@"
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

                using var connection = CreateConnection();
                return await connection.ExecuteScalarAsync<int>(sqlBuilder.ToString(), parameters);
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
                using var connection = CreateConnection();
                const string conversationSql = @"
                    SELECT * FROM conversations 
                    WHERE id = @ConversationId";

                var conversation = await connection.QueryFirstOrDefaultAsync<Conversation>(conversationSql, 
                    new { ConversationId = conversationId });

                if (conversation == null)
                {
                    return null;
                }

                const string firstMessageSql = @"
                    SELECT * FROM messages
                    WHERE conversation_id = @ConversationId
                    ORDER BY timestamp ASC
                    LIMIT 1";

                const string lastMessageSql = @"
                    SELECT * FROM messages
                    WHERE conversation_id = @ConversationId
                    ORDER BY timestamp DESC
                    LIMIT 1";

                const string countSql = @"
                    SELECT COUNT(*) FROM messages
                    WHERE conversation_id = @ConversationId";

                var firstMessage = await connection.QueryFirstOrDefaultAsync<Message>(firstMessageSql, 
                    new { ConversationId = conversationId });

                var lastMessage = await connection.QueryFirstOrDefaultAsync<Message>(lastMessageSql, 
                    new { ConversationId = conversationId });

                var messageCount = await connection.ExecuteScalarAsync<int>(countSql, 
                    new { ConversationId = conversationId });

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
    }
} 