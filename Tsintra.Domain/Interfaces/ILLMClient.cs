using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Core.Models;
using Microsoft.AspNetCore.Http;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    /// <summary>
    /// Загальний "порт" для чат-LLM: надсилає список повідомлень (текст + зображення)
    /// і повертає відповідь асистента.
    /// </summary>
    public interface ILLMClient
    {
        Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);

        Task<string> DescribeImagesAsync(string prompt, IEnumerable<ImageSource> imageSources, CancellationToken ct = default);
        Task<object> GenerateImageAsync(string prompt, ImageOptions opts, CancellationToken ct = default);

        Task<string> ChatCompletionAsync(List<Dictionary<string, string>> messages, Dictionary<string, object>? options = null);
        Task<string> CompletionAsync(string prompt, Dictionary<string, object>? options = null);
        Task<byte[]> GenerateImageAsync(ImageOptions options);
        Task<string> DescribeImageAsync(IFormFile image, string prompt);
    }

    public record ImageSource(
         byte[] Data,
         string FileName,
         string MediaType,
         string Url
    );
}
