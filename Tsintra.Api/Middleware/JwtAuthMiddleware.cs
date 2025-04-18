using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Tsintra.Api.Services;

namespace Tsintra.Api.Middleware
{
    public class JwtAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtAuthMiddleware> _logger;

        public JwtAuthMiddleware(RequestDelegate next, ILogger<JwtAuthMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IApiAuthService authService)
        {
            try
            {
                // Перевіряємо наявність JWT токену в cookie
                context.Request.Cookies.TryGetValue("jwt", out string token);

                // Якщо токен не знайдено, перевіряємо заголовок Authorization
                if (string.IsNullOrEmpty(token) && context.Request.Headers.ContainsKey("Authorization"))
                {
                    var header = context.Request.Headers["Authorization"].ToString();
                    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = header.Substring(7);
                    }
                }

                if (!string.IsNullOrEmpty(token))
                {
                    // Перевіряємо токен через Auth API
                    var isValid = await authService.ValidateTokenAsync(token);

                    if (isValid)
                    {
                        // Отримуємо інформацію про користувача
                        var user = await authService.GetCurrentUserAsync(token);

                        if (user != null)
                        {
                            // Створюємо ClaimsIdentity на основі інформації з Auth API
                            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
                                new Claim(ClaimTypes.Email, user.Email ?? "")
                            };

                            var identity = new ClaimsIdentity(claims, "AuthApiScheme");
                            context.User = new ClaimsPrincipal(identity);

                            _logger.LogInformation("User authenticated via Auth API: {UserId}", user.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Invalid JWT token received from client");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing JWT token in middleware");
            }

            // Продовжуємо обробку запиту незалежно від результату автентифікації
            await _next(context);
        }
    }

    // Extension method для зручного додавання middleware
    public static class JwtAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtAuthMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtAuthMiddleware>();
        }
    }
} 