using System.Collections.Generic;

namespace Tsintra.MarketplaceAgent.DTOs
{
    /// <summary>
    /// DTO for the input data required by RefineContentTool.
    /// </summary>
    public record RefineContentInput(
        string OriginalTitle,
        string MarketAnalysisJson,
        List<string> ImageCacheKeys,
        string TargetCurrency = "UAH" // Added TargetCurrency with a default value
    );
} 