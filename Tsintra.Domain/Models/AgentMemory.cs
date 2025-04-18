using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models;

/// <summary>
/// Представляє довгострокову пам'ять агента, яка зберігається для користувача
/// </summary>
public class AgentLongTermMemory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Key { get; set; }  // Унікальний ключ для цієї пам'яті
    public required string Content { get; set; }
    public int Priority { get; set; } = 0;  // Пріоритет пам'яті, вищий пріоритет означає більшу важливість
    public string Category { get; set; } = "general"; // Категорія пам'яті: preferences, facts, rules і т.д.
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastAccessed { get; set; } // Час останнього доступу до пам'яті
    
    // Navigation property
    public User? User { get; set; }
}

/// <summary>
/// Стара (застаріла) модель пам'яті агента.
/// Збережена для зворотньої сумісності.
/// Нові релізи повинні використовувати AgentLongTermMemory та AgentConversationMemory.
/// </summary>
public class AgentMemory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string ConversationId { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Navigation property
    public User? User { get; set; }
} 