using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.Extensions.Logging;
using Tsintra.Api.Crm.Services;

namespace Tsintra.Api.Crm.Middleware
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthMiddleware> _logger;

        public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            
            // Skip validation for swagger endpoints and development endpoints
            if (path.StartsWith("/swagger") || 
                path.StartsWith("/api/health") || 
                path.StartsWith("/api/Prom/") || // Додано для тестування без авторизації
                path.StartsWith("/api/prom/"))   // Додано з нижнім регістром для різних варіантів URL
            {
                _logger.LogInformation("Skipping auth validation for path: {Path}", path);
                await _next(context);
                return;
            }

            // Skip validation for preflight CORS requests
            if (context.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Логуємо всі заголовки для діагностики
            _logger.LogInformation($"Processing auth request for path: {path}");
            foreach (var header in context.Request.Headers)
            {
                _logger.LogInformation($"Header: {header.Key}: {header.Value}");
            }

            // Get the JWT token from the request header
            string? authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                // Check if token is in a cookie
                if (!context.Request.Cookies.TryGetValue("jwt", out string? tokenFromCookie) || string.IsNullOrEmpty(tokenFromCookie))
                {
                    _logger.LogWarning("Authorization header or cookie is missing or invalid");
                    
                    // Перевіряємо всі cookies
                    if (context.Request.Cookies.Count > 0)
                    {
                        _logger.LogInformation("Available cookies:");
                        foreach (var cookie in context.Request.Cookies)
                        {
                            _logger.LogInformation($"Cookie: {cookie.Key}: {cookie.Value.Substring(0, Math.Min(10, cookie.Value.Length))}...");
                        }
                    }
                    
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Missing or invalid token" });
                    return;
                }

                _logger.LogInformation($"Using token from cookie, starts with: {tokenFromCookie.Substring(0, Math.Min(10, tokenFromCookie.Length))}...");
                authHeader = $"Bearer {tokenFromCookie}";
            }
            else
            {
                _logger.LogInformation("Using token from Authorization header");
            }

            // Extract token
            string token = authHeader.Substring("Bearer ".Length).Trim();

            // Validate the token through Auth service
            try
            {
                _logger.LogInformation("Starting token validation");
                if (!await authService.ValidateTokenAsync(token))
                {
                    _logger.LogWarning("Token validation failed");
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Invalid token" });
                    return;
                }

                _logger.LogInformation("Token validation successful");
                
                // If token is valid, add user info to the context items
                var userInfo = await authService.GetUserInfoFromTokenAsync(token);
                context.Items["UserInfo"] = userInfo;
                _logger.LogInformation($"Added user info to context: {userInfo.Email}");

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during token validation");
                context.Response.StatusCode = 500; // Internal Server Error
                await context.Response.WriteAsJsonAsync(new { error = "An error occurred during authentication" });
            }
        }
    }

    // Extension method to add the middleware to the HTTP request pipeline
    public static class AuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthMiddleware>();
        }
    }
} 