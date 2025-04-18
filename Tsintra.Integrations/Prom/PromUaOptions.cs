using System.ComponentModel.DataAnnotations;

namespace Tsintra.Integrations.Prom; // Assuming it belongs here

public class PromUaOptions
{
    public const string SectionName = "PromUA";

    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
} 