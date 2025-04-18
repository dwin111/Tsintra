using System;
using System.Collections.Generic;
using System.Linq;

namespace Tsintra.MarketplaceAgent.Models.AI // Using root namespace for now
{
    /// <summary>
    /// Represents a single message in a chat conversation, potentially multi-modal.
    /// </summary>
    public record AiChatMessage
    {
        public ChatMessageRole Role { get; init; }
        public List<ChatMessageContentPart> Content { get; init; }

        private AiChatMessage(ChatMessageRole role, List<ChatMessageContentPart> content)
        {
            Role = role;
            Content = content;
        }

        public static AiChatMessage Create(ChatMessageRole role, string textContent)
        {
            return new AiChatMessage(role, new List<ChatMessageContentPart> { ChatMessageContentPart.CreateText(textContent) });
        }

        public static AiChatMessage Create(ChatMessageRole role, IEnumerable<ChatMessageContentPart> contentParts)
        {
            var parts = contentParts?.ToList() ?? new List<ChatMessageContentPart>();
            if (!parts.Any())
                throw new ArgumentException("Content parts cannot be empty.", nameof(contentParts));
            return new AiChatMessage(role, parts);
        }
    }
} 