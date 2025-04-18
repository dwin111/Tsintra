namespace Tsintra.MarketplaceAgent.DTOs
{
    // DTO для вхідних даних інструменту InstagramCaptionTool
    public record CaptionInput(
        string ProductJson, 
        string MarketAnalysisJson, 
        string AudienceJson, 
        string RefinedJson, 
        string Language
    );
} 