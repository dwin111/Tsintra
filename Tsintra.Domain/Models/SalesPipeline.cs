using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    public class Pipeline
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<PipelineStage> Stages { get; set; } = new List<PipelineStage>();
    }

    public class PipelineStage
    {
        public Guid Id { get; set; }
        public Guid PipelineId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public double Probability { get; set; }
        public bool IsWon { get; set; }
        public bool IsLost { get; set; }
    }

    public class Deal
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; }
        public Guid PipelineId { get; set; }
        public Guid StageId { get; set; }
        public decimal Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpectedCloseDate { get; set; }
        public DateTime? ClosedAt { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public string Notes { get; set; }
        public DealStatus Status { get; set; }
        public List<DealActivity> Activities { get; set; } = new List<DealActivity>();
    }

    public enum DealStatus
    {
        Open,
        Won,
        Lost,
        OnHold,
        Abandoned
    }

    public class DealActivity
    {
        public Guid Id { get; set; }
        public Guid DealId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public DealActivityType Type { get; set; }
    }

    public enum DealActivityType
    {
        Note,
        Call,
        Email,
        Meeting,
        Task,
        StatusChange,
        StageChange,
        Other
    }
} 