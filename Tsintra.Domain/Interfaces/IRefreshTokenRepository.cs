using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken> GetByIdAsync(Guid id);
        Task<RefreshToken> GetByTokenAsync(string token);
        Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId);
        Task<RefreshToken> CreateAsync(RefreshToken refreshToken);
        Task UpdateAsync(RefreshToken refreshToken);
        Task DeleteAsync(Guid id);
        Task RevokeAsync(Guid id);
        Task RevokeAllUserTokensAsync(Guid userId);
        Task<IEnumerable<RefreshToken>> GetExpiredTokensAsync(DateTime cutoffDate);
        Task DeleteExpiredTokensAsync(DateTime cutoffDate);
        Task DeleteAllForUserAsync(Guid userId);
    }
} 