using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Tsintra.Api.Controllers
{
    [ApiController]
    [Route("api/auth-proxy")]
    public class AuthProxyController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthProxyController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Перенаправляє на сторінку логіну Auth API
        /// </summary>
        [HttpGet("login")]
        public IActionResult RedirectToLogin(string returnUrl = "/")
        {
            string authApiBaseUrl = _configuration["AuthApi:BaseUrl"] ?? "https://localhost:7175";
            string loginUrl = $"{authApiBaseUrl}/api/auth/login/google?returnUrl={Uri.EscapeDataString(returnUrl)}";
            
            return Redirect(loginUrl);
        }
        
        /// <summary>
        /// Перенаправляє на logout в Auth API
        /// </summary>
        [HttpGet("logout")]
        public IActionResult RedirectToLogout()
        {
            string authApiBaseUrl = _configuration["AuthApi:BaseUrl"] ?? "https://localhost:7175";
            string logoutUrl = $"{authApiBaseUrl}/api/auth/logout";
            
            // Додатково очищаємо JWT cookie в головному API
            Response.Cookies.Delete("jwt");
            
            return Redirect(logoutUrl);
        }
        
        /// <summary>
        /// Перевіряє стан авторизації користувача
        /// </summary>
        [HttpGet("check-auth")]
        public IActionResult CheckAuth()
        {
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            
            return Ok(new { 
                isAuthenticated = isAuthenticated,
                userName = isAuthenticated ? User.Identity.Name : null,
                userId = isAuthenticated ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null
            });
        }
    }
} 