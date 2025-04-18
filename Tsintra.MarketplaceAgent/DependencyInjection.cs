using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
// using Microsoft.Playwright; // No longer needed here
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.Agents;
using Tsintra.MarketplaceAgent.Services;
using Tsintra.MarketplaceAgent.Tools.Core;
using Tsintra.MarketplaceAgent.Tools.AI;
using Tsintra.MarketplaceAgent.Configuration; // For ChatServiceConfig, AiCompletionOptions, PublishingConfig
using Tsintra.Domain.Interfaces;
using System.Collections.Generic;
using Tsintra.MarketplaceAgent.DTOs; // Додаємо DTOs для типів інструментів

namespace Tsintra.MarketplaceAgent;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketplaceAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Agent options
        services.Configure<AgentConfig>(configuration.GetSection(AgentConfig.SectionName));
        
        // Configure CRM options
        services.Configure<CrmServiceConfig>(configuration.GetSection(CrmServiceConfig.SectionName));
        
        // Configure Publishing Tool options
        services.Configure<PublishingToolConfig>(configuration.GetSection(PublishingToolConfig.SectionName));
        
        // Configure Chat Service options
        services.Configure<ChatServiceConfig>(configuration.GetSection("ChatService"));
        
        // Register HTTP clients
        services.AddHttpClient<OpenAIAgent>();
        
        // Register AI services
        services.AddScoped<OpenAIAgent>();
        services.AddScoped<ILLMService, OpenAIAgent>();
        services.AddScoped<IAgent, OpenAIAgent>();
        services.AddScoped<IAiChatCompletionService, OpenAiChatService>();
        
        // Register core and AI tools
        services.AddScoped<ValidationTool>();
        services.AddScoped<PublishingTool>();
        services.AddScoped<ReverseImageSearchTool>();
        services.AddScoped<WebScraperTool>();
        services.AddScoped<PhotoCorrectionTool>();
        services.AddScoped<BestVisionPipelineTool>();
        services.AddScoped<DeepMarketAnalysisTool>();
        services.AddScoped<RefineContentTool>();
        services.AddScoped<AudienceDefinitionTool>();
        services.AddScoped<InstagramCaptionTool>();
        
        // Реєстрація інструментів через їх інтерфейси
        services.AddScoped<ITool<PhotoProcessingInputS3, List<string>>, PhotoCorrectionTool>();
        services.AddScoped<ITool<VisionPipelineInput, string>, BestVisionPipelineTool>();
        services.AddScoped<ITool<ReverseImageSearchInput, string>, ReverseImageSearchTool>();
        services.AddScoped<ITool<WebScraperInput, string>, WebScraperTool>();
        services.AddScoped<ITool<MarketAnalysisInput, string>, DeepMarketAnalysisTool>();
        services.AddScoped<ITool<RefineContentInput, string>, RefineContentTool>();
        services.AddScoped<ITool<string, string>, AudienceDefinitionTool>();
        services.AddScoped<ITool<CaptionInput, string>, InstagramCaptionTool>();
        
        // Register agents
        services.AddScoped<ListingAgent>();
        services.AddScoped<ChatAgent>();
        services.AddScoped<IProductDescriptionAgent, ProductDescriptionAgent>();
        services.AddScoped<IProductGenerationTools, ListingAgent>();
        
        return services;
    }
} 