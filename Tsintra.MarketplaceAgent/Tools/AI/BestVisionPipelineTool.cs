using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.Models.AI;
using Tsintra.MarketplaceAgent.Models.Core;
using System.Diagnostics;
using Tsintra.Integrations.Interfaces;
using Microsoft.Extensions.Options;
using Tsintra.Integrations;

namespace Tsintra.MarketplaceAgent.Tools.AI
{
    public class BestVisionPipelineTool : ITool<VisionPipelineInput, string>
    {
        private readonly ILogger<BestVisionPipelineTool> _logger;
        private readonly IAiChatCompletionService _aiChatService;
        private readonly IMemoryCache _cache;
        private readonly IS3StorageService _s3StorageService;
        private readonly string _s3BucketName;

        public BestVisionPipelineTool(ILogger<BestVisionPipelineTool> logger,
                                      IAiChatCompletionService aiChatService,
                                      IMemoryCache cache,
                                      IS3StorageService s3StorageService,
                                      IOptions<AwsOptions> awsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _aiChatService = aiChatService ?? throw new ArgumentNullException(nameof(aiChatService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _s3StorageService = s3StorageService ?? throw new ArgumentNullException(nameof(s3StorageService));
            var options = awsOptions?.Value ?? throw new ArgumentNullException(nameof(awsOptions));
            _s3BucketName = options.S3?.BucketName ?? throw new InvalidOperationException("S3 BucketName is not configured in AwsOptions.");
            _logger.LogInformation("[{ToolName}] Initialized.", Name);
        }
        public string Name => "BestVisionPipeline";
        public string Description => "Analyzes images (downloaded from S3) using AI to identify the product, describe scene, extract features.";

        public async Task<string> RunAsync(VisionPipelineInput input, CancellationToken cancellationToken = default)
        {
            var toolStopwatch = Stopwatch.StartNew();
            var stepStopwatch = new Stopwatch();
            var timings = new List<KeyValuePair<string, long>>();

            _logger.LogInformation("[{ToolName}] Running...", Name);
            
            stepStopwatch.Start();
            if (input == null || input.ImageCacheKeys == null || !input.ImageCacheKeys.Any())
            {
                _logger.LogWarning("[{ToolName}] Input VisionPipelineInput is null or has no ImageCacheKeys.", Name);
                return "{}";
            }
            timings.Add(new KeyValuePair<string, long>("Input Validation", stepStopwatch.ElapsedMilliseconds));
            stepStopwatch.Restart();

            string imageCacheKeysHash = string.Join("|", input.ImageCacheKeys.Select(k => k?.GetHashCode() ?? 0));
            string cacheKey = $"{Name}_{input.Language}_{input.Hints?.GetHashCode() ?? 0}_{imageCacheKeysHash}";
            timings.Add(new KeyValuePair<string, long>("Cache Key Generation", stepStopwatch.ElapsedMilliseconds));
            stepStopwatch.Restart();

            if (_cache.TryGetValue(cacheKey, out string cachedResult))
            {
                timings.Add(new KeyValuePair<string, long>("Cache Check (Hit)", stepStopwatch.ElapsedMilliseconds));
                _logger.LogInformation("[{ToolName}] Cache hit for key: {CacheKey}. Returning cached result.", Name, cacheKey);
                LogTimingReport(timings, toolStopwatch.ElapsedMilliseconds);
                return cachedResult;
            }
            timings.Add(new KeyValuePair<string, long>("Cache Check (Miss)", stepStopwatch.ElapsedMilliseconds));
            _logger.LogInformation("[{ToolName}] Cache miss for key: {CacheKey}. Preparing API call...", Name, cacheKey);
            
            if (input.Hints?.IndexOf("не викон", StringComparison.OrdinalIgnoreCase) >= 0) 
            { 
                _logger.LogWarning("[{ToolName}] Skipping execution due to 'не викон' hint.", Name);
                return "{}"; 
            }

            string jsonResponse = "{}";
            try
            {
                stepStopwatch.Restart();
                var sb = new StringBuilder();
                sb.AppendLine($"You are an expert in computer vision and product analysis.");
                sb.AppendLine($"Analyze the provided image(s) to identify the main product and describe the scene context.");
                sb.AppendLine($"Please respond in {input.Language}."); 
                sb.AppendLine("User-supplied hints provide additional context or requirements that MUST be considered."); 
                if (!string.IsNullOrWhiteSpace(input.Hints)) { sb.AppendLine($"Context hints from user: {input.Hints}"); } 
                sb.AppendLine("Return EXACTLY ONE valid JSON object with the following structure:");
                sb.AppendLine("{\"productName\": \"<...Name>\", \"sceneDescription\": \"<...Desc>\", \"keyFeatures\": [\"<...>\"], \"confidenceScore\": <0.0-1.0>}");
                sb.AppendLine("Ensure the JSON is well-formed.");
                string systemPrompt = sb.ToString();
                timings.Add(new KeyValuePair<string, long>("System Prompt Build", stepStopwatch.ElapsedMilliseconds));

                stepStopwatch.Restart();
                var userContentParts = new List<ChatMessageContentPart>();
                _logger.LogInformation("[{ToolName}] Downloading images from S3 for {Count} image cache keys...", Name, input.ImageCacheKeys.Count);
                foreach (var imageCacheKey in input.ImageCacheKeys)
                {
                    if (string.IsNullOrWhiteSpace(imageCacheKey)) continue;
                    Stream? imageStream = null;
                    byte[]? imageBytes = null;
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // First try to get S3 key from cache
                        if (_cache.TryGetValue(imageCacheKey, out string? s3Key) && !string.IsNullOrEmpty(s3Key))
                        {
                            _logger.LogDebug("[{ToolName}] Cache hit for S3 key '{S3Key}' using cache key: {CacheKey}", Name, s3Key, imageCacheKey);

                            imageStream = await _s3StorageService.DownloadFileAsStreamAsync(_s3BucketName, s3Key, cancellationToken);

                            if (imageStream != null)
                            {
                                using var memoryStream = new MemoryStream();
                                await imageStream.CopyToAsync(memoryStream, cancellationToken);
                                imageBytes = memoryStream.ToArray();
                                _logger.LogDebug("[{ToolName}] Downloaded {BytesLength} bytes from S3 key {S3Key}", Name, imageBytes.Length, s3Key);
                            }
                            else
                            {
                                _logger.LogWarning("[{ToolName}] Failed to download image stream from S3 key: {S3Key}. Checking for base64 fallback.", Name, s3Key);
                                
                                // Check if we have a base64 fallback
                                if (_cache.TryGetValue($"base64-{s3Key}", out string? base64Image) && !string.IsNullOrEmpty(base64Image))
                                {
                                    _logger.LogDebug("[{ToolName}] Found base64 fallback for S3 key: {S3Key}", Name, s3Key);
                                    imageBytes = Convert.FromBase64String(base64Image);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[{ToolName}] Cache miss or empty S3 key found for cache key: {CacheKey}. Checking for base64 fallback.", Name, imageCacheKey);
                            
                            // Check if this is a base64 cache key
                            if (imageCacheKey.StartsWith("base64-") && _cache.TryGetValue(imageCacheKey, out string? base64Image) && !string.IsNullOrEmpty(base64Image))
                            {
                                _logger.LogDebug("[{ToolName}] Found base64 image for cache key: {CacheKey}", Name, imageCacheKey);
                                imageBytes = Convert.FromBase64String(base64Image);
                            }
                        }

                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            string mimeType = "image/png";
                            userContentParts.Add(ChatMessageContentPart.CreateImage(imageBytes, mimeType));
                            _logger.LogDebug("[{ToolName}] Added image bytes to AI request.", Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[{ToolName}] Error processing image for cache key: {CacheKey}", Name, imageCacheKey);
                    }
                    finally
                    {
                        imageStream?.Dispose();
                    }
                }

                if (!userContentParts.Any())
                { _logger.LogWarning("[{ToolName}] No valid images could be processed from S3.", Name); return "{}"; }
                timings.Add(new KeyValuePair<string, long>("Image Download & Processing (S3)", stepStopwatch.ElapsedMilliseconds));

                var messages = new List<AiChatMessage> { AiChatMessage.Create(ChatMessageRole.System, systemPrompt), AiChatMessage.Create(ChatMessageRole.User, userContentParts) };
                var options = new AiCompletionOptions { Temperature = 0.3f, MaxTokens = 2048, ResponseFormat = ChatResponseFormatType.JsonObject };

                stepStopwatch.Restart();
                _logger.LogInformation("[{ToolName}] Sending request to AI service with image bytes...", Name);
                string? apiResponse = await _aiChatService.GetCompletionAsync(messages, options, cancellationToken);
                timings.Add(new KeyValuePair<string, long>("AI API Call (GetCompletionAsync)", stepStopwatch.ElapsedMilliseconds));

                stepStopwatch.Restart();
                if (apiResponse == null) 
                { _logger.LogError("[{ToolName}] AI service returned null response.", Name); return "{\"error\": \"Error communicating with AI API via service\"}"; } 
                try { using (JsonDocument.Parse(apiResponse)) { } _logger.LogInformation("[{ToolName}] Successfully received and validated JSON response from AI service.", Name); _cache.Set(cacheKey, apiResponse, TimeSpan.FromHours(2)); jsonResponse = apiResponse; timings.Add(new KeyValuePair<string, long>("Response Validation & Caching", stepStopwatch.ElapsedMilliseconds)); }
                catch (JsonException jex) { _logger.LogError(jex, "[{ToolName}] Failed to parse AI service response as JSON. Response: {Response}", Name, apiResponse); jsonResponse = "{\"error\": \"Failed to parse AI service response as JSON\"}"; }
            }
            catch (OperationCanceledException)
            { _logger.LogInformation("[{ToolName}] Vision pipeline operation cancelled.", Name); jsonResponse = "{\"error\": \"Operation cancelled\"}"; }
            catch (Exception ex)
            { _logger.LogError(ex, "[{ToolName}] Internal tool error occurred.", Name); jsonResponse = "{\"error\": \"Internal tool error occurred during vision pipeline execution.\"}"; }
            finally
            { LogTimingReport(timings, toolStopwatch.ElapsedMilliseconds); }
            return jsonResponse;
        }

        private void LogTimingReport(List<KeyValuePair<string, long>> timings, long totalMs)
        {
            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine($"--- [{Name}] Internal Performance Report ---");
            long accountedMs = 0;
            if (timings != null && timings.Any())
            { foreach (var timing in timings) { reportBuilder.AppendLine($"  - {timing.Key}: {timing.Value} ms"); accountedMs += timing.Value; } }
            else { reportBuilder.AppendLine("  (No timing data collected)"); }
            reportBuilder.AppendLine($"  - Unaccounted Time: {totalMs - accountedMs} ms");
            reportBuilder.AppendLine("-----------------------------------------");
            reportBuilder.AppendLine($"  Total Tool Time: {totalMs} ms");
            reportBuilder.AppendLine("-----------------------------------------");
            _logger.LogInformation(reportBuilder.ToString());
        }
    }
}