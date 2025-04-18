namespace Tsintra.Application.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty; // Initialize to avoid null warnings
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }
} 