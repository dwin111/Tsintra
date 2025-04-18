using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Interfaces
{
    public interface IMarketplaceProduct
    {
        string ExternalId { get; set; }
        string Name { get; set; }
        string? Sku { get; set; }
        decimal Price { get; set; }
        string? Currency { get; set; }
        string? Description { get; set; }
        string? MainImage { get; set; }
        List<string>? Images { get; set; }
        string? Status { get; set; }
        int? QuantityInStock { get; set; }
        bool InStock { get; set; }
        DateTime? DateModified { get; set; }
        Dictionary<string, string>? NameMultilang { get; set; }
        Dictionary<string, string>? DescriptionMultilang { get; set; }
    }
} 