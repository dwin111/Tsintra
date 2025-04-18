using System.ComponentModel.DataAnnotations;

namespace Tsintra.Integrations.OpenAI;

    public class OpenAiOptions
    {
        public const string SectionName = "OpenAI";
        [Required(ErrorMessage = "OpenAI API Key is required.")]
        public string? ApiKey { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public string? ChatModel { get; set; } = "gpt-4-turbo";
        public string? ImageModel { get; set; } = "dall-e-3";
    }