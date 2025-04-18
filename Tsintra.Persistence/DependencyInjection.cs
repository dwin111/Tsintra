using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Persistence.Repositories;
using Tsintra.Persistence.Context;
using FluentMigrator.Runner;
using System.Reflection;

namespace Tsintra.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DatabaseContext
        services.AddSingleton<DatabaseContext>(sp => 
            new DatabaseContext(
                configuration, 
                sp.GetRequiredService<ILogger<DatabaseContext>>()
            ));

        // Register repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAgentMemoryRepository, AgentMemoryRepository>();
        services.AddScoped<IAgentLongTermMemoryRepository, AgentLongTermMemoryRepository>();
        services.AddScoped<IAgentConversationMemoryRepository, AgentConversationMemoryRepository>();
        services.AddScoped<IPromRepository, PromRepository>();
        services.AddScoped<IConversationRepository>(_ => 
            new ConversationRepository(
                configuration.GetConnectionString("DefaultConnection"), 
                _.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ConversationRepository>>()));
        services.AddScoped<IChatRepository>(_ => 
            new ChatRepository(
                configuration.GetConnectionString("DefaultConnection"), 
                _.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ChatRepository>>()));

        return services;
    }

    /// <summary>
    /// Ініціалізує базу даних - створює таблиці, якщо вони ще не існують
    /// </summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        await dbContext.InitializeDatabaseAsync();
    }

    /// <summary>
    /// Додає FluentMigrator і налаштовує сервіси для виконання міграцій
    /// </summary>
    public static IServiceCollection AddFluentMigrator(this IServiceCollection services, string connectionString)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }

    /// <summary>
    /// Виконує міграції бази даних
    /// </summary>
    /// <param name="configuration">Об'єкт конфігурації</param>
    /// <param name="logger">Логер для виводу повідомлень (опціонально)</param>
    /// <returns>true, якщо міграції виконані успішно</returns>
    public static bool RunMigrations(IConfiguration configuration, ILogger logger = null)
    {
        try
        {
            // Отримання рядка підключення
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                logger?.LogError("Connection string 'DefaultConnection' not found in configuration");
                return false;
            }

            // Створення провайдера сервісів з FluentMigrator
            var serviceProvider = new ServiceCollection()
                .AddFluentMigrator(connectionString)
                .BuildServiceProvider(false);

            // Запуск міграцій
            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                runner.MigrateUp();
            }

            logger?.LogInformation("Міграції успішно виконані");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Помилка при виконанні міграцій");
            return false;
        }
    }
} 