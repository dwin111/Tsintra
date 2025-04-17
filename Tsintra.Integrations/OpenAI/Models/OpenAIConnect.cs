namespace Tsintra.Integrations.OpenAI.Models
{
    public class OpenAIConnect
    {
        public string ApiKey { get; set; } = default!;
        public string ChatModel { get; set; } = "gpt-4o";
        public string ImageModel { get; set; } = "dall-e-2";
    }
}
