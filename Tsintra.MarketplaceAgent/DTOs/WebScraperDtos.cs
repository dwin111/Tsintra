using System.Collections.Generic;

namespace Tsintra.MarketplaceAgent.DTOs;

public class ScrapedPageInfo
{
    public string Url { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; } 
    public string? Price { get; set; } // Price is often text, hard to parse reliably to decimal
    public string? ImageUrl { get; set; } // e.g., og:image
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class WebScraperOutput
{
    public List<ScrapedPageInfo> ScrapedData { get; set; } = new();
} 