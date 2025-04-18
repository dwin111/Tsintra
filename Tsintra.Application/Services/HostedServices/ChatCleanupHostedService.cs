using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tsintra.Application.Services.HostedServices
{
    public class ChatCleanupHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ChatCleanupHostedService> _logger;
        private readonly TimeSpan _syncInterval = TimeSpan.FromHours(6); // Run every 6 hours

        public ChatCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<ChatCleanupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Chat cleanup service is starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Running chat sync operation...");

                try
                {
                    // We need to create a scope to use scoped services like IChatCleanupService
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var cleanupService = scope.ServiceProvider.GetRequiredService<IChatCleanupService>();
                        
                        // Sync Redis with database
                        await cleanupService.SyncRedisWithDatabaseAsync(stoppingToken);
                    }

                    _logger.LogInformation("Chat sync completed successfully. Next run in {interval} hours.", 
                        _syncInterval.TotalHours);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while running chat sync operation.");
                }

                // Wait for the next interval or until stopped
                try
                {
                    await Task.Delay(_syncInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping, break the loop
                    break;
                }
            }

            _logger.LogInformation("Chat cleanup service is stopping...");
        }
    }
} 