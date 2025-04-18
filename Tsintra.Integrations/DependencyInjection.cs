using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsintra.Domain.Interfaces;
using Tsintra.Integrations.OpenAI;
using Tsintra.Integrations.Prom;
using Microsoft.Extensions.Options;
using Tsintra.Integrations.Interfaces;
using Tsintra.Integrations.Services;
using Amazon.S3;
using Amazon.Runtime;
using Amazon;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System;
using Microsoft.AspNetCore.Http;
using Tsintra.Domain.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tsintra.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure OpenAI if enabled in configuration (optional integration)
        var openAiSection = configuration.GetSection(OpenAiOptions.SectionName);
        if (openAiSection.Exists() && !string.IsNullOrEmpty(openAiSection["ApiKey"]))
        {
            services.AddOptions<OpenAiOptions>()
                .Bind(openAiSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register OpenAiLLMClient if OpenAI is configured
            services.AddSingleton<ILLMClient, OpenAiLLMClient>();
        }
        else
        {
            // Register a null/stub implementation when OpenAI is not configured
            services.AddSingleton<ILLMClient, NullLLMClient>();
        }

        // Configure Options using the created classes and SectionName constants for Prom.ua
        services.AddOptions<PromUaOptions>()
            .Bind(configuration.GetSection(PromUaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register PromUAClient, which implements IMarketplaceClient
        services.AddHttpClient<IMarketplaceClient, PromUAClient>(client =>
        {
            // Configure base address or other settings if needed directly from config
            // Or rely on IOptions injected into PromUAClient constructor
        }); //.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()); // Usually not needed unless specific handler config required

        // Add integration services
        services.AddIntegrationServices(configuration);

        return services;
    }

    public static IServiceCollection AddIntegrationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure and register OpenAI if enabled
        var openAiSection = configuration.GetSection(OpenAiOptions.SectionName);
        if (openAiSection.Exists() && !string.IsNullOrEmpty(openAiSection["ApiKey"]))
        {
            services.Configure<OpenAiOptions>(openAiSection);
            services.AddScoped<ILLMClient, OpenAiLLMClient>();
        }
        else
        {
            // Register a null/stub implementation when OpenAI is not configured
            services.AddScoped<ILLMClient, NullLLMClient>();
        }

        // Configure and register Prom.ua client
        services.Configure<PromUaOptions>(configuration.GetSection(PromUaOptions.SectionName));
        services.AddHttpClient<IMarketplaceClient, PromUAClient>();
        
        // Register PromMarketplaceIntegration as implementation of IMarketplaceIntegration
        services.AddScoped<IMarketplaceIntegration, PromMarketplaceIntegration>();

        // Configure and register AWS S3
        services.Configure<AwsOptions>(configuration.GetSection("AWS"));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var logger = sp.GetService<ILogger<S3StorageService>>();

            var awsOptions = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var region = RegionEndpoint.GetBySystemName(awsOptions.Region);
            
            // Check if credentials are provided in configuration
            if (!string.IsNullOrEmpty(awsOptions.AccessKeyId) && !string.IsNullOrEmpty(awsOptions.SecretAccessKey))
            {
                logger?.LogInformation("Using AWS credentials from configuration");
                var credentials = new BasicAWSCredentials(awsOptions.AccessKeyId, awsOptions.SecretAccessKey);
                return new AmazonS3Client(credentials, new AmazonS3Config { RegionEndpoint = region });
            }
            // Для сумісності перевіряємо також старі ключі в конфігурації (якщо вони використовуються)
            if (!string.IsNullOrEmpty(awsOptions.S3?.AccessKeyId) && !string.IsNullOrEmpty(awsOptions.S3?.SecretAccessKey))
            {
                logger?.LogInformation("Using AWS credentials from legacy S3 configuration");
                var credentials = new BasicAWSCredentials(awsOptions.S3.AccessKeyId, awsOptions.S3.SecretAccessKey);
                return new AmazonS3Client(credentials, new AmazonS3Config { RegionEndpoint = region });
            }

            // Otherwise use the SDK's default credential resolution (env vars, EC2 instance profile, etc)
            logger?.LogInformation("Using AWS credentials from SDK's default credential resolution chain");
            return new AmazonS3Client(new AmazonS3Config { RegionEndpoint = region });
        });

        // Register S3StorageService
        services.Configure<S3Options>(options => 
        {
            var s3Section = configuration.GetSection("S3");
            s3Section.Bind(options);
            
            // Для сумісності з існуючим кодом
            var awsS3Section = configuration.GetSection("AWS:S3");
            if (awsS3Section.Exists() && string.IsNullOrEmpty(options.BucketName))
            {
                options.BucketName = awsS3Section["BucketName"];
            }
            if (string.IsNullOrEmpty(options.DefaultBucket) && !string.IsNullOrEmpty(options.BucketName))
            {
                options.DefaultBucket = options.BucketName;
            }
        });
        services.AddScoped<IS3StorageService, S3StorageService>();

        return services;
    }
}

// Classes for options pattern
public class AwsOptions
{
    public string Region { get; set; } = "eu-central-1"; // Default to EU Frankfurt
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public S3Options S3 { get; set; } = new S3Options();
}

public class S3Options
{
    public string DefaultBucket { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty; // Для сумісності з існуючим кодом
    public string AccessKeyId { get; set; } = string.Empty; // Для сумісності з існуючим кодом
    public string SecretAccessKey { get; set; } = string.Empty; // Для сумісності з існуючим кодом
}

// Add null implementation for LLM service when OpenAI is not configured
public class NullLLMClient : ILLMClient
{
    private readonly ILogger<NullLLMClient> _logger;
    private const string NOT_CONFIGURED_MESSAGE = "OpenAI integration not configured. Please configure OpenAI:ApiKey in settings.";
    
    public NullLLMClient(ILogger<NullLLMClient> logger = null)
    {
        _logger = logger;
    }

    public Task<string> GenerateContentAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient. Add OpenAI:ApiKey to configuration to enable LLM features.");
        return Task.FromResult(NOT_CONFIGURED_MESSAGE);
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        return Task.FromResult(NOT_CONFIGURED_MESSAGE);
    }

    public Task<string> DescribeImagesAsync(string prompt, IEnumerable<ImageSource> imageSources, CancellationToken ct = default)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        return Task.FromResult(NOT_CONFIGURED_MESSAGE);
    }

    public Task<object> GenerateImageAsync(string prompt, ImageOptions opts, CancellationToken ct = default)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        return Task.FromResult<object>(NOT_CONFIGURED_MESSAGE);
    }

    public Task<string> ChatCompletionAsync(List<Dictionary<string, string>> messages, Dictionary<string, object>? options = null)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        return Task.FromResult(NOT_CONFIGURED_MESSAGE);
    }

    public Task<string> CompletionAsync(string prompt, Dictionary<string, object>? options = null)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        return Task.FromResult(NOT_CONFIGURED_MESSAGE);
    }

    public Task<byte[]> GenerateImageAsync(ImageOptions options)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        // Return an empty byte array since we can't generate an image
        return Task.FromResult(new byte[0]);
    }

    public Task<string> DescribeImageAsync(IFormFile image, string prompt)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        return Task.FromResult(NOT_CONFIGURED_MESSAGE);
    }

    public Task<string> GenerateImageAsync(string prompt, int width = 1024, int height = 1024, CancellationToken cancellationToken = default)
    {
        _logger?.LogWarning("OpenAI integration not configured. Using NullLLMClient.");
        return Task.FromResult(NOT_CONFIGURED_MESSAGE);
    }
}
