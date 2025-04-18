using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tsintra.MarketplaceAgent.Interfaces; // For ITool, IAiChatCompletionService
using Tsintra.MarketplaceAgent.DTOs; // For ProductDetailsDto, PublishResultDto, etc.
using Tsintra.MarketplaceAgent.Tools.Core; // For core tools
using Tsintra.MarketplaceAgent.Tools.AI; // For AI tools
using Tsintra.MarketplaceAgent.Models.AI; // For AI models if needed directly
using Tsintra.MarketplaceAgent.Configuration; // For AI configuration if needed directly
using System.Text.Json;
using Tsintra.MarketplaceAgent.Models;
using Microsoft.Playwright; // Потрібно для IBrowser
using SixLabors.Fonts; // Для конфігурацій
using Microsoft.Extensions.DependencyInjection; // Added for IServiceProvider
using Tsintra.MarketplaceAgent.Models.Core;
using SixLabors.ImageSharp; // Added for Color parsing and saving images
using System.IO; // Added for Path operations
using Microsoft.Extensions.Caching.Memory; // Added for IMemoryCache
using System.Diagnostics; // Added for Stopwatch
using System.Text; // Added for StringBuilder in report
using Tsintra.Integrations.Interfaces; // *** ADDED for S3 ***
using Microsoft.Extensions.Options; // *** ADDED for Options ***
using Tsintra.Integrations; // *** ADDED for AwsOptions ***
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.DTOs;
using Tsintra.Domain.Models; // *** ADDED for AgentMemory ***
using Tsintra.MarketplaceAgent.DTOs;
using System.Text.RegularExpressions;

namespace Tsintra.MarketplaceAgent.Agents;

public class ListingAgent : IProductGenerationTools
{
    private readonly ILogger<ListingAgent> _logger;
    private readonly IServiceProvider _serviceProvider; // Inject IServiceProvider
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache; // Added IMemoryCache
    private readonly IS3StorageService _s3StorageService; // *** ADDED ***
    private readonly AwsOptions _awsOptions; // *** ADDED ***
    private readonly string _s3BucketName; // *** ADDED ***
    private readonly IAgentMemoryService _agentMemoryService; // *** ADDED for memory ***
    private Guid _clientUserId; // Changed to non-readonly so it can be set later

    // Constructor injection
    public ListingAgent(
        ILogger<ListingAgent> logger,
        IServiceProvider serviceProvider, // Inject IServiceProvider
        HttpClient httpClient,
        IMemoryCache memoryCache, // Inject IMemoryCache
        IS3StorageService s3StorageService, // *** ADDED ***
        IOptions<AwsOptions> awsOptions, // *** ADDED ***
        IAgentMemoryService agentMemoryService // *** ADDED for memory ***
        // Removed clientUserId parameter - will be set through a method instead
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _s3StorageService = s3StorageService ?? throw new ArgumentNullException(nameof(s3StorageService)); // *** ADDED ***
        _awsOptions = awsOptions?.Value ?? throw new ArgumentNullException(nameof(awsOptions)); // *** ADDED ***
        _s3BucketName = _awsOptions.S3?.BucketName ?? throw new InvalidOperationException("S3 BucketName is not configured in AwsOptions."); // *** ADDED ***
        _agentMemoryService = agentMemoryService ?? throw new ArgumentNullException(nameof(agentMemoryService)); // *** ADDED ***
        _clientUserId = Guid.Empty; // Default empty value

        _logger.LogInformation("ListingAgent initialized. Tools will be resolved via IServiceProvider. MemoryCache, S3StorageService and AgentMemory injected.");
    }

    /// <summary>
    /// Sets the client user ID for memory operations.
    /// </summary>
    /// <param name="userId">The user ID to use for memory operations</param>
    public void SetClientUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }
        _clientUserId = userId;
        _logger.LogInformation("Client user ID set to {UserId}", userId);
    }

    // *** MODIFIED: method to update agent memory with new actions ***
    private async Task SaveActionToMemoryAsync(string conversationId, string action, object data, CancellationToken cancellationToken = default)
    {
        try
        {
            // Створюємо новий запис дії
            var newAction = new
            {
                Action = action,
                Timestamp = DateTime.UtcNow,
                Data = data
            };

            // Отримуємо існуючий запис пам'яті (якщо є)
            var existingMemory = await _agentMemoryService.GetMemoryAsync(_clientUserId, conversationId);
            List<object> memoryActions = new List<object>();

            if (existingMemory != null && !string.IsNullOrEmpty(existingMemory.Content))
            {
                try
                {
                    memoryActions = JsonSerializer.Deserialize<List<object>>(existingMemory.Content) ?? new List<object>();
                }
                catch (JsonException)
                {
                    memoryActions = new List<object>();
                }
            }

            memoryActions.Add(newAction);

            // Серіалізуємо оновлений список дій
            string serializedActions = JsonSerializer.Serialize(memoryActions);

            // Створюємо або оновлюємо запис пам'яті
            await _agentMemoryService.SaveMemoryAsync(new AgentMemory
            {
                Id = existingMemory?.Id ?? Guid.NewGuid(),
                UserId = _clientUserId,
                ConversationId = conversationId,
                Content = serializedActions,
                CreatedAt = existingMemory?.CreatedAt ?? DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30) // Зберігаємо на 30 днів
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save action to memory: {ExceptionMessage}", ex.Message);
        }
    }

    // *** MODIFIED: method to get agent memory history (now returns List<object>) ***
    private async Task<List<object>> GetMemoryHistoryAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var memory = await _agentMemoryService.GetMemoryAsync(_clientUserId, conversationId);
            if (memory != null && !string.IsNullOrEmpty(memory.Content))
            {
                try
                {
                    // Спочатку намагаємося десеріалізувати як список об'єктів
                    var memoryActions = JsonSerializer.Deserialize<List<object>>(memory.Content);
                    if (memoryActions != null)
                    {
                        _logger.LogDebug("Retrieved {Count} memory actions for conversation {ConversationId}", 
                            memoryActions.Count, conversationId);
                        return memoryActions;
                    }
                }
                catch
                {
                    // Якщо не вдалося як список, пробуємо як один об'єкт (для сумісності зі старим форматом)
                    try
                    {
                        var singleAction = JsonSerializer.Deserialize<object>(memory.Content);
                        if (singleAction != null)
                        {
                            _logger.LogDebug("Retrieved single memory action for conversation {ConversationId} (legacy format)", 
                                conversationId);
                            return new List<object> { singleAction };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize memory content as single object");
                    }
                }
            }
            
            _logger.LogDebug("No memory found or empty content for conversation {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent memory history for conversation {ConversationId}", conversationId);
        }
        
        return new List<object>();
    }

    private async Task<(TOutput Result, long ElapsedMilliseconds)> RunToolAsync<TInput, TOutput>(
        TInput input,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ITool<TInput, TOutput>? tool = _serviceProvider.GetService<ITool<TInput, TOutput>>();
        if (tool == null)
        {
            string errorMsg = $"Tool implementing ITool<{typeof(TInput).Name}, {typeof(TOutput).Name}> not found in DI container.";
            _logger.LogError(errorMsg);
             // Throw a more specific exception or return a default/error value
            throw new InvalidOperationException(errorMsg); 
        }

        string toolName = tool.Name;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Running tool '{ToolName}' with input type {InputType}...", toolName, typeof(TInput).Name);
            
            // Save tool execution start to memory
            await SaveActionToMemoryAsync(conversationId, $"StartedTool_{toolName}", new { 
                ToolName = toolName, 
                InputType = typeof(TInput).Name,
                Input = input
            }, cancellationToken);
            
            TOutput result = await tool.RunAsync(input, cancellationToken);
            stopwatch.Stop();
            long elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("Tool '{ToolName}' finished successfully in {ElapsedMilliseconds} ms.", toolName, elapsedMs);
            
            // Save tool execution result to memory
            await SaveActionToMemoryAsync(conversationId, $"CompletedTool_{toolName}", new { 
                ToolName = toolName, 
                ElapsedMs = elapsedMs,
                // For large results, consider storing a summary or limited portion
                Result = typeof(TOutput) == typeof(string) && result?.ToString()?.Length > 1000 
                    ? (object)(result?.ToString()?.Substring(0, 1000) + "...") 
                    : (object)result
            }, cancellationToken);
            
            return (result, elapsedMs); // Return result and time
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            long elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("Tool '{ToolName}' execution was cancelled after {ElapsedMilliseconds} ms.", toolName, elapsedMs);
            
            // Save tool execution cancellation to memory
            await SaveActionToMemoryAsync(conversationId, $"CancelledTool_{toolName}", new { 
                ToolName = toolName, 
                ElapsedMs = elapsedMs
            }, cancellationToken);
            
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            long elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, "Error running tool '{ToolName}' after {ElapsedMilliseconds} ms.", toolName, elapsedMs);
            
            // Save tool execution error to memory
            await SaveActionToMemoryAsync(conversationId, $"ErrorTool_{toolName}", new { 
                ToolName = toolName, 
                ElapsedMs = elapsedMs,
                ErrorMessage = ex.Message
            }, cancellationToken);
            
            throw new ToolExecutionException(toolName, ex.Message, ex);
        }
    }

    private async Task<JsonElement> RunToolAndParseJsonAsync<TInput>(TInput input, string conversationId, CancellationToken cancellationToken = default)
    {
         // Call RunToolAsync and discard the elapsed time
         (string jsonResult, _) = await RunToolAsync<TInput, string>(input, conversationId, cancellationToken);
         
         if (string.IsNullOrWhiteSpace(jsonResult)) { /* ... */ return JsonDocument.Parse("{}").RootElement; }
         try { using var doc = JsonDocument.Parse(jsonResult); return doc.RootElement.Clone(); }
         catch (JsonException ex) { /* ... */ return JsonDocument.Parse("{}").RootElement; }
    }

    private async Task<(string Result, long ElapsedMilliseconds)> RunToolAndGetStringAsync<TInput>(TInput input, string conversationId, CancellationToken cancellationToken = default)
    {
         return await RunToolAsync<TInput, string>(input, conversationId, cancellationToken);
    }

    private async Task<(TOutput Result, long ElapsedMilliseconds)> RunToolAndGetResultAsync<TInput, TOutput>(
        TInput input,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ITool<TInput, TOutput>? tool = _serviceProvider.GetService<ITool<TInput, TOutput>>();
        if (tool == null)
        {
            string errorMsg = $"Tool implementing ITool<{typeof(TInput).Name}, {typeof(TOutput).Name}> not found.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }
        string toolName = tool.Name;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Running tool '{ToolName}' expecting direct result type {OutputType}...", toolName, typeof(TOutput).Name);
            
            // Save tool execution start to memory
            await SaveActionToMemoryAsync(conversationId, $"StartedTool_{toolName}", new { 
                ToolName = toolName, 
                InputType = typeof(TInput).Name,
                Input = input
            }, cancellationToken);
            
            TOutput result = await tool.RunAsync(input, cancellationToken);
            stopwatch.Stop();
            long elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("Tool '{ToolName}' finished successfully in {ElapsedMilliseconds} ms.", toolName, elapsedMs);
            
            // Save tool execution result to memory
            await SaveActionToMemoryAsync(conversationId, $"CompletedTool_{toolName}", new { 
                ToolName = toolName, 
                ElapsedMs = elapsedMs,
                // For large results, consider storing a summary
                Result = typeof(TOutput) == typeof(string) && result?.ToString()?.Length > 1000 
                    ? (object)(result?.ToString()?.Substring(0, 1000) + "...")
                    : (object)result
            }, cancellationToken);
            
            return (result, elapsedMs); // Return result and time
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            long elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("Tool '{ToolName}' execution was cancelled after {ElapsedMilliseconds} ms.", toolName, elapsedMs);
            
            // Save tool execution cancellation to memory
            await SaveActionToMemoryAsync(conversationId, $"CancelledTool_{toolName}", new { 
                ToolName = toolName, 
                ElapsedMs = elapsedMs
            }, cancellationToken);
            
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            long elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, "Error running tool '{ToolName}' after {ElapsedMilliseconds} ms.", toolName, elapsedMs);
            
            // Save tool execution error to memory
            await SaveActionToMemoryAsync(conversationId, $"ErrorTool_{toolName}", new { 
                ToolName = toolName, 
                ElapsedMs = elapsedMs,
                ErrorMessage = ex.Message
            }, cancellationToken);
            
            throw new ToolExecutionException(toolName, ex.Message, ex);
        }
    }

    public async Task<MarketplaceProductDetailsDto?> GenerateProductAsync(
        IEnumerable<string> base64Images,
        string language = "ukr",
        string currency = "UAH",
        string? userHints = null,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        // Generate a conversation ID if not provided
        if (string.IsNullOrEmpty(conversationId))
        {
            conversationId = $"product-generation-{Guid.NewGuid():N}";
        }
        
        var totalStopwatch = Stopwatch.StartNew();
        var timingsReport = new List<KeyValuePair<string, long>>(); // List for the report
        var s3KeysToCleanUp = new List<string>(); // Track S3 keys for potential cleanup

        _logger.LogInformation("Starting product generation. Conversation ID: {ConversationId}", conversationId);
        
        // Save generation start to memory
        await SaveActionToMemoryAsync(conversationId, "StartProductGeneration", new
        {
            Language = language,
            Currency = currency,
            UserHints = userHints,
            ImageCount = base64Images?.Count() ?? 0
        }, cancellationToken);

        if (base64Images == null || !base64Images.Any())
        {
            _logger.LogWarning("GenerateProductAsync called with no images.");
            
            // Save error to memory
            await SaveActionToMemoryAsync(conversationId, "ErrorNoImages", new { }, cancellationToken);
            
            return null;
        }

        List<string> originalImageS3Keys = new List<string>(); // Store S3 keys for original images
        List<string> processedImageS3Keys = new List<string>(); // Store S3 keys for processed images
        List<string> processedImageCacheKeys = new List<string>(); // Cache keys pointing to processed S3 keys
        List<string> fallbackBase64Images = new List<string>(); // Store base64 images as fallback
        string runId = Guid.NewGuid().ToString("N"); // Unique ID for this run's S3 keys/cache keys

        try
        {
            var stepStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Decoding {Count} Base64 images and uploading originals to S3...", base64Images.Count());
            int imgIndex = 0;
            foreach (var b64 in base64Images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                imgIndex++;
                if (string.IsNullOrWhiteSpace(b64))
                {
                    _logger.LogWarning("Skipping empty base64 string at index {Index}.", imgIndex);
                    continue;
                }
                try
                {
                    var bytes = Convert.FromBase64String(b64);
                    using var stream = new MemoryStream(bytes);
                    // *** S3 Upload ***
                    string s3Key = $"raw/{runId}/image-{imgIndex}.png"; // Assuming PNG, adjust if needed
                    string? uploadedKey = await _s3StorageService.UploadFileAsync(_s3BucketName, s3Key, stream, "image/png", cancellationToken);

                    if (!string.IsNullOrEmpty(uploadedKey))
                    {
                        originalImageS3Keys.Add(uploadedKey);
                        s3KeysToCleanUp.Add(uploadedKey); // Add original key for potential cleanup
                        _logger.LogDebug("Uploaded original image {Index} to S3 key: {S3Key}", imgIndex, uploadedKey);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to upload original image {Index} to S3. Storing base64 as fallback.", imgIndex);
                        fallbackBase64Images.Add(b64); // Store base64 as fallback
                        // Cache the base64 image with a unique key
                        string base64CacheKey = $"base64-{runId}-{imgIndex}";
                        _memoryCache.Set(base64CacheKey, b64, TimeSpan.FromMinutes(30));
                        _logger.LogDebug("Cached base64 image with key: {CacheKey}", base64CacheKey);
                    }
                }
                catch (FormatException formatEx)
                {
                    _logger.LogWarning(formatEx, "Invalid Base64 string at index {Index}. Skipping.", imgIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error decoding or uploading original image {Index} to S3.", imgIndex);
                    // Store base64 as fallback in case of other errors
                    fallbackBase64Images.Add(b64);
                    // Cache the base64 image with a unique key
                    string base64CacheKey = $"base64-{runId}-{imgIndex}";
                    _memoryCache.Set(base64CacheKey, b64, TimeSpan.FromMinutes(30));
                    _logger.LogDebug("Cached base64 image with key: {CacheKey}", base64CacheKey);
                }
            }

            if (!originalImageS3Keys.Any() && !fallbackBase64Images.Any())
            {
                _logger.LogError("No valid images could be decoded and uploaded to S3, and no fallback base64 images available. Aborting generation.");
                return null;
            }

            // If we have some S3 keys but also fallback images, log a warning
            if (fallbackBase64Images.Any())
            {
                _logger.LogWarning("Using {S3Count} S3 images and {FallbackCount} fallback base64 images for processing.", 
                    originalImageS3Keys.Count, fallbackBase64Images.Count);
            }
            else
            {
                _logger.LogInformation("Uploaded {Count} original images to S3.", originalImageS3Keys.Count);
            }
            stepStopwatch.Stop();
            timingsReport.Add(new KeyValuePair<string, long>("Base64 Decoding & S3 Upload", stepStopwatch.ElapsedMilliseconds));
            _logger.LogInformation("Base64 Decoding & S3 Upload completed in {ElapsedMilliseconds} ms.", stepStopwatch.ElapsedMilliseconds);
            stepStopwatch.Restart();

            // Step 1: Photo Processing (Now takes S3 keys, returns S3 keys)
            _logger.LogInformation("[1] Running PhotoProcessing for {S3Count} S3 images and {FallbackCount} fallback images...", 
                originalImageS3Keys.Count, fallbackBase64Images.Count);
            
            // Process S3 images if we have any
            if (originalImageS3Keys.Any())
            {
                var processingInput = new PhotoProcessingInputS3(
                    InputS3Keys: originalImageS3Keys,
                    RunId: runId,
                    RotationAngle: 0,
                    BackgroundColor: "Transparent",
                    Width: 1280,
                    Height: 960,
                    WatermarkText: "©Tsintra",
                    WatermarkFont: "Arial"
                );

                (List<string> returnedProcessedS3Keys, long photoProcessingMs) = await RunToolAndGetResultAsync<PhotoProcessingInputS3, List<string>>(processingInput, conversationId, cancellationToken);
                timingsReport.Add(new KeyValuePair<string, long>("Tool: PhotoProcessing (S3)", photoProcessingMs));

                if (returnedProcessedS3Keys != null && returnedProcessedS3Keys.Any())
                {
                    processedImageS3Keys.AddRange(returnedProcessedS3Keys);
                    s3KeysToCleanUp.AddRange(returnedProcessedS3Keys);
                    _logger.LogInformation("[1] Out: Received {Count} processed image S3 keys.", returnedProcessedS3Keys.Count);
                }
            }

            // Process fallback base64 images if we have any
            if (fallbackBase64Images.Any())
            {
                _logger.LogInformation("[1] Processing {Count} fallback base64 images...", fallbackBase64Images.Count);
                foreach (var b64 in fallbackBase64Images)
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(b64);
                        using var stream = new MemoryStream(bytes);
                        string s3Key = $"processed/{runId}/fallback-{Guid.NewGuid():N}.png";
                        string? uploadedKey = await _s3StorageService.UploadFileAsync(_s3BucketName, s3Key, stream, "image/png", cancellationToken);

                        if (!string.IsNullOrEmpty(uploadedKey))
                        {
                            processedImageS3Keys.Add(uploadedKey);
                            s3KeysToCleanUp.Add(uploadedKey);
                            _logger.LogDebug("Processed and uploaded fallback image to S3 key: {S3Key}", uploadedKey);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to upload processed fallback image to S3. Using base64 directly.");
                            // Cache the processed base64 image with a unique key
                            string processedBase64CacheKey = $"processed-base64-{runId}-{Guid.NewGuid():N}";
                            _memoryCache.Set(processedBase64CacheKey, b64, TimeSpan.FromMinutes(30));
                            processedImageCacheKeys.Add(processedBase64CacheKey);
                            _logger.LogDebug("Cached processed base64 image with key: {CacheKey}", processedBase64CacheKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing fallback base64 image.");
                    }
                }
            }

            if (!processedImageS3Keys.Any() && !processedImageCacheKeys.Any())
            {
                _logger.LogWarning("No images were successfully processed. Aborting generation.");
                return null;
            }

            // Cache processed S3 keys using cache keys
            stepStopwatch.Restart();
            _logger.LogInformation("Caching {Count} processed S3 keys...", processedImageS3Keys.Count);
            imgIndex = 0; // Reset index for cache key generation
            foreach (var processedS3Key in processedImageS3Keys)
            {
                imgIndex++;
                cancellationToken.ThrowIfCancellationRequested();
                // Generate cache key similar to before, but value is now S3 key
                string cacheKey = $"run-{runId}-processed-{imgIndex}";
                _memoryCache.Set(cacheKey, processedS3Key, TimeSpan.FromMinutes(30)); // Cache S3 key (longer TTL?)
                processedImageCacheKeys.Add(cacheKey); // Store the key for subsequent tools
                _logger.LogDebug("Cached processed S3 key '{S3Key}' with cache key: {CacheKey}", processedS3Key, cacheKey);
            }
            _logger.LogInformation("Caching complete.");
            stepStopwatch.Stop();
            timingsReport.Add(new KeyValuePair<string, long>("S3 Key Caching", stepStopwatch.ElapsedMilliseconds));
            _logger.LogInformation("S3 Key Caching completed in {ElapsedMilliseconds} ms.", stepStopwatch.ElapsedMilliseconds);

            // --- Subsequent steps now use processedImageCacheKeys ---
            // These cache keys will resolve to S3 keys inside the tools

            // Step 2 & 3: Vision Pipeline and Web Image Search (Concurrent)
             _logger.LogInformation("[2&3] Starting BestVisionPipeline and WebImageSearch concurrently using cache keys pointing to S3...");
             stepStopwatch.Restart(); // Time the concurrent block start

            // Створюємо власний CTS із більшим таймаутом
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            
            var visionInput = new VisionPipelineInput(processedImageCacheKeys, language, userHints);
            Task<JsonElement> visionTask = null;
            JsonElement visionResult = JsonDocument.Parse("{}").RootElement;
            
            var webSearchInput = new ReverseImageSearchInput(processedImageCacheKeys);
            Task<JsonElement> webSearchTask = null;
            JsonElement webSearchResult = JsonDocument.Parse("{}").RootElement;
            
            try
            {
                // Запускаємо обидва завдання, але обробляємо помилки окремо
                visionTask = RunToolAndParseJsonAsync(visionInput, conversationId, linkedCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting BestVisionPipeline tool");
                await SaveActionToMemoryAsync(conversationId, "ErrorVisionPipeline", new { ErrorMessage = ex.Message }, cancellationToken);
            }
            
            try
            {
                webSearchTask = RunToolAndParseJsonAsync(webSearchInput, conversationId, linkedCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting WebImageSearch tool");
                await SaveActionToMemoryAsync(conversationId, "ErrorWebImageSearch", new { ErrorMessage = ex.Message }, cancellationToken);
            }
            
            // Очікуємо завершення обох завдань, якщо вони були успішно запущені
            if (visionTask != null)
            {
                try
                {
                    visionResult = await visionTask;
                    _logger.LogInformation("BestVisionPipeline completed successfully");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("BestVisionPipeline operation was cancelled, but we will continue with product generation");
                    await SaveActionToMemoryAsync(conversationId, "CanceledVisionPipeline", new { }, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing BestVisionPipeline tool");
                    await SaveActionToMemoryAsync(conversationId, "ErrorVisionPipeline", new { ErrorMessage = ex.Message }, cancellationToken);
                }
            }
            
            if (webSearchTask != null)
            {
                try
                {
                    webSearchResult = await webSearchTask;
                    _logger.LogInformation("WebImageSearch completed successfully");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("WebImageSearch operation was cancelled, but we will continue with product generation");
                    await SaveActionToMemoryAsync(conversationId, "CanceledWebImageSearch", new { }, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing WebImageSearch tool");
                    await SaveActionToMemoryAsync(conversationId, "ErrorWebImageSearch", new { ErrorMessage = ex.Message }, cancellationToken);
                }
            }
            
            stepStopwatch.Stop(); // Time the concurrent block end

            // Використовуємо результати, навіть якщо один з інструментів не завершився успішно
            long visionWebSearchConcurrentMs = stepStopwatch.ElapsedMilliseconds;
            timingsReport.Add(new KeyValuePair<string, long>("Wait: Vision & WebSearch Concurrency", visionWebSearchConcurrentMs));
            _logger.LogInformation("[2&3] Vision & WebSearch concurrent execution awaited in {ElapsedMilliseconds} ms.", visionWebSearchConcurrentMs);

            // Step 3.5: Parse Web Search Results & Extract URLs (Keep timing for this non-tool step)
            stepStopwatch.Restart();
            _logger.LogInformation("Parsing web search results and selecting URLs...");
            List<string> urlsToScrape = new List<string>();
            int maxUrlsToScrape = 5; // Configurable?
            if (webSearchResult.TryGetProperty("PagesWithMatchingImages", out var pages) && pages.ValueKind == JsonValueKind.Array)
            {
                urlsToScrape = pages.EnumerateArray()
                    .Where(p => p.TryGetProperty("Url", out var urlElem) && urlElem.ValueKind == JsonValueKind.String)
                    .Select(p => p.GetProperty("Url").GetString()!)
                    .Where(url => !string.IsNullOrEmpty(url))
                    .Take(maxUrlsToScrape)
                    .ToList();
            }
             _logger.LogInformation("Selected top {Count} URLs to scrape.", urlsToScrape.Count);
            stepStopwatch.Stop();
             timingsReport.Add(new KeyValuePair<string, long>("Result Parsing & URL Extraction", stepStopwatch.ElapsedMilliseconds));
             _logger.LogInformation("Result Parsing & URL Extraction completed in {ElapsedMilliseconds} ms.", stepStopwatch.ElapsedMilliseconds);


            // Step 4: Web Scraper (Conditional)
            stepStopwatch.Restart();
            string webScraperContent = string.Empty;
            long webScraperMs = 0; 
            if (urlsToScrape.Any())
            {
                _logger.LogInformation("[4] Running WebScraper for {Count} URLs...", urlsToScrape.Count);
                // Відновлюємо виклик WebScraper
                var scraperInput = new WebScraperInput(urlsToScrape); 
                (string scrapeResult, webScraperMs) = await RunToolAndGetStringAsync(scraperInput, conversationId, cancellationToken);
                webScraperContent = scrapeResult;
                
                timingsReport.Add(new KeyValuePair<string, long>("Tool: WebScraper", webScraperMs));
                _logger.LogInformation("[4] WebScraper finished. Result length: {Length} characters", webScraperContent?.Length ?? 0);
            }
            else
            {
                _logger.LogInformation("[4] Skipping WebScraper as no URLs were found.");
            }
            stepStopwatch.Stop(); 
            _logger.LogInformation("[4] Web Scraping step completed in {ElapsedMilliseconds} ms.", stepStopwatch.ElapsedMilliseconds);


            // Step 5: Deep Market Analysis
            _logger.LogInformation("[5] Running DeepMarketAnalysis...");
            // Передаємо всі доступні дані для повного аналізу ринку
            var marketAnalysisInput = new MarketAnalysisInput
            { 
                Title = visionResult.TryGetProperty("productName", out var pn) ? pn.GetString() ?? "" : "", 
                SceneDescription = visionResult.TryGetProperty("sceneDescription", out var sd) ? sd.GetString() ?? "" : "", 
                Keywords = visionResult.TryGetProperty("keyFeatures", out var kf) && kf.ValueKind == JsonValueKind.Array ? kf.EnumerateArray().Select(f => f.GetString() ?? "").ToList() : new List<string>(),
                ScrapedWebDataJson = webScraperContent, // Детальний результат скрапінгу веб-сторінок
                Language = language,
                TargetCurrency = currency,
                UserHints = userHints // Додаємо підказки користувача для кращого аналізу
            };
            _logger.LogInformation("[5] Передаємо в аналіз ринку {WebContentLength} символів веб-даних", webScraperContent?.Length ?? 0);

            (string marketAnalysisJson, long marketAnalysisMs) = await RunToolAndGetStringAsync(marketAnalysisInput, conversationId, cancellationToken);
            JsonElement marketAnalysisResult = JsonDocument.Parse(marketAnalysisJson).RootElement.Clone();
            timingsReport.Add(new KeyValuePair<string, long>("Tool: DeepMarketAnalysis", marketAnalysisMs));
            _logger.LogInformation("[5] DeepMarketAnalysis finished. Аналіз отримав інформацію про товари конкурентів та їхні характеристики.");
            stepStopwatch.Stop(); 
            _logger.LogInformation("[5] DeepMarketAnalysis completed in {ElapsedMilliseconds} ms.", stepStopwatch.ElapsedMilliseconds);


            // Step 6 & 7: Audience Definition & Content Refinement (Concurrent)
             _logger.LogInformation("[6&7] Starting AudienceDefinition and RefineContent concurrently...");
             stepStopwatch.Restart();

            /* Placeholder for AudienceDefinition call */
            Task<(string Result, long ElapsedMilliseconds)> audienceTask = Task.FromResult(("{}", 0L)); 
            JsonElement audienceResult = JsonDocument.Parse("{}").RootElement.Clone(); 
            long audienceMs = 0; 
            /* End Placeholder */
            
            // *** Using correct constructor/properties for RefineContentInput record ***
            var refineInput = new RefineContentInput(
                 OriginalTitle: visionResult.TryGetProperty("productName", out pn) ? pn.GetString() ?? "" : "", 
                 MarketAnalysisJson: marketAnalysisResult.ToString(), 
                 ImageCacheKeys: processedImageCacheKeys, // Pass cache keys pointing to S3 keys
                 TargetCurrency: currency
             );
             var refineTask = RunToolAndGetStringAsync(refineInput, conversationId, cancellationToken);

            await Task.WhenAll(audienceTask, refineTask);
            stepStopwatch.Stop(); 

            long audienceRefineConcurrentMs = stepStopwatch.ElapsedMilliseconds;
            timingsReport.Add(new KeyValuePair<string, long>("Wait: Audience & Refine Concurrency", audienceRefineConcurrentMs));
            _logger.LogInformation("[6&7] Audience & RefineContent concurrent execution awaited in {ElapsedMilliseconds} ms.", audienceRefineConcurrentMs);

            (string audienceJson, audienceMs) = await audienceTask;
            (string refinedContentJson, long refineMs) = await refineTask;
            audienceResult = JsonDocument.Parse(audienceJson).RootElement.Clone();
            JsonElement refinedContentResult = JsonDocument.Parse(refinedContentJson).RootElement.Clone();
            timingsReport.Add(new KeyValuePair<string, long>("Tool: AudienceDefinition", audienceMs));
            timingsReport.Add(new KeyValuePair<string, long>("Tool: RefineContent", refineMs));

            // Step 8: Instagram Caption Generation (Commented out)
            _logger.LogInformation("[8] Running InstagramCaption...");
            string instagramCaption = ""; // *** Define placeholder variable ***
            long instagramCaptionMs = 0; // Placeholder
            /* Commented out block 
             var captionInput = ... 
             (instagramCaption, instagramCaptionMs) = ... 
            */
            timingsReport.Add(new KeyValuePair<string, long>("Tool: InstagramCaption", instagramCaptionMs));
            _logger.LogInformation("[8] InstagramCaption finished.");
            stepStopwatch.Stop(); 
            _logger.LogInformation("[8] InstagramCaption completed in {ElapsedMilliseconds} ms.", stepStopwatch.ElapsedMilliseconds);

            // Step 9: Combine results into ProductDetailsDto
            stepStopwatch.Restart();
            _logger.LogInformation("Combining results into ProductDetailsDto for Prom.ua marketplace...");

            // Extract data safely from JSON Elements obtained from tools
            string productName = refinedContentResult.TryGetProperty("refinedProductName", out var rpn) ? rpn.GetString() ?? "Generated Product" : "Generated Product";
            string description = refinedContentResult.TryGetProperty("refinedDescription", out var rd) ? rd.GetString() ?? "No description generated." : "No description generated.";
            List<string> keywords = refinedContentResult.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array
                                    ? kw.EnumerateArray().Select(k => k.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                                    : new List<string>();
            
            // Витягуємо ціну та інші атрибути з результатів аналізу ринку
            decimal price = refinedContentResult.TryGetProperty("recommendedPrice", out var priceElem) && priceElem.ValueKind == JsonValueKind.Number 
                            ? priceElem.GetDecimal() 
                            : 0; // Default price if not found

            // Додатково спробуємо витягнути ціну з аналізу ринку, якщо вона не визначена
            if (price == 0 && marketAnalysisResult.TryGetProperty("averagePrice", out var avgPriceElem))
            {
                if (avgPriceElem.ValueKind == JsonValueKind.Number)
                {
                    price = avgPriceElem.GetDecimal();
                }
                else if (avgPriceElem.ValueKind == JsonValueKind.String && decimal.TryParse(avgPriceElem.GetString(), out var parsedPrice))
                {
                    price = parsedPrice;
                }
            }

            // Визначаємо одиницю виміру
            string measureUnit = "шт."; // За замовчуванням
            if (marketAnalysisResult.TryGetProperty("recommendedMeasureUnit", out var unitElem) && 
                unitElem.ValueKind == JsonValueKind.String)
            {
                measureUnit = unitElem.GetString() ?? "шт.";
            }

            // Визначаємо наявність
            string availability = "в наявності";
            if (marketAnalysisResult.TryGetProperty("recommendedAvailability", out var availElem) && 
                availElem.ValueKind == JsonValueKind.String)
            {
                availability = availElem.GetString() ?? "в наявності";
            }

            // Габаритні розміри товару
            int? width = null, height = null, length = null;
            decimal? weight = null;

            if (marketAnalysisResult.TryGetProperty("dimensions", out var dimElem) && dimElem.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    // Ширина
                    if (dimElem.TryGetProperty("width", out var widthElem)) 
                    {
                        if (widthElem.ValueKind == JsonValueKind.Number)
                        {
                            try 
                            {
                                width = widthElem.GetInt32();
                            }
                            catch (FormatException)
                            {
                                // Якщо число з плаваючою комою, округлюємо до цілого
                                if (widthElem.TryGetDecimal(out var widthDecimal))
                                {
                                    width = (int)Math.Round(widthDecimal);
                                }
                            }
                        }
                        else if (widthElem.ValueKind == JsonValueKind.String)
                        {
                            // Якщо рядок, пробуємо конвертувати
                            string? widthStr = widthElem.GetString();
                            if (!string.IsNullOrEmpty(widthStr))
                            {
                                if (int.TryParse(widthStr, out int parsedWidth))
                                {
                                    width = parsedWidth;
                                }
                                else if (decimal.TryParse(widthStr, 
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out decimal parsedWidthDecimal))
                                {
                                    width = (int)Math.Round(parsedWidthDecimal);
                                }
                            }
                        }
                    }
                    
                    // Висота
                    if (dimElem.TryGetProperty("height", out var heightElem)) 
                    {
                        if (heightElem.ValueKind == JsonValueKind.Number)
                        {
                            try 
                            {
                                height = heightElem.GetInt32();
                            }
                            catch (FormatException)
                            {
                                // Якщо число з плаваючою комою, округлюємо до цілого
                                if (heightElem.TryGetDecimal(out var heightDecimal))
                                {
                                    height = (int)Math.Round(heightDecimal);
                                }
                            }
                        }
                        else if (heightElem.ValueKind == JsonValueKind.String)
                        {
                            // Якщо рядок, пробуємо конвертувати
                            string? heightStr = heightElem.GetString();
                            if (!string.IsNullOrEmpty(heightStr))
                            {
                                if (int.TryParse(heightStr, out int parsedHeight))
                                {
                                    height = parsedHeight;
                                }
                                else if (decimal.TryParse(heightStr, 
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out decimal parsedHeightDecimal))
                                {
                                    height = (int)Math.Round(parsedHeightDecimal);
                                }
                            }
                        }
                    }
                    
                    // Довжина
                    if (dimElem.TryGetProperty("length", out var lengthElem)) 
                    {
                        if (lengthElem.ValueKind == JsonValueKind.Number)
                        {
                            try 
                            {
                                length = lengthElem.GetInt32();
                            }
                            catch (FormatException)
                            {
                                // Якщо число з плаваючою комою, округлюємо до цілого
                                if (lengthElem.TryGetDecimal(out var lengthDecimal))
                                {
                                    length = (int)Math.Round(lengthDecimal);
                                }
                            }
                        }
                        else if (lengthElem.ValueKind == JsonValueKind.String)
                        {
                            // Якщо рядок, пробуємо конвертувати
                            string? lengthStr = lengthElem.GetString();
                            if (!string.IsNullOrEmpty(lengthStr))
                            {
                                if (int.TryParse(lengthStr, out int parsedLength))
                                {
                                    length = parsedLength;
                                }
                                else if (decimal.TryParse(lengthStr, 
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out decimal parsedLengthDecimal))
                                {
                                    length = (int)Math.Round(parsedLengthDecimal);
                                }
                            }
                        }
                    }
                    
                    // Вага
                    if (dimElem.TryGetProperty("weight", out var weightElem)) 
                    {
                        if (weightElem.ValueKind == JsonValueKind.Number)
                        {
                            try 
                            {
                                weight = weightElem.GetDecimal();
                            }
                            catch (FormatException)
                            {
                                // Якщо число з плаваючою комою в іншому форматі
                                if (weightElem.TryGetDouble(out double weightDouble))
                                {
                                    weight = (decimal)weightDouble;
                                }
                            }
                        }
                        else if (weightElem.ValueKind == JsonValueKind.String)
                        {
                            // Якщо рядок, пробуємо конвертувати
                            string? weightStr = weightElem.GetString();
                            if (!string.IsNullOrEmpty(weightStr))
                            {
                                if (decimal.TryParse(weightStr, 
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out decimal parsedWeight))
                                {
                                    weight = parsedWeight;
                                }
                            }
                        }
                    }
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning("Помилка при обробці розмірів товару з аналізу ринку. Розміри будуть пропущені. {ExMessage}", ex.Message);
                }
            }

            // Підготовка атрибутів товару на основі специфікацій з конкурентів
            var attributes = new Dictionary<string, string>();
            
            // Спробуємо отримати популярні характеристики з аналізу ринку
            if (marketAnalysisResult.TryGetProperty("popularFeatures", out var featuresElem) && 
                featuresElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in featuresElem.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        attributes[property.Name] = property.Value.GetString() ?? "";
                    }
                }
            }
            
            // Спробуємо отримати категорію
            string category = "";
            if (marketAnalysisResult.TryGetProperty("recommendedCategory", out var categoryElem) && 
                categoryElem.ValueKind == JsonValueKind.String)
            {
                category = categoryElem.GetString() ?? "";
            }

            // Мінімальна кількість для замовлення
            int minimumOrderQuantity = 1;
            if (marketAnalysisResult.TryGetProperty("minimumOrderQuantity", out var minOrderElem) && 
                minOrderElem.ValueKind == JsonValueKind.Number)
            {
                minimumOrderQuantity = minOrderElem.GetInt32();
            }

            // Генерація багатомовних полів (українська та російська)
            var nameMultilang = new MultiLanguageText();
            var descriptionMultilang = new MultiLanguageText();
            var keywordsMultilang = new MultiLanguageKeywords();

            // Перевіряємо, чи доступні переклади у відповіді, якщо ні - здійснюємо переклад
            if (refinedContentResult.TryGetProperty("nameUk", out var nameUkElement)) {
                nameMultilang.Uk = nameUkElement.GetString() ?? productName;
            } else {
                nameMultilang.Uk = productName; // Припускаємо, що основна мова українська
            }

            if (refinedContentResult.TryGetProperty("nameRu", out var nameRuElement)) {
                nameMultilang.Ru = nameRuElement.GetString() ?? TranslateToRussian(productName);
            } else {
                nameMultilang.Ru = TranslateToRussian(productName);
            }

            if (refinedContentResult.TryGetProperty("descriptionUk", out var descUkElement)) {
                descriptionMultilang.Uk = descUkElement.GetString() ?? description;
            } else {
                descriptionMultilang.Uk = description; // Припускаємо, що основна мова українська
            }

            if (refinedContentResult.TryGetProperty("descriptionRu", out var descRuElement)) {
                descriptionMultilang.Ru = descRuElement.GetString() ?? TranslateToRussian(description);
            } else {
                descriptionMultilang.Ru = TranslateToRussian(description);
            }

            if (refinedContentResult.TryGetProperty("keywordsUk", out var keywordsUkElement) && keywordsUkElement.ValueKind == JsonValueKind.Array) {
                keywordsMultilang.Uk = keywordsUkElement.EnumerateArray()
                    .Select(k => k.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            } else {
                keywordsMultilang.Uk = keywords;
            }

            if (refinedContentResult.TryGetProperty("keywordsRu", out var keywordsRuElement) && keywordsRuElement.ValueKind == JsonValueKind.Array) {
                keywordsMultilang.Ru = keywordsRuElement.EnumerateArray()
                    .Select(k => k.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            } else {
                keywordsMultilang.Ru = keywords.Select(TranslateToRussian).ToList();
            }

            // Генерація SEO-оптимізованих полів
            string metaTitle = refinedContentResult.TryGetProperty("metaTitle", out var metaTitleElement) 
                ? metaTitleElement.GetString() ?? productName 
                : productName;

            string metaDescription = refinedContentResult.TryGetProperty("metaDescription", out var metaDescElement) 
                ? metaDescElement.GetString() ?? (description.Length > 160 ? description.Substring(0, 157) + "..." : description)
                : (description.Length > 160 ? description.Substring(0, 157) + "..." : description);

            string seoUrl = GenerateSeoUrl(productName);

            // Пошукові запити (теги) як рядок з комами для Prom.ua
            string tagsString = string.Join(", ", keywords);
            Dictionary<string, string> tagsMultilang = new Dictionary<string, string>
            {
                { "uk", string.Join(", ", keywordsMultilang.Uk) },
                { "ru", string.Join(", ", keywordsMultilang.Ru) }
            };

            var productDetails = new MarketplaceProductDetailsDto
            {
                RefinedTitle = productName, 
                RefinedDescription = description,
                Keywords = keywords,
                Currency = currency,
                Images = processedImageS3Keys,
                RecommendedPrice = price,
                InstagramCaption = instagramCaption,
                
                // Set standard ProductDetailsDto properties too
                Description = description,
                Price = price,
                Tags = keywords,
                Attributes = attributes,
                Category = category,
                
                // Багатомовні поля
                NameMultilang = nameMultilang,
                DescriptionMultilang = descriptionMultilang,
                KeywordsMultilang = keywordsMultilang,
                
                // SEO-оптимізація
                MetaTitle = metaTitle,
                MetaDescription = metaDescription,
                SeoUrl = seoUrl,
                
                // Додаткові поля для форми Prom.ua
                TagsString = tagsString,
                TagsMultilang = tagsMultilang,
                MeasureUnit = measureUnit,
                Availability = availability,
                MinimumOrderQuantity = minimumOrderQuantity,
                
                // Габаритні розміри
                Width = width,
                Height = height,
                Length = length,
                Weight = weight
            };

             _logger.LogInformation("Using S3 keys for images in final ProductDetailsDto: {S3Keys}", string.Join(", ", productDetails.Images ?? new List<string>()));
             _logger.LogInformation("Successfully generated ProductDetailsDto for '{ProductName}'.", productDetails.RefinedTitle); // Use RefinedTitle for logging
            stepStopwatch.Stop();
            timingsReport.Add(new KeyValuePair<string, long>("Final DTO Combination", stepStopwatch.ElapsedMilliseconds));
             _logger.LogInformation("Final DTO Combination completed in {ElapsedMilliseconds} ms.", stepStopwatch.ElapsedMilliseconds);

             _logger.LogInformation("Product generation finished successfully.");
             totalStopwatch.Stop();
             LogPerformanceReport(timingsReport, totalStopwatch.ElapsedMilliseconds); 
             await SaveActionToMemoryAsync(conversationId, "CompleteProductGeneration", new
             {
                 Success = true,
                 TotalTimeMs = totalStopwatch.ElapsedMilliseconds,
                 ProductName = productDetails.RefinedTitle,
                 ImagesCount = productDetails.Images?.Count ?? 0,
                 TimingsReport = timingsReport
             }, cancellationToken);
             return productDetails;

        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Product generation was cancelled.");
            totalStopwatch.Stop();
            LogPerformanceReport(timingsReport, totalStopwatch.ElapsedMilliseconds); // Log partial report
            // Consider cleanup even if cancelled
            await SaveActionToMemoryAsync(conversationId, "ErrorProductGenerationCancelled", new { }, cancellationToken);
            return null;
        }
        catch (ToolExecutionException toolEx)
        {
             // Logged inside RunToolAsync helpers
             _logger.LogError(toolEx, "Product generation failed due to an error in tool: {ToolName}", toolEx.ToolName);
             totalStopwatch.Stop();
             LogPerformanceReport(timingsReport, totalStopwatch.ElapsedMilliseconds); // Log partial report
             await SaveActionToMemoryAsync(conversationId, "ErrorProductGenerationTool", new { ToolName = toolEx.ToolName }, cancellationToken);
             return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during product generation.");
            totalStopwatch.Stop();
             LogPerformanceReport(timingsReport, totalStopwatch.ElapsedMilliseconds); // Log partial report
             await SaveActionToMemoryAsync(conversationId, "ErrorProductGenerationUnexpected", new { }, cancellationToken);
             return null;
        }
        finally
        {
            // Cleanup cached S3 keys regardless of success/failure
            // Cache cleanup is usually handled by expiry, but explicit removal can be done
             _logger.LogInformation("Cleaning up {Count} processed image cache entries...", processedImageCacheKeys.Count);
             foreach (var cacheKey in processedImageCacheKeys)
             {
                 _memoryCache.Remove(cacheKey);
             }
             _logger.LogInformation("Cache cleanup complete.");

             // ** Optional: S3 Cleanup **
             // Decide if S3 objects should be deleted after processing.
             // If yes, uncomment and implement deletion logic.
             // _logger.LogInformation("Cleaning up {Count} S3 objects...", s3KeysToCleanUp.Count);
             // foreach (var s3Key in s3KeysToCleanUp)
             // {
             //     try { await _s3StorageService.DeleteFileAsync(_s3BucketName, s3Key); } // Assuming DeleteFileAsync exists
             //     catch(Exception ex) { _logger.LogWarning(ex, "Failed to delete S3 object {S3Key}", s3Key); }
             // }
             // _logger.LogInformation("S3 object cleanup complete.");

             _logger.LogInformation("Product generation finished (possibly with errors) in {ElapsedMilliseconds} ms.", totalStopwatch.ElapsedMilliseconds);
        }
    }

    // Helper method to log the performance report
    private void LogPerformanceReport(List<KeyValuePair<string, long>> timings, long totalMilliseconds)
    {
        var reportBuilder = new StringBuilder();
        reportBuilder.AppendLine("--- Performance Report ---");
        if (timings != null && timings.Any())
        {
            foreach (var timing in timings)
            {
                reportBuilder.AppendLine($"  - {timing.Key}: {timing.Value} ms");
            }
        } else {
            reportBuilder.AppendLine("  (No timing data collected)");
        }
        reportBuilder.AppendLine("-------------------------");
        reportBuilder.AppendLine($"Total Generation Time: {totalMilliseconds} ms");
        reportBuilder.AppendLine("-------------------------");
        _logger.LogInformation(reportBuilder.ToString());
    }

    // Implementation of IProductGenerationTools.PublishProductAsync
    public async Task<PublishResultDto> PublishProductAsync(
        ProductDetailsDto productDetails,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        // Convert ProductDetailsDto to MarketplaceProductDetailsDto
        var marketplaceProduct = MarketplaceProductDetailsDto.FromProductDetailsDto(productDetails);
        
        // Call the original method with MarketplaceProductDetailsDto
        var result = await PublishMarketplaceProductAsync(marketplaceProduct, conversationId, cancellationToken);
        
        // Convert to Domain.DTOs.PublishResultDto
        return new PublishResultDto
        {
            Success = result.Success,
            Message = result.Message,
            PublishedItemId = result.MarketplaceProductId
        };
    }
    
    // Original method renamed to avoid overload conflicts
    public async Task<PublishResponse> PublishMarketplaceProductAsync(
        MarketplaceProductDetailsDto productToPublish, 
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        // Generate a conversation ID if not provided
        if (string.IsNullOrEmpty(conversationId))
        {
            conversationId = $"product-publish-{Guid.NewGuid():N}";
        }
        
        _logger.LogInformation("Attempting to publish product '{ProductName}'. Conversation ID: {ConversationId}", 
            productToPublish?.RefinedTitle ?? "N/A", conversationId);
            
        // Save publish start to memory
        await SaveActionToMemoryAsync(conversationId, "StartProductPublish", new
        {
            ProductName = productToPublish?.RefinedTitle,
            ImagesCount = productToPublish?.Images?.Count ?? 0
        }, cancellationToken);

        if (productToPublish == null)
        {
            // Save error to memory
            await SaveActionToMemoryAsync(conversationId, "ErrorNullProduct", new { }, cancellationToken);
            
            return new PublishResponse { Success = false, Message = "Product details are missing." };
        }

        var marketplaceClient = _serviceProvider.GetService<IMarketplaceClient>(); // Should now resolve correctly
        if (marketplaceClient == null)
        {
             _logger.LogError("Marketplace client (IMarketplaceClient) not found in DI container.");
             
             // Save error to memory
             await SaveActionToMemoryAsync(conversationId, "ErrorNoMarketplaceClient", new { }, cancellationToken);
             
             return new PublishResponse { Success = false, Message = "Marketplace client not configured." };
        }

        try
        {
            // Convert our MarketplaceProductDetailsDto to the standard ProductDetailsDto required by IMarketplaceClient
            ProductDetailsDto standardDto = new ProductDetailsDto
            {
                RefinedTitle = productToPublish.RefinedTitle,
                Description = productToPublish.Description,
                Price = productToPublish.Price,
                Images = productToPublish.Images,
                Tags = productToPublish.Tags,
                Attributes = productToPublish.Attributes,
                Category = productToPublish.Category,
                // Конвертуємо MultiLanguageText у Dictionary для сумісності з API
                NameMultilang = productToPublish.ToNameMultilangDictionary(),
                DescriptionMultilang = productToPublish.ToDescriptionMultilangDictionary(),
                // Інші важливі поля
                MetaTitle = productToPublish.MetaTitle,
                MetaDescription = productToPublish.MetaDescription,
                SeoUrl = productToPublish.SeoUrl
            };

            var domainPublishResult = await marketplaceClient.PublishProductAsync(standardDto, cancellationToken);
            
            // Convert the Domain.DTOs.PublishResultDto to Core.DTOs.PublishResultDto
            var publishResult = new PublishResultDto 
            { 
                Success = domainPublishResult.Success,
                Message = domainPublishResult.Message,
                MarketplaceProductId = domainPublishResult.MarketplaceProductId,
                PublishedItemId = domainPublishResult.PublishedItemId
            };

            // Save result to memory
            await SaveActionToMemoryAsync(conversationId, publishResult.Success ? "ProductPublishSuccess" : "ProductPublishFailed", new
            {
                Success = publishResult.Success,
                Message = publishResult.Message,
                MarketplaceProductId = publishResult.MarketplaceProductId
            }, cancellationToken);

            if (publishResult.Success)
            {
                _logger.LogInformation("Successfully published product '{ProductName}'. Marketplace ID: {MarketplaceId}",
                    productToPublish.RefinedTitle, publishResult.MarketplaceProductId ?? "N/A"); 
            }
            else
            {
                 _logger.LogError("Failed to publish product '{ProductName}'. Reason: {Message}",
                    productToPublish.RefinedTitle, publishResult.Message); 
            }
            return new PublishResponse
            {
                Success = publishResult.Success,
                Message = publishResult.Message,
                MarketplaceProductId = publishResult.MarketplaceProductId
            };
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "An error occurred during product publishing for '{ProductName}'.", productToPublish.RefinedTitle);
             
             // Save error to memory
             await SaveActionToMemoryAsync(conversationId, "ErrorProductPublishException", new
             {
                 ExceptionType = ex.GetType().Name,
                 ExceptionMessage = ex.Message
             }, cancellationToken);
             
             return new PublishResponse { Success = false, Message = $"Exception during publishing: {ex.Message}" };
        }
    }

    // Simple custom response class to avoid namespace conflicts
    public class PublishResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? MarketplaceProductId { get; set; }
    }

    // Implement required methods from IProductGenerationTools
    public async Task<ProductDetailsDto?> GenerateProductAsync(
        IEnumerable<string> base64Images,
        string? userHints = null,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        // Call existing method with defaults for language and currency
        var result = await GenerateProductAsync(
            base64Images, 
            "ukr", // Default language 
            "UAH", // Default currency
            userHints,
            conversationId,
            cancellationToken);
            
        // Convert MarketplaceProductDetailsDto to ProductDetailsDto
        if (result != null)
        {
            return new ProductDetailsDto
            {
                RefinedTitle = result.RefinedTitle,
                Description = result.Description,
                Price = result.Price,
                Images = result.Images,
                Attributes = result.Attributes,
                Category = result.Category,
                Tags = result.Tags,
                // Використовуємо методи перетворення тут
                NameMultilang = result.ToNameMultilangDictionary(),
                DescriptionMultilang = result.ToDescriptionMultilangDictionary(),
                MetaTitle = result.MetaTitle,
                MetaDescription = result.MetaDescription,
                SeoUrl = result.SeoUrl
            };
        }
        
        return null;
    }

    /// <summary>
    /// Простий метод для транслітерації на російську
    /// </summary>
    private string TranslateToRussian(string ukrainianText)
    {
        // Це спрощений переклад. У реальному додатку варто використати NLP Translation Service
        var ukrainianToRussian = new Dictionary<string, string>
        {
            {"і", "и"}, {"є", "е"}, {"ї", "и"}, {"и", "ы"}, {"г", "г"}, {"ґ", "г"},
            {"І", "И"}, {"Є", "Е"}, {"Ї", "И"}, {"И", "Ы"}, {"Г", "Г"}, {"Ґ", "Г"}
        };

        // Спрощений переклад - заміна українських символів на російські
        foreach (var kvp in ukrainianToRussian)
        {
            ukrainianText = ukrainianText.Replace(kvp.Key, kvp.Value);
        }

        return ukrainianText;
    }

    /// <summary>
    /// Генерує SEO-URL з назви товару
    /// </summary>
    private string GenerateSeoUrl(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        // Словник транслітерації українських символів
        var ukrainianToLatin = new Dictionary<char, string>
        {
            {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "h"}, {'ґ', "g"}, {'д', "d"},
            {'е', "e"}, {'є', "ie"}, {'ж', "zh"}, {'з', "z"}, {'и', "y"}, {'і', "i"},
            {'ї', "i"}, {'й', "i"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"},
            {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"},
            {'ф', "f"}, {'х', "kh"}, {'ц', "ts"}, {'ч', "ch"}, {'ш', "sh"}, {'щ', "shch"},
            {'ь', ""}, {'ю', "iu"}, {'я', "ia"},
            {'А', "a"}, {'Б', "b"}, {'В', "v"}, {'Г', "h"}, {'Ґ', "g"}, {'Д', "d"},
            {'Е', "e"}, {'Є', "ie"}, {'Ж', "zh"}, {'З', "z"}, {'И', "y"}, {'І', "i"},
            {'Ї', "i"}, {'Й', "i"}, {'К', "k"}, {'Л', "l"}, {'М', "m"}, {'Н', "n"},
            {'О', "o"}, {'П', "p"}, {'Р', "r"}, {'С', "s"}, {'Т', "t"}, {'У', "u"},
            {'Ф', "f"}, {'Х', "kh"}, {'Ц', "ts"}, {'Ч', "ch"}, {'Ш', "sh"}, {'Щ', "shch"},
            {'Ь', ""}, {'Ю', "iu"}, {'Я', "ia"}
        };

        // Транслітерація
        var transliterated = new StringBuilder();
        foreach (char c in title.ToLower())
        {
            if (ukrainianToLatin.TryGetValue(c, out string? latinChar))
            {
                transliterated.Append(latinChar);
            }
            else if (c == ' ')
            {
                transliterated.Append('-');
            }
            else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')
            {
                transliterated.Append(c);
            }
            // інші символи ігноруємо
        }

        // Конвертуємо до рядка
        string seoUrl = transliterated.ToString();
        
        // Видаляємо подвійні дефіси
        while (seoUrl.Contains("--"))
        {
            seoUrl = seoUrl.Replace("--", "-");
        }
        
        // Обрізаємо дефіси на початку і в кінці
        return seoUrl.Trim('-');
    }

    private Dictionary<string, string> BuildNameMultilangFromRequest(ProductDetailsDto dto)
    {
        if (dto is MarketplaceProductDetailsDto mpDto && mpDto.NameMultilang != null)
        {
            return new Dictionary<string, string>
            {
                { "uk", mpDto.NameMultilang.Uk },
                { "ru", mpDto.NameMultilang.Ru }
            };
        }
        return new Dictionary<string, string>
        {
            { "uk", dto.RefinedTitle ?? "" },
            { "ru", TranslateToRussian(dto.RefinedTitle ?? "") }
        };
    }
    
    private Dictionary<string, string> BuildDescriptionMultilangFromRequest(ProductDetailsDto dto)
    {
        if (dto is MarketplaceProductDetailsDto mpDto && mpDto.DescriptionMultilang != null)
        {
            return new Dictionary<string, string>
            {
                { "uk", mpDto.DescriptionMultilang.Uk },
                { "ru", mpDto.DescriptionMultilang.Ru }
            };
        }
        return new Dictionary<string, string>
        {
            { "uk", dto.Description ?? "" },
            { "ru", TranslateToRussian(dto.Description ?? "") }
        };
    }
}

// Custom exception for tool errors
public class ToolExecutionException : Exception
{
    public string ToolName { get; }

    public ToolExecutionException(string toolName, string message, Exception? innerException = null)
        : base($"Error executing tool '{toolName}': {message}", innerException)
    {
        ToolName = toolName;
    }
}

// Define necessary input/output DTOs if they don't exist or need adjustment
// Example: Assumed new input DTO for PhotoCorrectionTool
public record PhotoProcessingInputS3(
    List<string> InputS3Keys,
    string RunId, // Added RunId for naming conventions
    double RotationAngle = 0,
    string? BackgroundColor = null,
    int? Width = null,
    int? Height = null,
    string? WatermarkText = null,
    string? WatermarkFont = null);

// Other DTOs like VisionPipelineInput, ReverseImageSearchInput, MarketAnalysisInput etc.
// should already exist in Tsintra.MarketplaceAgent.DTOs namespace and use CacheKeys
// (e.g., VisionPipelineInput(List<string> ImageCacheKeys, string Language, string? Hints))
// These do NOT need to change as they work with cache keys that now point to S3 keys.

