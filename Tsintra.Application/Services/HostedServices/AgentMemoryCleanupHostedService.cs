using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tsintra.Application.Services.HostedServices;

public class AgentMemoryCleanupSettings
{
    public const string SectionName = "AgentMemoryCleanup";
    
    public int CleanupIntervalInMinutes { get; set; } = 60; // за замовчуванням 1 година
}

public class AgentMemoryCleanupHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentMemoryCleanupHostedService> _logger;
    private readonly AgentMemoryCleanupSettings _settings;
    
    public AgentMemoryCleanupHostedService(
        IServiceProvider serviceProvider,
        IOptions<AgentMemoryCleanupSettings> options,
        ILogger<AgentMemoryCleanupHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = options.Value;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Запуск фонового сервісу очищення застарілих записів пам'яті агента");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Початок періодичного очищення застарілих записів пам'яті агента");
                
                using var scope = _serviceProvider.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<IAgentMemoryCleanupService>();
                
                await cleanupService.CleanupExpiredMemoriesAsync(stoppingToken);
                
                _logger.LogDebug("Періодичне очищення застарілих записів пам'яті агента завершено");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка під час виконання періодичного очищення застарілих записів пам'яті агента");
            }
            
            // Затримка до наступного запуску
            var delayTimeSpan = TimeSpan.FromMinutes(_settings.CleanupIntervalInMinutes);
            _logger.LogDebug("Наступне очищення застарілих записів пам'яті заплановано через {Delay}", delayTimeSpan);
            
            await Task.Delay(delayTimeSpan, stoppingToken);
        }
    }
} 