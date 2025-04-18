using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Tsintra.MarketplaceAgent.DTOs;

/// <summary>
/// Вхідні дані для інструменту веб-скрапінгу
/// </summary>
public class WebScraperInput
{
    /// <summary>
    /// Список URL для скрапінгу
    /// </summary>
    [JsonPropertyName("urls")]
    public List<string> Urls { get; set; } = new List<string>();

    /// <summary>
    /// Шаблони для витягування даних (ключ: назва даних, значення: XPath або селектор)
    /// </summary>
    [JsonPropertyName("extract_patterns")]
    public Dictionary<string, string> ExtractPatterns { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Максимальна кількість сторінок для скрапінгу (для запобігання перевантаження)
    /// </summary>
    public int MaxPages { get; set; } = 5;

    /// <summary>
    /// Тип товару для аналізу (для кращого розуміння контексту ШІ)
    /// </summary>
    public string? ProductType { get; set; }

    /// <summary>
    /// Конструктор з параметрами
    /// </summary>
    public WebScraperInput(List<string> urls, Dictionary<string, string>? extractPatterns = null)
    {
        Urls = urls ?? new List<string>();
        ExtractPatterns = extractPatterns ?? new Dictionary<string, string>
        {
            ["title"] = "//h1",
            ["price"] = "//span[contains(@class,'price')]",
            ["description"] = "//div[contains(@class,'description')]",
            ["images"] = "//div[contains(@class,'gallery')]//img/@src"
        };
    }

    /// <summary>
    /// Порожній конструктор для серіалізації
    /// </summary>
    public WebScraperInput()
    {
    }
} 