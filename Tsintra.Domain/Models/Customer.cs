using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    public class Customer
    {
        public Guid Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string MarketplaceType { get; set; } = string.Empty;
        public string MarketplaceId { get; set; } = string.Empty;
        public Dictionary<string, string> MarketplaceIdentifiers { get; set; } = new();
        
        // Basic information
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        
        // Address information
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        
        // Additional information
        public string? Company { get; set; }
        public string? TaxNumber { get; set; }
        public string? Notes { get; set; }
        
        // Marketplace specific data
        public Dictionary<string, object>? MarketplaceSpecificData { get; set; }
        
        // Orders
        public List<Order> Orders { get; set; } = new();
        
        // Communication preferences
        public bool AllowMarketingEmails { get; set; }
        public bool AllowMarketingSms { get; set; }
        
        // Customer status
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastOrderDate { get; set; }
        
        // Customer segments
        public List<string> Tags { get; set; } = new();
        public string? CustomerType { get; set; } // B2B, B2C, etc.
        
        // Customer value metrics
        public decimal TotalSpent { get; set; }
        public int OrderCount { get; set; }
        public decimal AverageOrderValue { get; set; }
    }
} 