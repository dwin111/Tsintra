using System.Text; // For StringBuilder
using Microsoft.Extensions.Logging;
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.Models.AI;

namespace Tsintra.MarketplaceAgent.Tools.AI
{
    // CaptionInput definition removed, assuming it's in MarketplaceAgentApp.DTOs

    // Updated to implement ITool<CaptionInput, string>
    public class InstagramCaptionTool : ITool<CaptionInput, string>
    {
        private readonly ILogger<InstagramCaptionTool> _logger;
        private readonly IAiChatCompletionService _aiChatService;

        public InstagramCaptionTool(ILogger<InstagramCaptionTool> logger, IAiChatCompletionService aiChatService)
        {
            _logger = logger; _aiChatService = aiChatService;
            _logger.LogInformation("[{ToolName}] Initialized.", Name);
        }
        public string Name => "InstagramCaption";
        public string Description => "Generates an Instagram caption based on product details, market analysis, audience, and refined content.";

        // Updated RunAsync signature
        public async Task<string> RunAsync(CaptionInput input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[{ToolName}] Generating Instagram caption...", Name);
            if (input == null)
            {
                _logger.LogWarning("[{ToolName}] Input CaptionInput is null.", Name);
                return ""; // Return empty string on invalid input
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // --- Prompt Generation (Example) ---
                var systemPromptBuilder = new StringBuilder();
                systemPromptBuilder.AppendLine("You are a social media marketing expert specializing in Instagram content for e-commerce.");
                systemPromptBuilder.AppendLine("Your task is to generate an engaging Instagram caption for the product described in the provided JSON data.");
                systemPromptBuilder.AppendLine("Use the product details, market analysis, audience profile, and refined content to create a compelling caption.");
                systemPromptBuilder.AppendLine("Include relevant hashtags (3-5 popular and niche). Incorporate emojis where appropriate.");
                systemPromptBuilder.AppendLine("The caption should encourage engagement (e.g., ask a question, prompt to tag a friend).");
                systemPromptBuilder.AppendLine($"Adapt the tone and style based on the target audience and product type. The target language is {input.Language}.");
                systemPromptBuilder.AppendLine("Respond ONLY with the generated caption text (string), without any introductory phrases or JSON formatting.");
                string systemPrompt = systemPromptBuilder.ToString();

                var userInputBuilder = new StringBuilder();
                userInputBuilder.AppendLine("Generate an Instagram caption based on the following data:");
                userInputBuilder.AppendLine("\n--- Product Info (from Vision) ---");
                userInputBuilder.AppendLine(input.ProductJson);
                userInputBuilder.AppendLine("\n--- Market Analysis --- ");
                userInputBuilder.AppendLine(input.MarketAnalysisJson);
                userInputBuilder.AppendLine("\n--- Target Audience --- ");
                userInputBuilder.AppendLine(input.AudienceJson);
                 userInputBuilder.AppendLine("\n--- Refined Content --- ");
                 userInputBuilder.AppendLine(input.RefinedJson);
                 string userInput = userInputBuilder.ToString();
                // --- End Prompt Generation ---

                var messages = new List<AiChatMessage> { AiChatMessage.Create(ChatMessageRole.System, systemPrompt), AiChatMessage.Create(ChatMessageRole.User, userInput) };
                // Note: We expect plain text output, so ResponseFormat is not set to JsonObject
                var options = new AiCompletionOptions { Temperature = 0.7f, MaxTokens = 300 }; 

                 _logger.LogInformation("[{ToolName}] Sending request to AI service for Instagram caption...", Name);
                string? captionResponse = await _aiChatService.GetCompletionAsync(messages, options, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                     _logger.LogInformation("[{ToolName}] Instagram caption generation cancelled.", Name);
                     return "Caption generation cancelled."; // Return specific message
                }

                if (string.IsNullOrWhiteSpace(captionResponse))
                { 
                    _logger.LogError("[{ToolName}] AI service returned null or empty response for Instagram caption.", Name);
                    return "Error: Could not generate caption."; 
                }

                _logger.LogInformation("[{ToolName}] Instagram caption generated successfully.", Name);
                // Clean up potential markdown code fences if the model adds them anyway
                if (captionResponse.StartsWith("```")) captionResponse = captionResponse.Substring(3);                
                if (captionResponse.EndsWith("```")) captionResponse = captionResponse.Substring(0, captionResponse.Length - 3);
                
                return captionResponse.Trim();
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInformation("[{ToolName}] Instagram caption generation operation cancelled.", Name);
                 return "Caption generation cancelled.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ToolName}] Internal tool error occurred during Instagram caption generation.", Name);
                return "Error: Internal tool error generating caption.";
            }
        }
    }
} 