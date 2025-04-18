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
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<RefreshTokenRepository> _logger;

        public RefreshTokenRepository(IConfiguration configuration, ILogger<RefreshTokenRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<RefreshToken> CreateAsync(RefreshToken refreshToken)
        {
            _logger.LogDebug("Creating refresh token for user: {UserId}", refreshToken.UserId);
            const string sql = @"
                INSERT INTO ""RefreshTokens"" (""Id"", ""UserId"", ""Token"", ""ExpiryDate"", ""CreatedAt"", ""RevokedAt"")
                VALUES (@Id, @UserId, @Token, @ExpiryDate, @CreatedAt, @RevokedAt)
                RETURNING *;";
            
            try
            {
                using var connection = CreateConnection();
                refreshToken.Id = refreshToken.Id == Guid.Empty ? Guid.NewGuid() : refreshToken.Id;
                refreshToken.CreatedAt = refreshToken.CreatedAt == default ? DateTime.UtcNow : refreshToken.CreatedAt;

                var createdToken = await connection.QuerySingleAsync<RefreshToken>(sql, refreshToken);
                _logger.LogInformation("Successfully created refresh token with ID: {Id}", createdToken.Id);
                return createdToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating refresh token for user: {UserId}", refreshToken.UserId);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            _logger.LogDebug("Deleting refresh token with ID: {Id}", id);
            const string sql = "DELETE FROM \"RefreshTokens\" WHERE \"Id\" = @Id;";
            
            try
            {
                using var connection = CreateConnection();
                int rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
                _logger.LogInformation("Deleted refresh token with ID: {Id}. Rows affected: {RowsAffected}", id, rowsAffected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting refresh token with ID: {Id}", id);
                throw;
            }
        }

        public async Task DeleteExpiredTokensAsync(DateTime cutoffDate)
        {
            _logger.LogDebug("Deleting expired refresh tokens before: {CutoffDate}", cutoffDate);
            const string sql = "DELETE FROM \"RefreshTokens\" WHERE \"ExpiryDate\" < @CutoffDate;";
            
            try
            {
                using var connection = CreateConnection();
                int rowsAffected = await connection.ExecuteAsync(sql, new { CutoffDate = cutoffDate });
                _logger.LogInformation("Deleted {Count} expired refresh tokens before: {CutoffDate}", rowsAffected, cutoffDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expired refresh tokens before: {CutoffDate}", cutoffDate);
                throw;
            }
        }

        public async Task<RefreshToken> GetByIdAsync(Guid id)
        {
            _logger.LogDebug("Getting refresh token by ID: {Id}", id);
            const string sql = @"
                SELECT t.*, u.* 
                FROM ""RefreshTokens"" t
                LEFT JOIN ""Users"" u ON t.""UserId"" = u.""Id""
                WHERE t.""Id"" = @Id;";
            
            try
            {
                using var connection = CreateConnection();
                var tokens = await connection.QueryAsync<RefreshToken, User, RefreshToken>(
                    sql,
                    (token, user) =>
                    {
                        token.User = user;
                        return token;
                    },
                    new { Id = id },
                    splitOn: "Id");

                return tokens.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting refresh token by ID: {Id}", id);
                throw;
            }
        }

        public async Task<RefreshToken> GetByTokenAsync(string token)
        {
            _logger.LogDebug("Getting refresh token by token string");
            const string sql = @"
                SELECT t.*, u.* 
                FROM ""RefreshTokens"" t
                LEFT JOIN ""Users"" u ON t.""UserId"" = u.""Id""
                WHERE t.""Token"" = @Token;";
            
            try
            {
                using var connection = CreateConnection();
                var tokens = await connection.QueryAsync<RefreshToken, User, RefreshToken>(
                    sql,
                    (refreshToken, user) =>
                    {
                        refreshToken.User = user;
                        return refreshToken;
                    },
                    new { Token = token },
                    splitOn: "Id");

                return tokens.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting refresh token by token string");
                throw;
            }
        }

        public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId)
        {
            _logger.LogDebug("Getting refresh tokens for user: {UserId}", userId);
            const string sql = @"
                SELECT t.*, u.* 
                FROM ""RefreshTokens"" t
                LEFT JOIN ""Users"" u ON t.""UserId"" = u.""Id""
                WHERE t.""UserId"" = @UserId
                ORDER BY t.""CreatedAt"" DESC;";
            
            try
            {
                using var connection = CreateConnection();
                var tokens = await connection.QueryAsync<RefreshToken, User, RefreshToken>(
                    sql,
                    (token, user) =>
                    {
                        token.User = user;
                        return token;
                    },
                    new { UserId = userId },
                    splitOn: "Id");

                return tokens;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting refresh tokens for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<RefreshToken>> GetExpiredTokensAsync(DateTime cutoffDate)
        {
            _logger.LogDebug("Getting expired refresh tokens before: {CutoffDate}", cutoffDate);
            const string sql = @"
                SELECT t.*, u.* 
                FROM ""RefreshTokens"" t
                LEFT JOIN ""Users"" u ON t.""UserId"" = u.""Id""
                WHERE t.""ExpiryDate"" < @CutoffDate
                ORDER BY t.""ExpiryDate"" ASC;";
            
            try
            {
                using var connection = CreateConnection();
                var tokens = await connection.QueryAsync<RefreshToken, User, RefreshToken>(
                    sql,
                    (token, user) =>
                    {
                        token.User = user;
                        return token;
                    },
                    new { CutoffDate = cutoffDate },
                    splitOn: "Id");

                return tokens;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expired refresh tokens before: {CutoffDate}", cutoffDate);
                throw;
            }
        }

        public async Task RevokeAsync(Guid id)
        {
            _logger.LogDebug("Revoking refresh token with ID: {Id}", id);
            const string sql = @"
                UPDATE ""RefreshTokens""
                SET ""RevokedAt"" = @RevokedAt
                WHERE ""Id"" = @Id;";
            
            try
            {
                using var connection = CreateConnection();
                int rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, RevokedAt = DateTime.UtcNow });
                _logger.LogInformation("Revoked refresh token with ID: {Id}. Rows affected: {RowsAffected}", id, rowsAffected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token with ID: {Id}", id);
                throw;
            }
        }

        public async Task RevokeAllUserTokensAsync(Guid userId)
        {
            _logger.LogDebug("Revoking all refresh tokens for user: {UserId}", userId);
            const string sql = @"
                UPDATE ""RefreshTokens""
                SET ""RevokedAt"" = @RevokedAt
                WHERE ""UserId"" = @UserId AND ""RevokedAt"" IS NULL;";
            
            try
            {
                using var connection = CreateConnection();
                int rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId, RevokedAt = DateTime.UtcNow });
                _logger.LogInformation("Revoked {Count} refresh tokens for user: {UserId}", rowsAffected, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all refresh tokens for user: {UserId}", userId);
                throw;
            }
        }

        public async Task UpdateAsync(RefreshToken refreshToken)
        {
            _logger.LogDebug("Updating refresh token with ID: {Id}", refreshToken.Id);
            const string sql = @"
                UPDATE ""RefreshTokens""
                SET ""Token"" = @Token,
                    ""ExpiryDate"" = @ExpiryDate,
                    ""RevokedAt"" = @RevokedAt
                WHERE ""Id"" = @Id;";
            
            try
            {
                using var connection = CreateConnection();
                int rowsAffected = await connection.ExecuteAsync(sql, refreshToken);
                _logger.LogInformation("Updated refresh token with ID: {Id}. Rows affected: {RowsAffected}", refreshToken.Id, rowsAffected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating refresh token with ID: {Id}", refreshToken.Id);
                throw;
            }
        }

        public async Task DeleteAllForUserAsync(Guid userId)
        {
            _logger.LogDebug("Deleting all refresh tokens for user: {UserId}", userId);
            const string sql = "DELETE FROM \"RefreshTokens\" WHERE \"UserId\" = @UserId RETURNING *;";
            
            try
            {
                using var connection = CreateConnection();
                var tokens = await connection.QueryAsync<RefreshToken>(sql, new { UserId = userId });
                _logger.LogInformation("Deleted {Count} refresh tokens for user {UserId}", tokens.Count(), userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all refresh tokens for user: {UserId}", userId);
                throw;
            }
        }
    }
} 