using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Google.Apis.Auth;
using Tsintra.Api.Auth.Interfaces;

namespace Tsintra.Api.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly Tsintra.Api.Auth.Interfaces.IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger,
        IUserRepository userRepository,
        Tsintra.Api.Auth.Interfaces.IJwtTokenService jwtTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IConfiguration configuration)
    {
        _authService = authService;
        _logger = logger;
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiates the Google login flow.
    /// The user will be redirected to Google for authentication.
    /// </summary>
    [HttpGet("login/google")]
    public IActionResult LoginWithGoogle(string? returnUrl = "/")
    {
        _logger.LogInformation("Attempting Google login. Return URL: {ReturnUrl}", returnUrl);
        
        var properties = new AuthenticationProperties 
        { 
            RedirectUri = Url.Action(nameof(GoogleCallback)),
            // Зберегти для перенаправлення після аутентифікації
            Items =
            {
                { "returnUrl", returnUrl },
                { "scheme", GoogleDefaults.AuthenticationScheme }
            },
            // Переконатися, що CORS не блокує редіректи
            AllowRefresh = true,
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
        };
        
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles the callback from Google after successful authentication.
    /// This endpoint is typically configured as the Redirect URI in Google Cloud Console
    /// and in the AddGoogle configuration in Program.cs (implicitly via CallbackPath).
    /// </summary>
    [HttpGet("google-callback")]
    //[ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GoogleCallback()
    {
        _logger.LogInformation("Received callback from Google.");

        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            _logger.LogWarning("Google authentication failed or principal not found after callback.");
            return RedirectToAction(nameof(LoginFailed), new { reason = "Authentication failed after callback." });
        }

        var userEmail = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email) ?? "[Email not found]";
        _logger.LogInformation("Authentication successful, processing user. User: {UserEmail}", userEmail);

        try
        {
            var user = await _authService.ProcessGoogleLoginAsync(authenticateResult.Principal);
            _logger.LogInformation("User processed successfully. User ID: {UserId}", user.Id);

            return Redirect("/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Google login after callback for {UserEmail}.", userEmail);
            return RedirectToAction(nameof(LoginFailed), new { reason = "Error processing login." });
        }
    }

    /// <summary>
    /// Authenticates a user using a Google ID token from a SPA client.
    /// This method validates the token, finds or creates a user, and returns a JWT in a secure cookie.
    /// </summary>
    [HttpPost("external-login")]
    public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginDto dto)
    {
        try
        {
            // Configure Google token validation settings
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["GoogleAuth:ClientId"] }
            };

            // Validate Google ID token
            var payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);

            // Спочатку шукаємо за GoogleId
            var user = await _userRepository.GetByGoogleIdAsync(payload.Subject);

            // Якщо не знайшли за GoogleId, шукаємо за Email
            if (user == null)
            {
                _logger.LogInformation("User with GoogleId {GoogleId} not found. Checking by email {Email}.", payload.Subject, payload.Email);
                user = await _userRepository.GetByEmailAsync(payload.Email);
            }

            if (user == null)
            {
                // Create new user with Google credentials
                user = new User
                {
                    Id = Guid.NewGuid(),
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName,
                    ProfilePictureUrl = payload.Picture,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                await _userRepository.CreateAsync(user);
                _logger.LogInformation("Created new user with Google external login: {UserId}, {Email}", user.Id, user.Email);
            }
            else
            {
                // Оновлюємо GoogleId, якщо знайшли за email але GoogleId відсутній
                if (string.IsNullOrEmpty(user.GoogleId))
                {
                    user.GoogleId = payload.Subject;
                    _logger.LogInformation("Updated GoogleId for user {UserId}", user.Id);
                }

                // Update user data if needed
                bool needsUpdate = false;

                if (user.FirstName != payload.GivenName) { user.FirstName = payload.GivenName; needsUpdate = true; }
                if (user.LastName != payload.FamilyName) { user.LastName = payload.FamilyName; needsUpdate = true; }
                if (user.ProfilePictureUrl != payload.Picture) { user.ProfilePictureUrl = payload.Picture; needsUpdate = true; }

                // Always update last login time
                user.LastLoginAt = DateTime.UtcNow;
                needsUpdate = true;

                if (needsUpdate)
                {
                    await _userRepository.UpdateAsync(user);
                }

                _logger.LogInformation("Existing user logged in with Google external login: {UserId}, {Email}", user.Id, user.Email);
            }

            // Generate JWT token
            var tokenResponse = await _jwtTokenService.GenerateTokenAsync(user);

            // Set JWT in a secure HttpOnly cookie
            Response.Cookies.Append("jwt", tokenResponse.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = tokenResponse.Expiration
            });

            // Return user info without including the token (since it's in the cookie)
            return Ok(new
            {
                userId = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                profilePictureUrl = user.ProfilePictureUrl
            });
        }
        catch (InvalidJwtException ex) when (ex.Message.Contains("Audience"))
        {
            _logger.LogWarning(ex, "External login failed: Invalid audience in Google token");
            return BadRequest(new { error = "Invalid token audience" });
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "External login failed: Invalid Google token");
            return BadRequest(new { error = "Invalid token" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during external login");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the currently authenticated user's information.
    /// </summary>
    [HttpGet("me")]
    //[Authorize]
    public async Task<ActionResult<User>> GetCurrentUser()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "[unknown]";
        _logger.LogInformation("Attempting to get current user information for {UserEmail}", userEmail);

        try
        {
            // Шукаємо користувача за email замість створення
            var user = await _userRepository.GetByEmailAsync(userEmail);

            if (user == null)
            {
                _logger.LogWarning("User not found for email {UserEmail}", userEmail);
                return NotFound("User data not found.");
            }

            // Оновлюємо час останнього входу
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("Successfully retrieved user information for {UserEmail}. User ID: {UserId}", userEmail, user.Id);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user information for {UserEmail}", userEmail);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user information.");
        }
    }

    /// <summary>
    /// Logs the current user out.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromQuery] string returnUrl = "/")
    {
        // Clear auth cookie
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Clear JWT cookie
        Response.Cookies.Delete("jwt", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None
        });

        // Clear any refresh tokens for this user
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
            {
                await _refreshTokenRepository.DeleteAllForUserAsync(userGuid);
                _logger.LogInformation("Cleared all refresh tokens for user {UserId} during logout", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing refresh tokens during logout");
        }

        _logger.LogInformation("User logged out successfully");

        // Перенаправляємо на головний API
        return Ok(new { success = true, message = "Logged out successfully" });
    }

    /// <summary>
    /// Endpoint to redirect to in case of login failure.
    /// </summary>
    [HttpGet("login-failed")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult LoginFailed(string? reason = null)
    {
        _logger.LogWarning("Login failed. Reason: {Reason}", reason ?? "No reason provided.");
        return BadRequest(new { message = "Google authentication failed.", reason = reason });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt with email: {Email}", request.Email);

            // In a real app, you would validate credentials against a database
            // For this example, we'll use a simplified approach
            var user = await _userRepository.GetByEmailAsync(request.Email);
            
            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found with email {Email}", request.Email);
                return Unauthorized("Invalid email or password");
            }

            // In a real app, you would validate the password hash
            // Here we're simplifying for demonstration purposes
            // var passwordValid = _passwordService.VerifyPassword(request.Password, user.PasswordHash);
            // if (!passwordValid) return Unauthorized("Invalid email or password");

            // Generate JWT token
            var tokenResponse = await _jwtTokenService.GenerateTokenAsync(user);
            
            // Set JWT in a secure HttpOnly cookie
            Response.Cookies.Append("jwt", tokenResponse.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = tokenResponse.Expiration
            });
            
            _logger.LogInformation("Login successful for user: {UserId}, {Email}", user.Id, user.Email);
            
            // Return user info without the token in the response
            return Ok(new
            {
                userId = user.Id,
                email = user.Email,
                firstName = user.FirstName, 
                lastName = user.LastName,
                profilePictureUrl = user.ProfilePictureUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login attempt for {Email}", request.Email);
            return StatusCode(500, "Internal server error during login");
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            _logger.LogInformation("Token refresh request received");
            
            if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.RefreshToken))
            {
                return BadRequest("Token and refresh token are required");
            }

            var tokenResponse = await _jwtTokenService.RefreshTokenAsync(request.Token, request.RefreshToken);
            
            if (tokenResponse == null)
            {
                _logger.LogWarning("Token refresh failed: Invalid token or refresh token");
                return Unauthorized("Invalid token or refresh token");
            }

            // Set the new JWT in a secure HttpOnly cookie
            Response.Cookies.Append("jwt", tokenResponse.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = tokenResponse.Expiration
            });

            _logger.LogInformation("Token refreshed successfully for user: {UserId}", tokenResponse.UserId);
            
            // Return only necessary info without tokens
            return Ok(new
            {
                userId = tokenResponse.UserId,
                email = tokenResponse.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, "Internal server error during token refresh");
        }
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Unauthorized("Invalid authentication");
            }

            _logger.LogInformation("Token revocation request for user: {UserId}", userGuid);

            // Get the refresh token
            var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);
            
            if (refreshToken == null || refreshToken.UserId != userGuid)
            {
                _logger.LogWarning("Token revocation failed: Invalid refresh token for user {UserId}", userGuid);
                return BadRequest("Invalid refresh token");
            }

            // Revoke the token
            await _refreshTokenRepository.RevokeAsync(refreshToken.Id);
            
            // Clear the JWT cookie
            Response.Cookies.Delete("jwt", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });

            _logger.LogInformation("Token successfully revoked for user: {UserId}", userGuid);
            return Ok(new { message = "Token revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token revocation");
            return StatusCode(500, "Internal server error during token revocation");
        }
    }

    [HttpPost("revoke-all")]
    [Authorize]
    public async Task<IActionResult> RevokeAllTokens()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Unauthorized("Invalid authentication");
            }

            _logger.LogInformation("Request to revoke all tokens for user: {UserId}", userGuid);

            // Revoke all tokens for the user
            await _refreshTokenRepository.RevokeAllUserTokensAsync(userGuid);
            
            // Clear the JWT cookie
            Response.Cookies.Delete("jwt", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });

            _logger.LogInformation("All tokens successfully revoked for user: {UserId}", userGuid);
            return Ok(new { message = "All tokens revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during revocation of all tokens");
            return StatusCode(500, "Internal server error during token revocation");
        }
    }

    [HttpGet("validate-token")]
    [Authorize]
    public IActionResult ValidateToken()
    {
        // Якщо запит дійшов сюди, значить токен валідний
        // (перевірка JWT відбувається через атрибут [Authorize])
        return Ok(new { isValid = true });
    }
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }
}

public class RefreshTokenRequest
{
    [Required]
    public string Token { get; set; }

    [Required]
    public string RefreshToken { get; set; }
}

public class RevokeTokenRequest
{
    [Required]
    public string RefreshToken { get; set; }
}

public class ExternalLoginDto
{
    [Required]
    public string IdToken { get; set; }
} 