using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using Tsintra.Domain.Interfaces;
using Tsintra.Infrastructure.Services;

namespace Tsintra.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Реєстрація Redis з налаштуваннями стійкості до помилок підключення
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                redisConnectionString = "localhost:6379";
                logger.LogWarning("Redis connection string not found, using default: {connectionString}", redisConnectionString);
            }

            try
            {
                // Make sure we have abortConnect=false in the connection string
                if (!redisConnectionString.Contains("abortConnect="))
                {
                    redisConnectionString = redisConnectionString + ",abortConnect=false";
                }

                // Parse the connection string to add more resilient settings
                var options = ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 5;
                options.ConnectTimeout = 5000;
                options.SyncTimeout = 5000;
                options.ResponseTimeout = 5000;

                logger.LogInformation("Initializing Redis connection with abortConnect=false");
                return ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to initialize Redis connection. A dummy multiplexer will be used");
                // Return a dummy ConnectionMultiplexer that will allow the application to start
                // but won't actually connect to Redis
                return GetDummyConnectionMultiplexer(logger);
            }
        });
        
        // Реєстрація сервісу кешування з підтримкою роботи без Redis
        services.AddScoped<ICacheService, RedisCacheService>();
        
        return services;
    }

    // Creates a dummy multiplexer that doesn't actually connect to Redis
    private static IConnectionMultiplexer GetDummyConnectionMultiplexer(ILogger logger)
    {
        try
        {
            // Configure a connection to a non-existent server with abortConnect=false
            // This will create a multiplexer that doesn't throw exceptions but also doesn't work
            var options = ConfigurationOptions.Parse("non-existent-server:6379,abortConnect=false");
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 0; // Don't retry connecting
            return ConnectionMultiplexer.Connect(options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating dummy connection multiplexer");
            throw;
        }
    }
} 