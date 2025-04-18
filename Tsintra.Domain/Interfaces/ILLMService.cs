using Microsoft.AspNetCore.Http;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface ILLMService
    {
        Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null);
        Task<string> GenerateTextWithMemoryAsync(string prompt, string? systemPrompt = null, Guid? memoryId = null);
        Task<string> DescribeImagesAsync(string prompt, List<IFormFile> images, List<string> imageUrls);
        Task<string> GenerateImageAsync(ImageOptions options);
        Task<List<string>> ConvertImageSourcesAsync(List<IFormFile> images, List<string> imageUrls);
    }
} 