using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Api.Auth.Services
{
    public class JwtTokenService : Tsintra.Api.Auth.Interfaces.IJwtTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtTokenService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public JwtTokenService(
            IConfiguration configuration,
            ILogger<JwtTokenService> logger,
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository)
        {
            _configuration = configuration;
            _logger = logger;
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
        }

        public async Task<JwtTokenResponse> GenerateTokenAsync(User user)
        {
            try
            {
                _logger.LogInformation("Generating token for user: {UserId}", user.Id);

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]);
                var tokenValidityMinutes = int.Parse(_configuration["JWT:TokenValidityInMinutes"] ?? "60");
                var refreshTokenValidityDays = int.Parse(_configuration["JWT:RefreshTokenValidityInDays"] ?? "7");

                // Create user's full name from FirstName and LastName
                string fullName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = "Unknown";
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, fullName),
                    new Claim(ClaimTypes.Email, user.Email ?? "no-email@tsintra.com")
                };

                // Add more claims if needed
                // Currently User model doesn't have a Role property

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(tokenValidityMinutes),
                    Issuer = _configuration["JWT:ValidIssuer"],
                    Audience = _configuration["JWT:ValidAudience"],
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key), 
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var jwtToken = tokenHandler.WriteToken(token);
                var refreshToken = GenerateRefreshToken();

                // Save refresh token to database
                var tokenEntity = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiryDate = DateTime.UtcNow.AddDays(refreshTokenValidityDays),
                    CreatedAt = DateTime.UtcNow
                };

                await _refreshTokenRepository.CreateAsync(tokenEntity);

                return new JwtTokenResponse
                {
                    Token = jwtToken,
                    RefreshToken = refreshToken,
                    Expiration = tokenDescriptor.Expires.Value,
                    UserId = user.Id,
                    Name = fullName,
                    Email = user.Email
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for user: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<JwtTokenResponse> RefreshTokenAsync(string token, string refreshToken)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]);
                
                // Validate the token format without validating signature
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                
                if (userIdClaim == null)
                {
                    _logger.LogWarning("Invalid token - no user ID claim found");
                    return null;
                }

                if (!Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    _logger.LogWarning("Invalid user ID format in token: {UserId}", userIdClaim.Value);
                    return null;
                }

                // Check if refresh token exists and is valid
                var storedToken = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
                if (storedToken == null || storedToken.UserId != userId || storedToken.ExpiryDate < DateTime.UtcNow)
                {
                    _logger.LogWarning("Invalid refresh token for user: {UserId}", userId);
                    return null;
                }

                // Get user by ID
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for UserId: {UserId}", userId);
                    return null;
                }

                // Invalidate the old refresh token
                await _refreshTokenRepository.DeleteAsync(storedToken.Id);

                // Generate new tokens
                return await GenerateTokenAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return null;
            }
        }

        public bool ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["JWT:ValidIssuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["JWT:ValidAudience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out _);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
} 