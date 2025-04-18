using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Tsintra.Api.Auth.Interfaces;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;


namespace Tsintra.Api.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository userRepository, ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<User> ProcessGoogleLoginAsync(ClaimsPrincipal principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("ProcessGoogleLoginAsync called with unauthenticated principal.");
            throw new ArgumentException("User is not authenticated.", nameof(principal));
        }

        var googleId = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var firstName = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
        var lastName = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value;
        var pictureUrl = principal.Claims.FirstOrDefault(c => c.Type == "urn:google:picture")?.Value;

        if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
        {
            _logger.LogError("Required claims (NameIdentifier, Email) not found for authenticated user.");
            throw new InvalidOperationException("Required claims (NameIdentifier, Email) not found.");
        }

        _logger.LogInformation("Processing Google login for GoogleId: {GoogleId}, Email: {Email}", googleId, email);

        // Спочатку шукаємо за GoogleId
        var user = await _userRepository.GetByGoogleIdAsync(googleId);
        
        // Якщо не знайшли за GoogleId, шукаємо за Email
        if (user == null)
        {
            _logger.LogInformation("User with GoogleId {GoogleId} not found. Checking by email {Email}.", googleId, email);
            user = await _userRepository.GetByEmailAsync(email);
        }

        // Тільки якщо користувача немає взагалі - створюємо нового
        if (user == null)
        {
            _logger.LogInformation("User not found by GoogleId or Email. Creating new user with email: {Email}", email);
            user = new User
            {
                GoogleId = googleId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                ProfilePictureUrl = pictureUrl,
                LastLoginAt = DateTime.UtcNow
            };
            try
            {
                user = await _userRepository.CreateAsync(user);
                _logger.LogInformation("Successfully created new user with ID {UserId}", user.Id);
            }
            catch(Exception ex)
            {
                 _logger.LogError(ex, "Failed to create user for GoogleId {GoogleId}", googleId);
                 throw;
            }
        }
        else
        {
            _logger.LogInformation("Found existing user with ID {UserId}. Updating details if needed.", user.Id);
            
            // Оновлюємо GoogleId, якщо знайшли за email але GoogleId відсутній
            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = googleId;
                _logger.LogInformation("Updated GoogleId for user {UserId}", user.Id);
            }
            
            bool needsUpdate = false;
            if (user.FirstName != firstName) { user.FirstName = firstName; needsUpdate = true; }
            if (user.LastName != lastName) { user.LastName = lastName; needsUpdate = true; }
            if (user.ProfilePictureUrl != pictureUrl) { user.ProfilePictureUrl = pictureUrl; needsUpdate = true; }
            if (user.Email != email) { user.Email = email; needsUpdate = true; } 

            user.LastLoginAt = DateTime.UtcNow;
            needsUpdate = true;
            
            if (needsUpdate)
            {
                await _userRepository.UpdateAsync(user);
                _logger.LogInformation("Updated user information for {UserId}", user.Id);
            }
        }

        return user;
    }

    public User? GetCurrentUser(ClaimsPrincipal principal)
    {
        try
        {
            if (principal == null) return null;

            var id = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var role = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            var picture = principal.Claims.FirstOrDefault(c => c.Type == "picture")?.Value;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(email))
            {
                return null;
            }

            return new User
            {
                Id = Guid.TryParse(id, out var userId) ? userId : Guid.Empty,
                Email = email ?? string.Empty,
                FirstName = name,
                ProfilePictureUrl = picture
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return null;
        }
    }
} 