using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsintra.App.Interfaces;
using Tsintra.Integrations.OpenAI;
using Tsintra.Integrations.OpenAI.Mapping;
using Tsintra.Integrations.OpenAI.Models;

namespace Tsintra.Integrations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<OpenAIConnect>(configuration.GetSection("OpenAI"));

            services.AddSingleton<ILLMClient, OpenAiLLMClient>();
            services.AddSingleton<OpenAiLLMClient>();

            return services;
        }
    }
}
