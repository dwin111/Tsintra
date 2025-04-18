using System;

namespace Tsintra.MarketplaceAgent.Models.AI // Using root namespace for now
{
    /// <summary>
    /// Represents a part of a chat message, supporting multi-modal content.
    /// </summary>
    public record ChatMessageContentPart
    {
        public enum PartType { Text, Image }

        public PartType Type { get; init; }
        public string? Text { get; init; }
        public byte[]? ImageData { get; init; }
        public string? MediaType { get; init; } 

        private ChatMessageContentPart(PartType type) { Type = type; }

        public static ChatMessageContentPart CreateText(string text)
        {
            if (string.IsNullOrEmpty(text)) 
                throw new ArgumentNullException(nameof(text));
            return new ChatMessageContentPart(PartType.Text) { Text = text };
        }

        public static ChatMessageContentPart CreateImage(byte[] imageData, string mediaType)
        {
            if (imageData == null || imageData.Length == 0) 
                throw new ArgumentNullException(nameof(imageData));
            if (string.IsNullOrEmpty(mediaType)) 
                throw new ArgumentNullException(nameof(mediaType));
            return new ChatMessageContentPart(PartType.Image) { ImageData = imageData, MediaType = mediaType };
        }
    }
} 