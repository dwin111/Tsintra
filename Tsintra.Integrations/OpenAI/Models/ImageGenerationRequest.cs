namespace Tsintra.Integrations.OpenAI.Models
{
    public class ImageGenerationRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string Quality { get; set; } = "standard";
        public string Size { get; set; } = "512x512";
        public string Style { get; set; } = "natural";
        public string ResponseFormat { get; set; } = "url";
    }
} 