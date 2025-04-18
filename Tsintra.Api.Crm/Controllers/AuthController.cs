using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Tsintra.Api.Crm.Services;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Verifies if the user is authenticated
        /// </summary>
        [HttpGet("verify")]
        [Authorize]
        public IActionResult VerifyAuthentication()
        {
            var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
            if (userInfo == null)
            {
                return Ok(new { authenticated = false });
            }

            return Ok(new 
            { 
                authenticated = true,
                user = new
                {
                    id = userInfo.Id,
                    email = userInfo.Email,
                    firstName = userInfo.FirstName,
                    lastName = userInfo.LastName,
                    profilePictureUrl = userInfo.ProfilePictureUrl
                }
            });
        }

        /// <summary>
        /// Gets the current user information
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            var userInfo = HttpContext.Items["UserInfo"] as UserInfo;
            if (userInfo == null)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                id = userInfo.Id,
                email = userInfo.Email,
                firstName = userInfo.FirstName,
                lastName = userInfo.LastName,
                profilePictureUrl = userInfo.ProfilePictureUrl
            });
        }

        /// <summary>
        /// Revokes the current user's token
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            // Get token from authorization header or cookie
            string? token = null;
            
            // Try to get from header
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }
            // Try to get from cookie
            else if (HttpContext.Request.Cookies.TryGetValue("jwt", out string? tokenFromCookie))
            {
                token = tokenFromCookie;
            }

            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { error = "No token found" });
            }

            var result = await _authService.RevokeTokenAsync(token);
            if (!result)
            {
                return StatusCode(500, new { error = "Failed to revoke token" });
            }

            // Clear jwt cookie
            Response.Cookies.Delete("jwt");

            return Ok(new { message = "Logged out successfully" });
        }
    }
} 