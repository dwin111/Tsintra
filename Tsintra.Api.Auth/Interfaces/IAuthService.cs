using System.Security.Claims;
using Tsintra.Domain.Models;

namespace Tsintra.Api.Auth.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Processes the user information obtained from Google after successful authentication.
    /// Finds an existing user by Google ID or creates a new one.
    /// </summary>
    /// <param name="principal">The ClaimsPrincipal containing user claims from Google.</param>
    /// <returns>The application's User object corresponding to the authenticated Google user.</returns>
    Task<User> ProcessGoogleLoginAsync(ClaimsPrincipal principal);
} 