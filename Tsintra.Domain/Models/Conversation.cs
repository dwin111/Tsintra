using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models;

public class Conversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; }
    public List<Message> Messages { get; set; } = new List<Message>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User User { get; set; }
} 