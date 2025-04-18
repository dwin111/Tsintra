using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsintra.Domain.Interfaces;
using Tsintra.Integrations;
using Tsintra.Persistence;
using Tsintra.Application.Services;
using Tsintra.Application.Interfaces;
using Tsintra.MarketplaceAgent;
using Tsintra.Application.Services.HostedServices;
using Tsintra.Application.Configuration;

namespace Tsintra.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApp(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ILLMServices, OpenAIServices>();
            services.AddScoped<IProductGenerationService, ProductGenerationService>();
            services.AddScoped<IAgentMemoryService, AgentMemoryService>();
            services.AddScoped<IAgentMemoryCleanupService, AgentMemoryCleanupService>();
            services.AddScoped<IAgentMemoryStatisticsService, AgentMemoryStatisticsService>();
            
            // Налаштування та реєстрація фонового сервісу очищення пам'яті
            services.Configure<AgentMemoryCleanupSettings>(
                configuration.GetSection(AgentMemoryCleanupSettings.SectionName));
            services.AddHostedService<AgentMemoryCleanupHostedService>();

            // Register chat services
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IRedisChatCacheService, RedisChatCacheService>();
            services.AddScoped<IChatCleanupService, ChatCleanupService>();
            services.AddHostedService<ChatCleanupHostedService>();

            services.AddPersistence(configuration);

            services.AddIntegrations(configuration);
            services.AddMarketplaceAgentServices(configuration);

            // Конфігурація Нової Пошти
            services.Configure<NovaPoshtaConfig>(configuration.GetSection("NovaPoshta"));

            // Register CRM services
            services.AddScoped<ICrmService, CrmService>();
            services.AddScoped<INovaPoshtaService, NovaPoshtaService>();

            // Add HttpClient configuration
            services.AddHttpClient("NovaPoshtaApi", client => {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            return services;
        }
    }
}
