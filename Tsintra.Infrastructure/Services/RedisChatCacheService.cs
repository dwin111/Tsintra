using System;

namespace Tsintra.Infrastructure.Services
{
    public class RedisChatCacheService
    {
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromDays(90);
    }
} 