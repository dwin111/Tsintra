using System;

namespace Tsintra.Domain.Models;

/// <summary>
/// Представляє короткострокову пам'ять агента, яка пов'язана з конкретною розмовою
/// </summary>
public class AgentConversationMemory
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public required string Key { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;
    public required string Source { get; set; }
    
    // Navigation property
    public virtual Conversation? Conversation { get; set; }
} 