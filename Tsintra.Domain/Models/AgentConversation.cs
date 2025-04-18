namespace Tsintra.Domain.Models;

public class AgentConversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ConversationData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property
    public User User { get; set; }
} 