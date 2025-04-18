using System;

namespace Tsintra.Domain.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } // "user" or "assistant"
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    
    public Conversation Conversation { get; set; }
} 