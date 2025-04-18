using Tsintra.Domain.Models;

namespace Tsintra.MarketplaceAgent.Interfaces
{
    public interface IProductDescriptionAgent
    {
        Task<string> GenerateDescriptionAsync(Product product, string? userPreferences = null);
        Task<string> RefineDescriptionAsync(string currentDescription, string userFeedback);
        Task<string> GenerateHashtagsAsync(Product product);
        Task<string> GenerateCallToActionAsync(Product product);
    }
} 