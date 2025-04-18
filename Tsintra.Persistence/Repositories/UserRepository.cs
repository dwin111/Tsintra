using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        _logger.LogDebug("Attempting to find user by GoogleId: {GoogleId}", googleId);
        const string sql = "SELECT * FROM \"Users\" WHERE \"GoogleId\" = @GoogleId LIMIT 1;";
        try
        {
            using var connection = CreateConnection();
            var user = await connection.QuerySingleOrDefaultAsync<User>(sql, new { GoogleId = googleId });
            if (user == null)
            {
                _logger.LogDebug("User not found for GoogleId: {GoogleId}", googleId);
            }
            else
            {
                _logger.LogDebug("User found for GoogleId: {GoogleId}. User ID: {UserId}", googleId, user.Id);
            }
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding user by GoogleId: {GoogleId}", googleId);
            throw;
        }
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        _logger.LogDebug("Attempting to find user by Email: {Email}", email);
        const string sql = "SELECT * FROM \"Users\" WHERE \"Email\" = @Email LIMIT 1;";
        try
        {
            using var connection = CreateConnection();
            var user = await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
            if (user == null)
            {
                _logger.LogDebug("User not found for Email: {Email}", email);
            }
            else
            {
                _logger.LogDebug("User found for Email: {Email}. User ID: {UserId}", email, user.Id);
            }
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding user by Email: {Email}", email);
            throw;
        }
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        _logger.LogDebug("Attempting to find user by ID: {UserId}", id);
        const string sql = "SELECT * FROM \"Users\" WHERE \"Id\" = @Id LIMIT 1;";
        try
        {
            using var connection = CreateConnection();
            var user = await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
            if (user == null)
            {
                _logger.LogDebug("User not found for ID: {UserId}", id);
            }
            else
            {
                _logger.LogDebug("User found for ID: {UserId}", id);
            }
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding user by ID: {UserId}", id);
            throw;
        }
    }

    public async Task<User> CreateAsync(User user)
    {
        _logger.LogDebug("Attempting to create user with Email: {Email}, GoogleId: {GoogleId}", user.Email, user.GoogleId);
        const string sql = """
            INSERT INTO "Users" ("Id", "GoogleId", "Email", "FirstName", "LastName", "ProfilePictureUrl", "CreatedAt", "LastLoginAt")
            VALUES (@Id, @GoogleId, @Email, @FirstName, @LastName, @ProfilePictureUrl, @CreatedAt, @LastLoginAt)
            RETURNING *;
            """;
        try
        {
            using var connection = CreateConnection();
            user.Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id;
            user.CreatedAt = user.CreatedAt == default ? DateTime.UtcNow : user.CreatedAt;

            var createdUser = await connection.QuerySingleAsync<User>(sql, user);
            _logger.LogInformation("Successfully created user with ID: {UserId}", createdUser.Id);
            return createdUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user with Email: {Email}, GoogleId: {GoogleId}", user.Email, user.GoogleId);
            throw;
        }
    }

    public async Task UpdateAsync(User user)
    {
        _logger.LogDebug("Attempting to update user with ID: {UserId}", user.Id);
        const string sql = """
            UPDATE "Users"
            SET "GoogleId" = @GoogleId,
                "Email" = @Email,
                "FirstName" = @FirstName,
                "LastName" = @LastName,
                "ProfilePictureUrl" = @ProfilePictureUrl,
                "LastLoginAt" = @LastLoginAt
            WHERE "Id" = @Id;
            """;
        try
        {
            using var connection = CreateConnection();
            int affectedRows = await connection.ExecuteAsync(sql, user);
            _logger.LogDebug("User update executed for ID: {UserId}. Rows affected: {AffectedRows}", user.Id, affectedRows);
            if (affectedRows == 0)
            {
                _logger.LogWarning("Update operation affected 0 rows for User ID: {UserId}. User might not exist.", user.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with ID: {UserId}", user.Id);
            throw;
        }
    }
} 