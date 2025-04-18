namespace Tsintra.MarketplaceAgent.DTOs
{
    /// <summary>
    /// Input for the Deep Market Analysis tool.
    /// </summary>
    public class MarketAnalysisInput
    {
        /// <summary>
        /// The initial product title (e.g., from vision analysis).
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// The description of the scene/context from vision analysis.
        /// </summary>
        public string? SceneDescription { get; set; }

        /// <summary>
        /// JSON string containing scraped data from relevant web pages (output of WebScraperTool).
        /// Включає повну інформацію про товари конкурентів, ціни, характеристики, які допоможуть визначити тип товару.
        /// </summary>
        public string? ScrapedWebDataJson { get; set; } // Renamed from ImageSearchResultsJson

        /// <summary>
        /// Target language for analysis and output. Мова, на якій буде основний опис (ukr або rus).
        /// </summary>
        public string Language { get; set; } = "ukr";

        /// <summary>
        /// Target currency for price analysis.
        /// </summary>
        public string TargetCurrency { get; set; } = "UAH";

        /// <summary>
        /// Опис товару, якщо він вже відомий з попередніх етапів аналізу
        /// </summary>
        public string? ProductDescription { get; set; }

        /// <summary>
        /// Цільова аудиторія товару
        /// </summary>
        public string? TargetAudience { get; set; }

        /// <summary>
        /// Ключові слова для товару
        /// </summary>
        public List<string>? Keywords { get; set; }

        /// <summary>
        /// Довідкова інформація про конкурентів
        /// </summary>
        public string? Competitors { get; set; }

        /// <summary>
        /// Підказки від користувача щодо товару
        /// </summary>
        public string? UserHints { get; set; }
    }
} 