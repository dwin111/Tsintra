using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models;

public class MessageQueryOptions
{
    /// <summary>
    /// Filter messages starting from this date
    /// </summary>
    public DateTime? FromDate { get; set; }
    
    /// <summary>
    /// Filter messages up to this date
    /// </summary>
    public DateTime? ToDate { get; set; }
    
    /// <summary>
    /// Number of messages to skip for pagination
    /// </summary>
    public int Skip { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of messages to return
    /// </summary>
    public int Take { get; set; } = 50;
    
    /// <summary>
    /// Search text within messages
    /// </summary>
    public string SearchText { get; set; }
    
    /// <summary>
    /// Filter by message role (user or assistant)
    /// </summary>
    public string Role { get; set; }
    
    /// <summary>
    /// Order messages by timestamp (asc or desc)
    /// </summary>
    public string OrderBy { get; set; } = "asc"; // asc or desc
}

public class PaginatedResult<T>
{
    /// <summary>
    /// The current page items
    /// </summary>
    public List<T> Items { get; set; } = new List<T>();
    
    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int CurrentPage { get; set; }
    
    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }
    
    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;
    
    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;
}

public class ConversationWithMessagesDto
{
    /// <summary>
    /// Conversation ID
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// User ID who owns the conversation
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Conversation title
    /// </summary>
    public string Title { get; set; }
    
    /// <summary>
    /// When the conversation was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the conversation was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Messages in the conversation with pagination
    /// </summary>
    public PaginatedResult<Message> Messages { get; set; }
}

public class ChatSummaryDto
{
    /// <summary>
    /// Conversation details
    /// </summary>
    public Conversation Conversation { get; set; }
    
    /// <summary>
    /// First message in the conversation
    /// </summary>
    public Message FirstMessage { get; set; }
    
    /// <summary>
    /// Last message in the conversation
    /// </summary>
    public Message LastMessage { get; set; }
    
    /// <summary>
    /// Total number of messages in the conversation
    /// </summary>
    public int MessageCount { get; set; }
    
    /// <summary>
    /// When the first message was sent
    /// </summary>
    public DateTime FirstMessageTime => FirstMessage?.Timestamp ?? DateTime.MinValue;
    
    /// <summary>
    /// When the last message was sent
    /// </summary>
    public DateTime LastMessageTime => LastMessage?.Timestamp ?? DateTime.MinValue;
} 