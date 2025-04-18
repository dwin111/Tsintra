using System.Text.Json;
using System.Threading.Tasks.Dataflow; // Для Dataflow
using Google.Cloud.Vision.V1;
// Corrected using statements
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory; // Added for IMemoryCache
// using Microsoft.Playwright; // Removed Playwright dependency
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Interfaces; // Playwright for browser automation
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System;
using Tsintra.Integrations.Interfaces; // *** ADDED for S3 ***
using Tsintra.Integrations; // *** ADDED for AwsOptions ***

namespace Tsintra.MarketplaceAgent.Tools.Core
{
    // Клас для результатів, як в оригіналі, для Distinct()
    public class ProductSearchResult : IEquatable<ProductSearchResult>
    {
        public string ResourceName { get; set; }
        public string DisplayName { get; set; }
        public float SimilarityScore { get; set; }
        public string ImageUri { get; set; } 

        public bool Equals(ProductSearchResult other)
        {
            if (other is null) return false;
            return ResourceName == other.ResourceName;
        }

        public override bool Equals(object obj) => Equals(obj as ProductSearchResult);
        public override int GetHashCode() => ResourceName?.GetHashCode() ?? 0;
    }

    // Updated to implement the generic ITool<ReverseImageSearchInput, string>
    public class ReverseImageSearchTool : ITool<ReverseImageSearchInput, string>
    {
        private readonly ILogger<ReverseImageSearchTool> _logger;
        // private readonly IBrowser? _browser; // Removed Playwright browser dependency
        private readonly Configuration.VisionProductSearchConfig _config;
        // Використовуємо клієнт, що впроваджується через DI
        private readonly ImageAnnotatorClient _visionClient; 
        private readonly IMemoryCache _memoryCache; // Added IMemoryCache
        private readonly IS3StorageService _s3StorageService; // *** ADDED ***
        private readonly string _s3BucketName; // *** ADDED ***

        // Update constructor to remove IBrowser and inject IMemoryCache
        public ReverseImageSearchTool(
            ILogger<ReverseImageSearchTool> logger,
            IOptions<Configuration.VisionProductSearchConfig> config,
            IMemoryCache memoryCache,
            IS3StorageService s3StorageService, // *** ADDED ***
            IOptions<AwsOptions> awsOptions) // *** ADDED ***
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _s3StorageService = s3StorageService ?? throw new ArgumentNullException(nameof(s3StorageService)); // *** ADDED ***
            var options = awsOptions?.Value ?? throw new ArgumentNullException(nameof(awsOptions));
            _s3BucketName = options.S3?.BucketName ?? throw new InvalidOperationException("S3 BucketName is not configured in AwsOptions."); // *** ADDED ***

            if (string.IsNullOrEmpty(_config.CredentialsPath))
            {
                _logger.LogWarning("[{ToolName}] Google Credentials Path is not configured. Tool might not work.", Name);
                // Не кидаємо виняток одразу, можливо, клієнт створений інакше (наприклад, через змінні оточення)
            }

            try
            {
                // Спробуємо ініціалізувати клієнт стандартним чином (SDK може сам підхопити credentials)
                 var builder = new ImageAnnotatorClientBuilder
                 {
                     CredentialsPath = _config.CredentialsPath // Передаємо шлях, якщо він є
                 };
                 _visionClient = builder.Build();
                 _logger.LogInformation("[{ToolName}] Google Vision Image Annotator client initialized. MemoryCache injected.", Name);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "[{ToolName}] Failed to initialize Google Vision Image Annotator client. Check credentials configuration.", Name);
                 throw new InvalidOperationException($"Failed to initialize Google Vision client for {Name}.", ex);
            }
        }
        
        private bool IsProductSearchConfigured() => 
            !string.IsNullOrEmpty(_config.ProjectId) && 
            !string.IsNullOrEmpty(_config.Location) && 
            !string.IsNullOrEmpty(_config.ProductSetId);

        public string Name => "WebImageSearch"; // Renamed slightly
        public string Description => "Performs a reverse image search using Google Cloud Vision API (Web Detection) to find image sources online.";

        // Updated RunAsync signature
        public async Task<string> RunAsync(ReverseImageSearchInput input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[{ToolName}] Performing web detection using S3 image referenced by cache key...", Name);

            if (input?.ImageCacheKeys == null || !input.ImageCacheKeys.Any())
            {
                _logger.LogWarning("[{ToolName}] No image cache keys provided.", Name);
                return JsonSerializer.Serialize(new { error = "No image cache keys provided." });
            }

            string imageCacheKey = input.ImageCacheKeys.First();
            Image image; // Google Vision Image object
            try
            {
                // *** Get S3 Key from Cache ***
                if (_memoryCache.TryGetValue(imageCacheKey, out string? s3Key) && !string.IsNullOrEmpty(s3Key))
                {
                    _logger.LogDebug("[{ToolName}] Cache hit for S3 key '{S3Key}' using cache key: {CacheKey}", Name, s3Key, imageCacheKey);

                    // *** Generate Pre-signed URL ***
                    string presignedUrl = await _s3StorageService.GetPresignedUrlAsync(_s3BucketName, s3Key, 600); // 10 min validity for Vision API?

                    if (!string.IsNullOrEmpty(presignedUrl))
                    {
                        // *** Use Image.FromUri ***
                        image = Image.FromUri(presignedUrl);
                        _logger.LogDebug("[{ToolName}] Created Google Vision Image from pre-signed URL for S3 key {S3Key}", Name, s3Key);
                    }
                    else
                    {
                        _logger.LogWarning("[{ToolName}] Failed to generate pre-signed URL for S3 key: {S3Key}", Name, s3Key);
                        return JsonSerializer.Serialize(new { error = $"Failed to generate S3 URL for key: {s3Key}" });
                    }
                }
                else
                {
                    _logger.LogError("[{ToolName}] Cache miss or empty S3 key found for cache key: {Key}. Cannot perform search.", Name, imageCacheKey);
                    return JsonSerializer.Serialize(new { error = $"S3 image key not found in cache for key: {imageCacheKey}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ToolName}] Failed to get S3 key from cache, generate URL, or create Google Vision Image for key: {Key}", Name, imageCacheKey);
                return JsonSerializer.Serialize(new { error = $"Failed load image reference from S3: {ex.Message}" });
            }

            try
            {
                _logger.LogDebug("[{ToolName}] Calling Google Vision API DetectWebInformationAsync...", Name);
                // Pass the Google Vision Image object
                WebDetection webDetection = await _visionClient.DetectWebInformationAsync(image);

                if (webDetection == null)
                {
                     _logger.LogWarning("[{ToolName}] Google Vision API returned null WebDetection.", Name);
                     return JsonSerializer.Serialize(new { results = new List<object>(), message = "Vision API returned no web detection data." });
                }

                _logger.LogInformation("[{ToolName}] Web detection finished. Found {PagesCount} pages with matching images.", 
                                     Name, webDetection.PagesWithMatchingImages?.Count ?? 0);

                // --- Prepare results --- 
                // Extract relevant information for the next steps (e.g., scraping)
                var webEntities = webDetection.WebEntities.Select(e => new { e.EntityId, e.Description, e.Score }).ToList();
                var pagesWithMatches = webDetection.PagesWithMatchingImages.Select(p => new { p.Url, p.PageTitle, Score = p.Score  }).OrderByDescending(p => p.Score).ToList();
                var visuallySimilar = webDetection.VisuallySimilarImages.Select(v => new { v.Url }).ToList();
                var bestGuessLabels = webDetection.BestGuessLabels.Select(l => new { l.Label, l.LanguageCode }).ToList();

                // Serialize the structured results
                var result = new 
                {
                    BestGuessLabels = bestGuessLabels,
                    WebEntities = webEntities,
                    PagesWithMatchingImages = pagesWithMatches,
                    VisuallySimilarImages = visuallySimilar
                    // You might want to add FullMatchingImages and PartialMatchingImages too if needed
                };

                return JsonSerializer.Serialize(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[{ToolName}] Web detection cancelled.", Name);
                return JsonSerializer.Serialize(new { error = "Operation cancelled." }); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ToolName}] Error calling Google Vision API Web Detection.", Name);
                // Consider checking for specific gRPC/API exceptions if needed
                return JsonSerializer.Serialize(new { error = "Error calling Vision API: " + ex.Message });
            }
             // Added default return here to satisfy compiler, should be handled in catches
             return "{\"error\": \"Unhandled exception occurred.\"}";
        }
    }
} 