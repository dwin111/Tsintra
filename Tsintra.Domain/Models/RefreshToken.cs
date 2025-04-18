using System;

namespace Tsintra.Domain.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsActive => RevokedAt == null && ExpiryDate > DateTime.UtcNow;
        
        // Navigation property
        public User User { get; set; }
    }
} 