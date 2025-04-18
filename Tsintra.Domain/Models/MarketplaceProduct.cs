using System;
using System.Collections.Generic;

namespace Tsintra.Core.Models
{
    public record MarketplaceProduct(
        string Id,
        string Name,
        decimal Price,
        string Description,
        Dictionary<string, object> SpecificAttributes
    );
} 