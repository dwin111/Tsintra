using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace Tsintra.Persistence.Context
{
    public class DatabaseContext
    {
        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly string _serverConnectionString;
        private readonly ILogger<DatabaseContext> _logger;

        public DatabaseContext(IConfiguration configuration, ILogger<DatabaseContext> logger = null)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' not found.");
            _logger = logger;
            
            // Отримання імені бази даних і рядка з'єднання з сервером (без вказівки бази даних)
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            _databaseName = builder.Database;
            builder.Database = "postgres"; // Підключення до стандартної бази PostgreSQL
            _serverConnectionString = builder.ToString();
        }

        // Метод для ініціалізації бази даних при запуску програми
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger?.LogInformation("Checking database connection and structure...");

                // Перевірка і створення бази даних, якщо її не існує
                await EnsureDatabaseExistsAsync();

                // Підключення до бази даних і перевірка/створення таблиць
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Перевірка чи існує таблиця users
                bool usersTableExists = await TableExistsAsync(connection, "users");
                if (!usersTableExists)
                {
                    _logger?.LogInformation("Creating users table...");
                    await connection.ExecuteAsync(@"
                        CREATE TABLE users (
                            id UUID PRIMARY KEY,
                            email VARCHAR(255) NOT NULL UNIQUE,
                            name VARCHAR(255),
                            created_at TIMESTAMP NOT NULL,
                            updated_at TIMESTAMP NOT NULL
                        );
                    ");
                }

                // Перевірка чи існує таблиця conversations
                bool conversationsTableExists = await TableExistsAsync(connection, "conversations");
                if (!conversationsTableExists)
                {
                    _logger?.LogInformation("Creating conversations table...");
                    await connection.ExecuteAsync(@"
                        CREATE TABLE conversations (
                            id UUID PRIMARY KEY,
                            user_id UUID NOT NULL,
                            title VARCHAR(255) NOT NULL,
                            created_at TIMESTAMP NOT NULL,
                            updated_at TIMESTAMP NOT NULL,
                            CONSTRAINT FK_Conversations_Users FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
                        );
                        CREATE INDEX IX_Conversations_UserId ON conversations(user_id);
                    ");
                }

                // Перевірка чи існує таблиця messages
                bool messagesTableExists = await TableExistsAsync(connection, "messages");
                if (!messagesTableExists)
                {
                    _logger?.LogInformation("Creating messages table...");
                    await connection.ExecuteAsync(@"
                        CREATE TABLE messages (
                            id UUID PRIMARY KEY,
                            conversation_id UUID NOT NULL,
                            role VARCHAR(20) NOT NULL,
                            content TEXT NOT NULL,
                            timestamp TIMESTAMP NOT NULL,
                            CONSTRAINT FK_Messages_Conversations FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
                        );
                        CREATE INDEX IX_Messages_ConversationId ON messages(conversation_id);
                    ");
                }

                // Перевірка чи існує таблиця agent_memories
                bool agentMemoriesTableExists = await TableExistsAsync(connection, "agent_memories");
                if (!agentMemoriesTableExists)
                {
                    _logger?.LogInformation("Creating agent_memories table...");
                    await connection.ExecuteAsync(@"
                        CREATE TABLE agent_memories (
                            id UUID PRIMARY KEY, 
                            agent_id UUID NOT NULL,
                            content TEXT NOT NULL,
                            embedding BYTEA NULL,
                            created_at TIMESTAMP NOT NULL,
                            updated_at TIMESTAMP NOT NULL
                        );
                        CREATE INDEX IX_agent_memories_agent_id ON agent_memories(agent_id);
                    ");
                }

                // Перевірка чи існує таблиця agent_long_term_memories
                bool agentLongTermMemoriesTableExists = await TableExistsAsync(connection, "agent_long_term_memories");
                if (!agentLongTermMemoriesTableExists)
                {
                    _logger?.LogInformation("Creating agent_long_term_memories table...");
                    await connection.ExecuteAsync(@"
                        CREATE TABLE agent_long_term_memories (
                            id UUID PRIMARY KEY,
                            agent_id UUID NOT NULL,
                            content TEXT NOT NULL,
                            embedding BYTEA NULL,
                            importance INT NOT NULL DEFAULT 1,
                            created_at TIMESTAMP NOT NULL,
                            updated_at TIMESTAMP NOT NULL
                        );
                        CREATE INDEX IX_agent_long_term_memories_agent_id ON agent_long_term_memories(agent_id);
                    ");
                }

                // Перевірка чи існує таблиця agent_conversation_memories
                bool agentConversationMemoriesTableExists = await TableExistsAsync(connection, "agent_conversation_memories");
                if (!agentConversationMemoriesTableExists)
                {
                    _logger?.LogInformation("Creating agent_conversation_memories table...");
                    await connection.ExecuteAsync(@"
                        CREATE TABLE agent_conversation_memories (
                            id UUID PRIMARY KEY,
                            agent_id UUID NOT NULL,
                            conversation_id UUID NOT NULL,
                            summary TEXT NOT NULL,
                            created_at TIMESTAMP NOT NULL,
                            updated_at TIMESTAMP NOT NULL
                        );
                        CREATE INDEX IX_agent_conversation_memories_agent_id ON agent_conversation_memories(agent_id);
                        CREATE INDEX IX_agent_conversation_memories_conversation_id ON agent_conversation_memories(conversation_id);
                    ");
                }

                _logger?.LogInformation("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing database: {Message}", ex.Message);
                throw;
            }
        }

        // Метод для перевірки існування бази даних і її створення при необхідності
        private async Task EnsureDatabaseExistsAsync()
        {
            try
            {
                // Спроба підключення до бази даних
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    _logger?.LogInformation("Database '{DatabaseName}' already exists", _databaseName);
                    return;
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "3D000") // Database does not exist
            {
                _logger?.LogInformation("Database '{DatabaseName}' does not exist. Creating...", _databaseName);
                
                // Підключення до сервера PostgreSQL (без вказівки конкретної бази даних)
                using (var connection = new NpgsqlConnection(_serverConnectionString))
                {
                    await connection.OpenAsync();
                    
                    // Перевірка, чи не існує вже база даних
                    string checkSql = "SELECT 1 FROM pg_database WHERE datname = @dbName";
                    var exists = await connection.ExecuteScalarAsync<bool>(checkSql, new { dbName = _databaseName });
                    
                    if (!exists)
                    {
                        // Створення нової бази даних
                        string createDbSql = $"CREATE DATABASE {_databaseName} WITH OWNER = postgres ENCODING = 'UTF8' CONNECTION LIMIT = -1;";
                        await connection.ExecuteAsync(createDbSql);
                        _logger?.LogInformation("Database '{DatabaseName}' created successfully", _databaseName);
                    }
                    else
                    {
                        _logger?.LogInformation("Database '{DatabaseName}' already exists", _databaseName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking/creating database: {Message}", ex.Message);
                throw;
            }
        }

        private async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName)
        {
            string query = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    AND table_name = @tableName
                );";
            
            return await connection.ExecuteScalarAsync<bool>(query, new { tableName });
        }
        
        protected async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }

        protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            return await connection.QueryAsync<T>(sql, parameters);
        }

        protected async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            return await connection.ExecuteAsync(sql, parameters);
        }

        protected async Task<T> ExecuteScalarAsync<T>(string sql, object parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<T>(sql, parameters);
        }

        protected async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            return await connection.QueryMultipleAsync(sql, parameters);
        }

        protected async Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(
            string sql,
            Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map,
            object parameters = null,
            string splitOn = "Id")
        {
            using var connection = new NpgsqlConnection(_connectionString);
            return await connection.QueryAsync(sql, map, parameters, splitOn: splitOn);
        }
    }
} 