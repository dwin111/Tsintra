using System.Collections.Generic;

namespace Tsintra.MarketplaceAgent.DTOs
{
    /// <summary>
    /// Represents the input for the BestVisionPipelineTool.
    /// </summary>
    public record VisionPipelineInput(
        List<string> ImageCacheKeys, 
        string Language,
        string Hints
    );
} 