namespace Tsintra.Domain.Models;

/// <summary>
/// Options for generating images
/// </summary>
public class ImageOptions
{
    /// <summary>
    /// The prompt to generate the image from
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
    
    /// <summary>
    /// The number of images to generate
    /// </summary>
    public int Count { get; set; } = 1;
    
    /// <summary>
    /// The width of the image
    /// </summary>
    public int Width { get; set; } = 1024;
    
    /// <summary>
    /// The height of the image
    /// </summary>
    public int Height { get; set; } = 1024;
    
    /// <summary>
    /// The style of the image
    /// </summary>
    public string? Style { get; set; }
    
    /// <summary>
    /// The quality of the image
    /// </summary>
    public string Quality { get; set; } = "standard";
}
