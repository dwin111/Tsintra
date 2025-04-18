using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<Product> GetByIdAsync(Guid id);
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Guid> CreateAsync(Product product);
        Task UpdateAsync(Product product);
        Task DeleteAsync(Guid id);
        Task<Product?> GetByMarketplaceIdAsync(string marketplaceId, string marketplaceType);
        Task<IEnumerable<Product>> GetByMarketplaceTypeAsync(string marketplaceType);
        Task<IEnumerable<Product>> GetVariantsAsync(Guid productId);
        Task<bool> UpdateStockAsync(Guid productId, int quantity);
        Task<bool> UpdatePriceAsync(Guid productId, decimal price);
        Task<IEnumerable<Product>> SearchAsync(string searchTerm);
        Task<IEnumerable<ProductDescriptionHistory>> GetProductHistoryAsync(int productId);
        Task<IEnumerable<string>> GetProductHashtagsAsync(int productId);
        Task<IEnumerable<string>> GetProductCTAsAsync(int productId);
        Task SaveProductDescriptionHistoryAsync(int productId, string description);
        Task SaveProductHashtagsAsync(int productId, IEnumerable<string> hashtags);
        Task SaveProductCTAsAsync(int productId, IEnumerable<string> ctas);
    }

    public class ProductDescriptionHistory
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
} 