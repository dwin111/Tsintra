using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    public class Order
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string MarketplaceType { get; set; } = string.Empty;
        public string MarketplaceId { get; set; } = string.Empty;
        public string MarketplaceName { get; set; } = string.Empty;
        public string MarketplaceOrderId { get; set; } = string.Empty;
        
        // Customer information
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; }
        
        // Order details
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        // Source information
        public string? Source { get; set; }
        
        // Shipping information
        public string ShippingMethod { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string ShippingCity { get; set; } = string.Empty;
        public string ShippingRegion { get; set; } = string.Empty;
        public string ShippingCountry { get; set; } = string.Empty;
        public string ShippingPostalCode { get; set; } = string.Empty;
        public string ShippingPhone { get; set; } = string.Empty;
        
        // Payment information
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public string? TransactionId { get; set; }
        
        // Order items
        public List<OrderItem> Items { get; set; } = new();
        
        // Additional information
        public string? Notes { get; set; }
        public Dictionary<string, object>? MarketplaceSpecificData { get; set; }
        
        // Delivery tracking
        public string? TrackingNumber { get; set; }
        public string? TrackingUrl { get; set; }
        public string? DeliveryService { get; set; }
        
        // Order history
        public List<OrderStatusHistory> StatusHistory { get; set; } = new();
    }

    public class OrderStatusHistory
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order Order { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? Notes { get; set; }
        public string? ChangedBy { get; set; }
    }
} 