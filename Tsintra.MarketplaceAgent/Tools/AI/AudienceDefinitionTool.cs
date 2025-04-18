using System.Text.Json;
// Corrected using statements
using Microsoft.Extensions.Logging;
using System.Text;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.Models.AI;
using Tsintra.MarketplaceAgent.Models.Core; // For StringBuilder

// Remove OpenAI specific usings
// using OpenAI.Chat;

namespace Tsintra.MarketplaceAgent.Tools.AI
{
    // Assuming input is the MarketAnalysis JSON string, output is JSON string for Audience
    public class AudienceDefinitionTool : ITool<string, string>
    {
        private readonly ILogger<AudienceDefinitionTool> _logger;
        // Inject the INTERFACE
        private readonly IAiChatCompletionService _aiChatService;

        // Update constructor
        public AudienceDefinitionTool(ILogger<AudienceDefinitionTool> logger, IAiChatCompletionService aiChatService)
        {
            _logger = logger;
            _aiChatService = aiChatService; // Store the interface instance
            _logger.LogInformation("[{ToolName}] Initialized.", Name);
        }

        public string Name => "AudienceDefinition";
        public string Description => "Defines the target audience based on market analysis data using AI.";

        // Updated RunAsync signature
        public async Task<string> RunAsync(string marketAnalysisJson, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[{ToolName}] Defining target audience...", Name);
            if (string.IsNullOrWhiteSpace(marketAnalysisJson))
            {
                _logger.LogWarning("[{ToolName}] Input market analysis JSON is empty.", Name);
                return "{}";
            }

            try
            {
                // --- Prompt Generation (Example) ---
                var systemPromptBuilder = new StringBuilder();
                systemPromptBuilder.AppendLine("You are a marketing strategist specializing in audience segmentation.");
                systemPromptBuilder.AppendLine("Based on the provided market analysis data, define the primary target audience for the product.");
                systemPromptBuilder.AppendLine("Describe the audience in terms of demographics, psychographics, needs, and pain points.");
                systemPromptBuilder.AppendLine("Respond ONLY with a valid JSON object containing the following fields:");
                systemPromptBuilder.AppendLine("  - \"audienceProfile\": string (Detailed description of the target audience)");
                systemPromptBuilder.AppendLine("  - \"keyCharacteristics\": List<string> (Bullet points of key audience traits)");
                string systemPrompt = systemPromptBuilder.ToString();

                var userInputBuilder = new StringBuilder();
                userInputBuilder.AppendLine("Define the target audience based on this market analysis:");
                userInputBuilder.AppendLine(marketAnalysisJson);
                string userInput = userInputBuilder.ToString();
                // --- End Prompt Generation ---

                var messages = new List<AiChatMessage> { AiChatMessage.Create(ChatMessageRole.System, systemPrompt), AiChatMessage.Create(ChatMessageRole.User, userInput) };
                var options = new AiCompletionOptions { Temperature = 0.4f, MaxTokens = 800, ResponseFormat = ChatResponseFormatType.JsonObject };

                _logger.LogInformation("[{ToolName}] Sending request to AI service for audience definition...", Name);
                string? jsonResponse = await _aiChatService.GetCompletionAsync(messages, options, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                     _logger.LogInformation("[{ToolName}] Audience definition cancelled.", Name);
                     return "{\"error\": \"Operation cancelled\"}";
                }
                
                if (jsonResponse == null) 
                { 
                    _logger.LogError("[{ToolName}] AI service returned null response for audience definition.", Name);
                    return "{\"error\": \"Error communicating with AI API for audience definition\"}"; 
                }
                
                try 
                { 
                    using (JsonDocument.Parse(jsonResponse)) { } 
                    _logger.LogInformation("[{ToolName}] Successfully received and validated JSON response for audience definition.", Name);
                    return jsonResponse; 
                }
                catch (JsonException jex) 
                { 
                     _logger.LogError(jex, "[{ToolName}] Failed to parse AI audience result as JSON. Response: {Response}", Name, jsonResponse);
                     return "{\"error\": \"Failed to parse AI audience result as JSON\"}"; 
                }
            }
             catch (OperationCanceledException)
            {
                 _logger.LogInformation("[{ToolName}] Audience definition operation cancelled.", Name);
                 return "{\"error\": \"Operation cancelled\"}";
            }
            catch (Exception ex) 
            { 
                 _logger.LogError(ex, "[{ToolName}] Internal tool error occurred during audience definition.", Name);
                 return "{\"error\": \"Internal tool error occurred during audience definition execution.\"}"; 
            }
        }
    }
} 