
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Images;
using Tsintra.App.Interfaces;
using Tsintra.App.Models;
using Tsintra.Integrations.OpenAI.Mapping;
using Tsintra.Integrations.OpenAI.Models;

namespace Tsintra.Integrations.OpenAI
{
    public class OpenAiLLMClient : ILLMClient
    {
        private readonly ChatClient _chatClient;
        private readonly ImageClient _imageClient;


        public OpenAiLLMClient(IOptions<OpenAIConnect> opts)
        {
            var o = opts.Value;
            _chatClient = new ChatClient(o.ChatModel, o.ApiKey);
            _imageClient = new(o.ImageModel, o.ApiKey);

        }

        public async Task<string> GenerateTextAsync(string prompt,
            CancellationToken ct = default)
        {

            var userMsg = new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(prompt));

            var completion = await _chatClient.CompleteChatAsync(new[] { userMsg }, cancellationToken: ct);
            return completion.Value.Content.First().Text!;
        }

        public async Task<string> DescribeImagesAsync(string prompt, IEnumerable<ImageSource> imageSources,
            CancellationToken ct = default)
        {
            var parts = new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart(prompt)
            };


            foreach (var source in imageSources)
            {
                if (source.Data != null && !string.IsNullOrEmpty(source.MediaType) && !string.IsNullOrEmpty(source.FileName))
                {
                    parts.Add(ChatMessageContentPart.CreateImagePart(
                        BinaryData.FromBytes(source.Data),
                        source.MediaType,
                        "auto"));
                }
                else if (!string.IsNullOrEmpty(source.Url))
                {
                    parts.Add(ChatMessageContentPart.CreateImagePart(new Uri(source.Url), "auto"));
                }
            }

            //var userMsg = new UserChatMessage(
            //    ChatMessageContentPart.CreateTextPart(prompt));
            List<ChatMessage> userMsg =
            [
                new UserChatMessage(parts)
            ];



            ChatCompletion response = await _chatClient.CompleteChatAsync(userMsg ,cancellationToken: ct);


           return response.Content.First().Text!;

           
        }

        // Показую, як правильно згенерувати картинку та повернути URL
        public async Task<object> GenerateImageAsync(string prompt, ImageOptions opts,
        CancellationToken ct = default)
        {
            ImageGenerationOptions options = new()
            {
                Quality = ImageMapping.Map(opts.Quality),
                Size = ImageMapping.Map(opts.Size),
                Style = ImageMapping.Map(opts.Style),
                ResponseFormat = ImageMapping.Map(opts.Format)
            };

            GeneratedImage image = await _imageClient.GenerateImageAsync(prompt, options, ct);

            return opts.Format switch
            {
                ImageFormat.Uri => image.ImageUri.AbsoluteUri,
                ImageFormat.Bytes => image.ImageBytes,
                _ => throw new ArgumentOutOfRangeException(nameof(opts.Format), opts.Format, "Непідтримуваний формат відповіді")
            };
        }

    }
}
