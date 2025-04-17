
namespace Tsintra.App.Models
{
    public enum ImageQuality { Standard, High }
    public enum ImageSize { W256xH256, W512xH512, W1024xH1024 }
    public enum ImageStyle { Natural, Vivid }
    public enum ImageFormat { Uri, Bytes }

    public record ImageOptions
    {
        // Додаємо обов'язкову властивість Prompt
        // "required" гарантує, що клієнт має її надіслати (потрібен C# 11 / .NET 7+)
        // Якщо використовуєте старшу версію, приберіть 'required' і додайте перевірку в контролері
        public required string Prompt { get; init; }

        // Інші властивості залишаються з значеннями за замовчуванням
        public ImageQuality Quality { get; init; } = ImageQuality.Standard;
        public ImageSize Size { get; init; } = ImageSize.W512xH512;
        public ImageStyle Style { get; init; } = ImageStyle.Natural;
        public ImageFormat Format { get; init; } = ImageFormat.Bytes;
    }
}
