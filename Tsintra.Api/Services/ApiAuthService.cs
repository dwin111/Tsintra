using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Api.Services
{
    public interface IApiAuthService
    {
        Task<User> GetCurrentUserAsync(string token);
        Task<bool> ValidateTokenAsync(string token);
    }

    public class ApiAuthService : IApiAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _authApiBaseUrl;
        private readonly ILogger<ApiAuthService> _logger;

        public ApiAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<ApiAuthService> logger)
        {
            _httpClient = httpClient;
            _authApiBaseUrl = configuration["AuthApi:BaseUrl"] ?? "https://localhost:7175";
            _logger = logger;

            // Додаємо базову URL адресу для всіх запитів
            _httpClient.BaseAddress = new Uri(_authApiBaseUrl);
        }

        public async Task<User> GetCurrentUserAsync(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await _httpClient.GetAsync("/api/auth/me");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    return user;
                }
                
                _logger.LogWarning("Failed to get user info from Auth API. Status code: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user information from Auth API");
                return null;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await _httpClient.GetAsync("/api/auth/validate-token");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token with Auth API");
                return false;
            }
        }
    }
} 