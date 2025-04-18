using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<User?> GetByEmailAsync(string email); // Might be useful
    Task<User?> GetByIdAsync(Guid id);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user); // For updating last login time etc.
} 