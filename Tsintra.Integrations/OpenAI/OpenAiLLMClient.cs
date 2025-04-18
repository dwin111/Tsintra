using Microsoft.Extensions.Options;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using OpenAI.Chat;
using OpenAI.Images;
using System.Text.Json;
using System.Net.Http.Json;
using Tsintra.Integrations.OpenAI.Mapping;
using Microsoft.AspNetCore.Http;

namespace Tsintra.Integrations.OpenAI
{
    public class OpenAiLLMClient : ILLMClient
    {
        private readonly ChatClient _chatClient;
        private readonly ImageClient _imageClient;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _imageModel;

        public OpenAiLLMClient(IOptions<OpenAiOptions> opts)
        {
            var o = opts.Value;
            _chatClient = new ChatClient(o.ChatModel, o.ApiKey);
            _imageClient = new(o.ImageModel, o.ApiKey);
            _httpClient = new HttpClient();
            _apiKey = o.ApiKey;
            _imageModel = o.ImageModel;
        }

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            var userMsg = new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(prompt));

            var completion = await _chatClient.CompleteChatAsync(new[] { userMsg }, cancellationToken: ct);
            return completion.Value.Content.First().Text!;
        }

        public async Task<string> DescribeImagesAsync(string prompt, IEnumerable<ImageSource> imageSources, CancellationToken ct = default)
        {
            var parts = new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart(prompt)
            };

            foreach (var source in imageSources)
            {
                if (source.Data != null && !string.IsNullOrEmpty(source.MediaType) && !string.IsNullOrEmpty(source.FileName))
                {
                    parts.Add(ChatMessageContentPart.CreateImagePart(new BinaryData(source.Data), source.MediaType, source.FileName));
                }
                else if (!string.IsNullOrEmpty(source.Url))
                {
                    parts.Add(ChatMessageContentPart.CreateImagePart(new Uri(source.Url)));
                }
            }

            var userMsg = new UserChatMessage(parts.ToArray());

            var completion = await _chatClient.CompleteChatAsync(new[] { userMsg }, cancellationToken: ct);
            return completion.Value.Content.First().Text!;
        }

        public async Task<object> GenerateImageAsync(string prompt, ImageOptions options, CancellationToken ct = default)
        {
            // Створюємо внутрішній формат опцій для API
            var internalOptions = new ImageGenerationOptions
            {
                Quality = ConvertQuality(options),
                Size = ConvertSize(options),
                Style = ConvertStyle(options),
                ResponseFormat = GeneratedImageFormat.Bytes // За замовчуванням використовуємо байти
            };

            var imageResponse = await _imageClient.GenerateImageAsync(prompt, internalOptions, ct);
            var image = imageResponse.Value;
            
            // За замовчуванням повертаємо байти зображення
            return image.ImageBytes;
        }

        // Методи для конвертації опцій
        private GeneratedImageQuality ConvertQuality(ImageOptions options)
        {
            return options.Quality == "high" ? GeneratedImageQuality.High : GeneratedImageQuality.Standard;
        }

        private GeneratedImageSize ConvertSize(ImageOptions options)
        {
            return (options.Width, options.Height) switch
            {
                (256, 256) => GeneratedImageSize.W256xH256,
                (512, 512) => GeneratedImageSize.W512xH512,
                _ => GeneratedImageSize.W1024xH1024
            };
        }

        private GeneratedImageStyle ConvertStyle(ImageOptions options)
        {
            return options.Style == "vivid" ? GeneratedImageStyle.Vivid : GeneratedImageStyle.Natural;
        }

        // Реалізація нових методів інтерфейсу
        public async Task<string> ChatCompletionAsync(List<Dictionary<string, string>> messages, Dictionary<string, object>? options = null)
        {
            var chatMessages = new List<ChatMessage>();

            foreach (var msg in messages)
            {
                if (msg.TryGetValue("role", out var role) && msg.TryGetValue("content", out var content))
                {
                    chatMessages.Add(role.ToLower() switch
                    {
                        "user" => new UserChatMessage(ChatMessageContentPart.CreateTextPart(content)),
                        "system" => new SystemChatMessage(content),
                        "assistant" => new AssistantChatMessage(ChatMessageContentPart.CreateTextPart(content)),
                        _ => new UserChatMessage(ChatMessageContentPart.CreateTextPart(content))
                    });
                }
            }

            var completion = await _chatClient.CompleteChatAsync(chatMessages.ToArray());
            return completion.Value.Content.First().Text!;
        }

        public async Task<string> CompletionAsync(string prompt, Dictionary<string, object>? options = null)
        {
            // В OpenAI API v1 немає прямого методу для завершення тексту, тому використовуємо ChatCompletion
            return await GenerateTextAsync(prompt);
        }

        public async Task<byte[]> GenerateImageAsync(ImageOptions options)
        {
            var result = await GenerateImageAsync(options.Prompt, options);
            return (byte[])result;
        }

        public async Task<string> DescribeImageAsync(IFormFile image, string prompt)
        {
            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var imageSource = new ImageSource(
                ms.ToArray(),
                image.FileName,
                image.ContentType,
                string.Empty
            );

            return await DescribeImagesAsync(prompt, new[] { imageSource });
        }
    }
}
