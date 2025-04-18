using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Tsintra.MarketplaceAgent.Interfaces; // For IAiChatCompletionService
using Tsintra.MarketplaceAgent.Models.AI; // For AiChatMessage, etc.
using Tsintra.MarketplaceAgent.Configuration; // For ChatServiceConfig, AiCompletionOptions
using System.Diagnostics; // Added for Stopwatch
using System.Text; // Added for StringBuilder
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Any/Min
using System; // Added for ArgumentException etc.
using System.Threading.Tasks; // Added for Task
using System.Threading; // Added for CancellationToken

namespace Tsintra.MarketplaceAgent.Services;
public class OpenAiChatService : IAiChatCompletionService // Implement the interface
{
    private readonly ILogger<OpenAiChatService> _logger;
    private readonly ChatClient _chatClient;
    private readonly ChatServiceConfig _config;

    public OpenAiChatService(IOptions<ChatServiceConfig> config, ILogger<OpenAiChatService> logger)
    {
        _logger = logger;
        _config = config.Value;

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogError("OpenAI API Key is not configured.");
            throw new InvalidOperationException("OpenAI API Key is missing in configuration.");
        }

        // Consider adding error handling for client creation if needed
        _chatClient = new ChatClient(_config.Model ?? "gpt-4o", _config.ApiKey);
        _logger.LogInformation("OpenAiChatService initialized with model: {Model}", _config.Model ?? "gpt-4o");
    }

    // Implement the interface method
    public async Task<string?> GetCompletionAsync(List<AiChatMessage> messages, AiCompletionOptions options, CancellationToken cancellationToken = default)
    {
        var methodStopwatch = Stopwatch.StartNew();
        var stepStopwatch = new Stopwatch();
        var timings = new List<KeyValuePair<string, long>>();
        string? resultText = null;

        try
        {
            stepStopwatch.Start();
            var openAiMessages = ConvertToOpenAiMessages(messages);
            timings.Add(new KeyValuePair<string, long>("Convert Messages", stepStopwatch.ElapsedMilliseconds));
            
            stepStopwatch.Restart();
            var openAiOptions = ConvertToOpenAiOptions(options);
            timings.Add(new KeyValuePair<string, long>("Convert Options", stepStopwatch.ElapsedMilliseconds));

            _logger.LogDebug("Sending chat completion request to OpenAI...");
            stepStopwatch.Restart();
            ChatCompletion completion = await _chatClient.CompleteChatAsync(openAiMessages, openAiOptions, cancellationToken);
            timings.Add(new KeyValuePair<string, long>("API Call (CompleteChatAsync)", stepStopwatch.ElapsedMilliseconds));

            stepStopwatch.Restart();
            if (completion?.Content?.Count > 0)
            {
                resultText = completion.Content[0].Text?.Trim();
                int logLength = Math.Min(resultText?.Length ?? 0, 100);
                _logger.LogDebug("Received successful response from OpenAI (first {Length} chars): {ResponseStart}...", logLength, resultText?.Substring(0, logLength) ?? "NULL");
            }
            else
            {
                _logger.LogWarning("Received an empty or invalid response from OpenAI.");
            }
            timings.Add(new KeyValuePair<string, long>("Process Response", stepStopwatch.ElapsedMilliseconds));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OpenAI request was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI Chat API.");
            resultText = null; // Ensure null on error
        }
        finally
        {
             LogTimingReport(timings, methodStopwatch.ElapsedMilliseconds);
        }
        return resultText;
    }

    // --- Conversion Helper Methods ---

    private List<ChatMessage> ConvertToOpenAiMessages(List<AiChatMessage> genericMessages)
    {
        var openAiMessages = new List<ChatMessage>();

        foreach (var gm in genericMessages)
        {
            switch (gm.Role)
            {
                case Models.AI.ChatMessageRole.System:
                    if (gm.Content.Count == 1 && gm.Content[0].Type == Models.AI.ChatMessageContentPart.PartType.Text)
                    {
                        openAiMessages.Add(new SystemChatMessage(gm.Content[0].Text!));
                    }
                    else
                    {
                        throw new ArgumentException("System message must have exactly one text content part.");
                    }
                    break;

                case Models.AI.ChatMessageRole.User:
                    var userContentParts = new List<OpenAI.Chat.ChatMessageContentPart>(); // Explicitly use SDK type
                    foreach (var cp in gm.Content)
                    {
                        OpenAI.Chat.ChatMessageContentPart sdkPart; // Use fully qualified SDK type
                        switch (cp.Type)
                        {
                            case Models.AI.ChatMessageContentPart.PartType.Text:
                                // Use correct static factory method: CreateTextPart
                                sdkPart = OpenAI.Chat.ChatMessageContentPart.CreateTextPart(cp.Text!);
                                break;
                            case Models.AI.ChatMessageContentPart.PartType.Image:
                                if (cp.ImageData == null) throw new ArgumentException("Image data is null.");
                                // Use correct static factory method: CreateImagePart, wrapping byte[] in BinaryData
                                sdkPart = OpenAI.Chat.ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(cp.ImageData), cp.MediaType!);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(cp.Type), $"Unsupported content part type: {cp.Type}");
                        }
                        userContentParts.Add(sdkPart);
                    }
                    openAiMessages.Add(new UserChatMessage(userContentParts));
                    break;

                case Models.AI.ChatMessageRole.Assistant:
                    if (gm.Content.Count == 1 && gm.Content[0].Type == Models.AI.ChatMessageContentPart.PartType.Text)
                    {
                        openAiMessages.Add(new AssistantChatMessage(gm.Content[0].Text!));
                    }
                    else
                    {
                        throw new ArgumentException("Assistant message conversion currently only supports single text part.");
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(gm.Role), $"Unsupported chat message role: {gm.Role}");
            }
        }
        return openAiMessages;
    }

    private ChatCompletionOptions ConvertToOpenAiOptions(AiCompletionOptions genericOptions)
    {
        var openAiOptions = new ChatCompletionOptions();

        if (genericOptions.Temperature.HasValue)
        {
            openAiOptions.Temperature = genericOptions.Temperature.Value;
        }
        if (genericOptions.MaxTokens.HasValue)
        {
            // Note: OpenAI SDK might use a different property name. Adjust if needed.
            // Assuming MaxOutputTokenCount is correct based on previous tool code.
            openAiOptions.MaxOutputTokenCount = genericOptions.MaxTokens.Value;
        }
        if (genericOptions.ResponseFormat.HasValue)
        {
            openAiOptions.ResponseFormat = genericOptions.ResponseFormat.Value switch
            {
                Models.Core.ChatResponseFormatType.Text => ChatResponseFormat.CreateTextFormat(), // Or null if Text is default?
                Models.Core.ChatResponseFormatType.JsonObject => ChatResponseFormat.CreateJsonObjectFormat(),
                _ => throw new ArgumentOutOfRangeException(nameof(genericOptions.ResponseFormat), $"Unsupported response format: {genericOptions.ResponseFormat.Value}")
            };
        }
        // Add conversion for other options (TopP, StopSequences) if they were added to AiCompletionOptions

        return openAiOptions;
    }

    // Helper method for logging timing report
    private void LogTimingReport(List<KeyValuePair<string, long>> timings, long totalMs)
    {
        var reportBuilder = new StringBuilder();
        reportBuilder.AppendLine($"--- [OpenAiChatService.GetCompletionAsync] Internal Performance Report ---");
        long accountedMs = 0;
        if (timings != null && timings.Any())
        {
            foreach (var timing in timings)
            {
                reportBuilder.AppendLine($"  - {timing.Key}: {timing.Value} ms");
                accountedMs += timing.Value;
            }
        }
        else
        {
            reportBuilder.AppendLine("  (No timing data collected)");
        }
        reportBuilder.AppendLine($"  - Unaccounted Time: {totalMs - accountedMs} ms");
        reportBuilder.AppendLine("---------------------------------------------------------------------");
        reportBuilder.AppendLine($"  Total Service Call Time: {totalMs} ms");
        reportBuilder.AppendLine("---------------------------------------------------------------------");
        _logger.LogInformation(reportBuilder.ToString()); // Use logger here
    }
}