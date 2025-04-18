using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Tsintra.Api.Crm.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _authApiBaseUrl;

        public AuthService(
            HttpClient httpClient,
            ILogger<AuthService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _authApiBaseUrl = _configuration["AuthService:BaseUrl"] ?? "http://localhost:5001";
        }

        /// <summary>
        /// Validates a JWT token by calling the Auth API
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                _logger.LogInformation($"Validating token at URL: {_authApiBaseUrl}/api/Auth/validate-token");
                
                // Логування початку токена (перші 10 символів) для діагностики
                if (!string.IsNullOrEmpty(token) && token.Length > 10)
                {
                    _logger.LogInformation($"Token starts with: {token.Substring(0, 10)}...");
                }
                
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync($"{_authApiBaseUrl}/api/Auth/validate-token");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Token validation successful");
                    return true;
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Token validation failed. Status: {response.StatusCode}, Response: {content}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        /// <summary>
        /// Gets user information from a valid token
        /// </summary>
        public async Task<UserInfo> GetUserInfoFromTokenAsync(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync($"{_authApiBaseUrl}/api/Auth/me");
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserInfo>() ?? new UserInfo();
                }
                
                return new UserInfo();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info from token");
                return new UserInfo();
            }
        }

        /// <summary>
        /// Revokes a token by calling the Auth API
        /// </summary>
        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var content = JsonContent.Create(new { token });
                var response = await _httpClient.PostAsync($"{_authApiBaseUrl}/api/Auth/revoke", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return false;
            }
        }
    }

    public interface IAuthService
    {
        Task<bool> ValidateTokenAsync(string token);
        Task<UserInfo> GetUserInfoFromTokenAsync(string token);
        Task<bool> RevokeTokenAsync(string token);
    }

    public class UserInfo
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ProfilePictureUrl { get; set; } = string.Empty;
    }
} 