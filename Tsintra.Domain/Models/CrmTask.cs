using System;

namespace Tsintra.Domain.Models
{
    public class CrmTask
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TaskStatus Status { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid AssignedToUserId { get; set; }
        public Guid? RelatedCustomerId { get; set; }
        public Guid? RelatedOrderId { get; set; }
        public TaskPriority Priority { get; set; }
    }

    public enum TaskStatus
    {
        NotStarted,
        InProgress,
        OnHold,
        Completed,
        Cancelled
    }

    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }
} 