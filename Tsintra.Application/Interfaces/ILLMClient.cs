namespace Tsintra.App.Interfaces
{
    /// <summary>
    /// Загальний “порт” для чат‑LLM: надсилає список повідомлень (текст + зображення)
    /// і повертає відповідь асистента.
    /// </summary>
    public interface ILLMClient
    {

        Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);

        Task<string> DescribeImagesAsync(string prompt, IEnumerable<ImageSource> imageSources, CancellationToken ct = default);

    }

    public record  ImageSource(
         byte[] Data,
         string FileName,
         string MediaType,
         string Url 
    );


}
