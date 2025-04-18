namespace Tsintra.Domain.Models
{
    public enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled,
        Refunded,
        OnHold,
        Failed,
        Completed
    }
} 