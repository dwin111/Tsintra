using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface IProductDescriptionGenerator
    {
        string GenerateInstagramDescription(Product product);
        Task<string> GenerateAIDescriptionAsync(Product product, string? userPreferences = null);
        Task<string> RefineDescriptionAsync(string currentDescription, string userFeedback);
    }
} 