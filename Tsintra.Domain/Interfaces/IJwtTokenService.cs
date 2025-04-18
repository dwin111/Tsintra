using System;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface IJwtTokenService
    {
        Task<JwtTokenResponse> GenerateTokenAsync(User user);
        Task<JwtTokenResponse> RefreshTokenAsync(string token, string refreshToken);
        bool ValidateToken(string token);
    }
}

namespace Tsintra.Domain.Models
{
    public class JwtTokenResponse
    {
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public DateTime Expiration { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
} 