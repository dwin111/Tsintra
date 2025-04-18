using System;

namespace Tsintra.Domain.Models
{
    public class Communication
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid? UserId { get; set; }
        public CommunicationType Type { get; set; }
        public string Subject { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public string ContactInfo { get; set; }
        public Guid? RelatedOrderId { get; set; }
        public Guid? RelatedTaskId { get; set; }
        public bool IsIncoming { get; set; }
    }

    public enum CommunicationType
    {
        Email,
        Call,
        SMS,
        Meeting,
        Chat,
        SocialMedia,
        Other
    }
} 