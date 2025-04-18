using Tsintra.Domain.DTOs; // Correct namespace
using Tsintra.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tsintra.Domain.Interfaces
{
    public interface IMarketplaceClient
    {
        // Methods using DTOs from Tsintra.Domain.DTOs
        Task<PublishResultDto> PublishProductAsync(ProductDetailsDto productDetails, CancellationToken cancellationToken = default);

        // Methods for product management
        Task<IEnumerable<MarketplaceProduct>> GetProductsAsync(CancellationToken ct = default);
        Task<MarketplaceProduct?> GetProductByIdAsync(string productId);
        Task<MarketplaceProduct> AddProductAsync(MarketplaceProduct product, CancellationToken ct = default);
        Task<MarketplaceProduct> UpdateProductAsync(MarketplaceProduct product, CancellationToken ct = default);
        Task DeleteProductAsync(string productId, CancellationToken ct = default);
    }
} 