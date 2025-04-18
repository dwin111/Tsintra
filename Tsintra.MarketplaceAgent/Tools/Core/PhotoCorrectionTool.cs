using System.Text.Json;
// Corrected using statements
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Drawing.Processing; // Added for watermarking
using SixLabors.Fonts; // Added for watermarking
using Tsintra.MarketplaceAgent.DTOs;
using Tsintra.MarketplaceAgent.Interfaces;
using System.IO; // Added for MemoryStream
using System.Collections.Generic; // Added for List<>
using System.Threading.Tasks; // Added for Task<>
using System.Linq; // Added for Any()
using Tsintra.Integrations.Interfaces; // *** ADDED for S3 ***
using Microsoft.Extensions.Options; // *** ADDED for Options ***
using Tsintra.Integrations; // *** ADDED for AwsOptions ***
using Tsintra.MarketplaceAgent.Agents; // *** ADDED for PhotoProcessingInputS3 ***
using System;
using Microsoft.Extensions.Caching.Memory; // *** ADDED for IMemoryCache ***

namespace Tsintra.MarketplaceAgent.Tools.Core
{
    // Ensure ITool is implemented correctly
    public class PhotoCorrectionTool : ITool<PhotoProcessingInputS3, List<string>>
    {
        private readonly ILogger<PhotoCorrectionTool> _logger;
        private readonly FontCollection _fontCollection; // For watermarking
        private readonly IS3StorageService _s3StorageService; // *** ADDED ***
        private readonly string _s3BucketName; // *** ADDED ***
        private readonly IMemoryCache _memoryCache; // *** ADDED for base64 caching ***

        public PhotoCorrectionTool(
            ILogger<PhotoCorrectionTool> logger,
            IS3StorageService s3StorageService,
            IOptions<AwsOptions> awsOptions,
            IMemoryCache memoryCache) // *** ADDED ***
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _s3StorageService = s3StorageService ?? throw new ArgumentNullException(nameof(s3StorageService));
            var options = awsOptions?.Value ?? throw new ArgumentNullException(nameof(awsOptions));
            _s3BucketName = options.S3?.BucketName ?? throw new InvalidOperationException("S3 BucketName is not configured in AwsOptions.");
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

            _fontCollection = new FontCollection();
            // Consider registering fonts properly if needed, especially custom ones
            // For simplicity, assuming system fonts are findable or Arial is available
             try
            {
                 // Attempt to preload the font to catch issues early
                 SystemFonts.TryGet( "Arial", out var _);
            }
             catch (Exception ex)
            {
                 _logger.LogWarning(ex, "Could not preload Arial font. Watermarking might fail if font is not found.");
            }
        }

        public string Name => "PhotoProcessing";
        public string Description => "Processes images from S3: corrects, resizes, watermarks, and saves back to S3.";

        public async Task<List<string>> RunAsync(PhotoProcessingInputS3 input, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[{ToolName}] Running S3 photo processing...", Name);
            
            var processedS3Keys = new List<string>();
            var processedBase64CacheKeys = new List<string>();

            // Input validation changed for InputS3Keys
            if (input == null || input.InputS3Keys == null || !input.InputS3Keys.Any())
            {
                _logger.LogWarning("[{ToolName}] Input PhotoProcessingInputS3 object is null or has no InputS3Keys.", Name);
                return processedS3Keys; // Return empty list
            }

            int index = 0;
            // Loop through input S3 keys
            foreach (var inputS3Key in input.InputS3Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;

                if (string.IsNullOrWhiteSpace(inputS3Key))
                {
                    _logger.LogWarning("[{ToolName}] Skipping null or empty S3 key at index {Index}.", Name, index);
                    continue;
                }

                string currentImageKey = inputS3Key;
                Stream? imageStream = null;
                Image? img = null;

                try
                {
                    _logger.LogDebug("[{ToolName}] Processing image from S3 key: {Key}", Name, currentImageKey);

                    // *** S3 Download ***
                    imageStream = await _s3StorageService.DownloadFileAsStreamAsync(_s3BucketName, currentImageKey, cancellationToken);

                    if (imageStream == null)
                    {
                        _logger.LogWarning("[{ToolName}] Could not download image from S3 key: {Key}. Checking for base64 fallback.", Name, currentImageKey);
                        
                        // Check if we have a base64 fallback for this S3 key
                        string base64CacheKey = $"base64-{input.RunId}-{index}";
                        if (_memoryCache.TryGetValue(base64CacheKey, out string? base64Image) && !string.IsNullOrEmpty(base64Image))
                        {
                            _logger.LogDebug("[{ToolName}] Found base64 fallback for S3 key: {Key}", Name, currentImageKey);
                            var bytes = Convert.FromBase64String(base64Image);
                            imageStream = new MemoryStream(bytes);
                        }
                        else
                        {
                            _logger.LogWarning("[{ToolName}] No base64 fallback found for S3 key: {Key}. Skipping.", Name, currentImageKey);
                            continue;
                        }
                    }

                    // Load image from downloaded stream
                    img = await Image.LoadAsync(imageStream, cancellationToken);

                    // Apply mutations based on input
                    img.Mutate(ctx => {
                        ctx.AutoOrient(); // Keep auto-orientation

                        // Apply rotation
                        if (input.RotationAngle != 0)
                        {
                             ctx.Rotate((float)input.RotationAngle);
                        }

                        // Apply resizing if Width and Height are provided
                        if (input.Width.HasValue && input.Height.HasValue)
                        {
                             // Ensure background color is parsed correctly. Default to Transparent if invalid.
                            Color backgroundColor = Color.Transparent;
                             if (!string.IsNullOrWhiteSpace(input.BackgroundColor))
                            {
                                 Color.TryParse(input.BackgroundColor, out backgroundColor);
                            }

                             ctx.Resize(new ResizeOptions
                            {
                                 Size = new Size(input.Width.Value, input.Height.Value),
                                 Mode = ResizeMode.Pad, // Use Pad to fill background
                                 PadColor = backgroundColor
                            });
                        } else if (input.Width.HasValue || input.Height.HasValue) {
                             _logger.LogWarning("[{ToolName}] Both Width and Height must be provided for resizing. Skipping resize for image key {Key}.", Name, currentImageKey);
                        }


                        // Apply basic corrections (can be parameterized later if needed)
                        ctx.Contrast(1.1f).Brightness(1.05f).Saturate(1.05f);

                        // Apply watermark if text is provided
                        if (!string.IsNullOrWhiteSpace(input.WatermarkText))
                        {
                             // Attempt to find the specified font, default to Arial
                            if(!SystemFonts.TryGet(input.WatermarkFont ?? "Arial", out var fontFamily))
                            {
                                 _logger.LogWarning("[{ToolName}] Font '{FontName}' not found. Falling back to Arial for watermark.", Name, input.WatermarkFont);
                                 SystemFonts.TryGet("Arial", out fontFamily); // Fallback
                            }

                            if(fontFamily != null)
                            {
                                // Dynamically size the font based on image width (e.g., 5% of width)
                                var fontSize = img.Width / 20f; // Adjust divisor for desired size
                                var font = fontFamily.CreateFont(fontSize, FontStyle.Regular);

                                var textOptions = new RichTextOptions(font)
                                {
                                    Origin = new PointF(img.Width * 0.05f, img.Height * 0.9f), // Position watermark bottom-left
                                    WrappingLength = img.Width * 0.9f, // Wrap text if needed
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                    VerticalAlignment = VerticalAlignment.Bottom
                                };

                                // Draw watermark with semi-transparent black text
                                ctx.DrawText(textOptions, input.WatermarkText, Color.FromRgba(0, 0, 0, 128));
                             } else {
                                 _logger.LogWarning("[{ToolName}] Could not find fallback font Arial. Skipping watermark for image key {Key}.", Name, currentImageKey);
                             }
                        }
                    });
                    
                    // Save processed image to byte array in memory
                    using var ms = new MemoryStream();
                    await img.SaveAsPngAsync(ms, cancellationToken); // Save as PNG to memory stream
                    ms.Position = 0; // Reset stream position for upload

                    // *** S3 Upload Processed Image ***
                    // Generate a new S3 key for the processed image
                    string processedS3Key = $"processed/{input.RunId}/image-{index}.png"; // Use RunId from input
                    string? uploadedKey = await _s3StorageService.UploadFileAsync(_s3BucketName, processedS3Key, ms, "image/png", cancellationToken);

                    if (!string.IsNullOrEmpty(uploadedKey))
                    {
                        processedS3Keys.Add(uploadedKey);
                        _logger.LogDebug("[{ToolName}] Finished processing. Uploaded processed image to S3 key: {Key}", Name, uploadedKey);
                    }
                    else
                    {
                        _logger.LogWarning("[{ToolName}] Failed to upload processed image to S3 for original key: {OriginalKey}. Using base64 fallback.", Name, currentImageKey);
                        
                        // Convert processed image to base64 and cache it
                        ms.Position = 0;
                        var processedBytes = ms.ToArray();
                        string processedBase64 = Convert.ToBase64String(processedBytes);
                        string processedBase64CacheKey = $"processed-base64-{input.RunId}-{index}";
                        _memoryCache.Set(processedBase64CacheKey, processedBase64, TimeSpan.FromMinutes(30));
                        processedBase64CacheKeys.Add(processedBase64CacheKey);
                        _logger.LogDebug("[{ToolName}] Cached processed base64 image with key: {CacheKey}", Name, processedBase64CacheKey);
                    }
                }
                catch (ImageFormatException imageEx)
                {
                    _logger.LogError(imageEx, "[{ToolName}] Could not decode image from S3 stream for key {Key}.", Name, currentImageKey);
                }
                catch (IOException ioEx)
                {
                     // This might still happen with MemoryStream, though less likely
                     _logger.LogError(ioEx, "[{ToolName}] IO error processing image from S3 key {Key}", Name, currentImageKey);
                }
                catch (OperationCanceledException)
                {
                     _logger.LogInformation("[{ToolName}] Photo processing cancelled for S3 key {Key}.", Name, currentImageKey);
                     throw; // Re-throw cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{ToolName}] Failed to process image from S3 key {Key}.", Name, currentImageKey);
                }
                finally
                {
                    // Ensure resources are disposed
                    img?.Dispose();
                    imageStream?.Dispose();
                }
            }

            // Return the list of processed S3 keys
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("[{ToolName}] Photo processing cancelled. Returning {S3Count} S3 keys and {Base64Count} base64 cache keys.", 
                    Name, processedS3Keys.Count, processedBase64CacheKeys.Count);
            }
            else
            {
                _logger.LogInformation("[{ToolName}] Processed {S3Count} S3 images and {Base64Count} base64 images.", 
                    Name, processedS3Keys.Count, processedBase64CacheKeys.Count);
            }

            // Return both S3 keys and base64 cache keys
            return processedS3Keys.Concat(processedBase64CacheKeys).ToList();
        }
    }
} 