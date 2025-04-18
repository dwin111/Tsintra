using System.ComponentModel.DataAnnotations;

namespace Tsintra.Api.Auth.Options;

public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;
} 