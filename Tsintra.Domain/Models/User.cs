namespace Tsintra.Domain.Models;

public class User
{
    public Guid Id { get; set; }
    public string GoogleId { get; set; } // Identifier from Google
    public string Email { get; set; }
    public string? FirstName { get; set; } // Optional
    public string? LastName { get; set; }  // Optional
    public string? ProfilePictureUrl { get; set; } // Optional
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public ICollection<AgentConversation> AgentConversations { get; set; } = new List<AgentConversation>();
    public ICollection<AgentMemory> AgentMemories { get; set; } = new List<AgentMemory>();
} 