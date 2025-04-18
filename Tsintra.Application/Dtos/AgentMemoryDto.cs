using System;

namespace Tsintra.Application.DTOs;

public class AgentMemoryDto
{
    public Guid Id { get; set; }
    public string ConversationId { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CreateAgentMemoryRequest
{
    public string ConversationId { get; set; }
    public string Content { get; set; }
    public DateTime? ExpiresAt { get; set; }
} 