using System.Text.Json.Serialization;
using System.Collections.Generic; // Required for List<>

namespace Tsintra.MarketplaceAgent.DTOs;

public record PhotoProcessingInput
{
    [JsonPropertyName("input_images")]
    public List<ImageData> InputImages { get; init; } = [];

    [JsonPropertyName("rotation_angle")]
    public double RotationAngle { get; init; } = 0;

    [JsonPropertyName("background_color")]
    public string BackgroundColor { get; init; } = "Transparent"; // Default to transparent

    [JsonPropertyName("width")]
    public int? Width { get; init; } // Nullable for optional resizing

    [JsonPropertyName("height")]
    public int? Height { get; init; } // Nullable for optional resizing

    [JsonPropertyName("watermark_text")]
    public string? WatermarkText { get; init; } // Nullable for optional watermark

    [JsonPropertyName("watermark_font")]
    public string WatermarkFont { get; init; } = "Arial"; // Default font
} 