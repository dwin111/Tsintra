using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Tsintra.Domain.DTOs;
using Tsintra.MarketplaceAgent.Tools.AI; // Додаємо для використання класів MultiLanguageText та MultiLanguageKeywords

namespace Tsintra.MarketplaceAgent.DTOs
{
    /// <summary>
    /// Extended version of ProductDetailsDto with additional properties required by the MarketplaceAgent
    /// </summary>
    public class MarketplaceProductDetailsDto : ProductDetailsDto
    {
        // Additional properties needed by the MarketplaceAgent
        [JsonPropertyName("refinedDescription")]
        public string? RefinedDescription { get; set; }
        
        [JsonPropertyName("keywords")]
        public List<string>? Keywords { get; set; }
        
        [JsonPropertyName("currency")]
        public string? Currency { get; set; }
        
        [JsonPropertyName("recommendedPrice")]
        public decimal? RecommendedPrice { get; set; }
        
        [JsonPropertyName("instagramCaption")]
        public string? InstagramCaption { get; set; }
        
        // Властивості для багатомовної підтримки
        [JsonPropertyName("nameMultilang")]
        public MultiLanguageText? NameMultilang { get; set; }
        
        [JsonPropertyName("descriptionMultilang")]
        public MultiLanguageText? DescriptionMultilang { get; set; }
        
        [JsonPropertyName("keywordsMultilang")]
        public MultiLanguageKeywords? KeywordsMultilang { get; set; }
        
        // Поля для SEO-оптимізації
        [JsonPropertyName("metaTitle")]
        public string? MetaTitle { get; set; }
        
        [JsonPropertyName("metaDescription")]
        public string? MetaDescription { get; set; }
        
        [JsonPropertyName("seoUrl")]
        public string? SeoUrl { get; set; }
        
        // Додаткові поля для форми Prom.ua
        [JsonPropertyName("tagsString")]
        public string? TagsString { get; set; }
        
        [JsonPropertyName("tagsMultilang")]
        public Dictionary<string, string>? TagsMultilang { get; set; }
        
        [JsonPropertyName("measureUnit")]
        public string? MeasureUnit { get; set; }
        
        [JsonPropertyName("availability")]
        public string? Availability { get; set; }
        
        [JsonPropertyName("minimumOrderQuantity")]
        public int? MinimumOrderQuantity { get; set; }
        
        // Габаритні розміри
        [JsonPropertyName("width")]
        public int? Width { get; set; }
        
        [JsonPropertyName("height")]
        public int? Height { get; set; }
        
        [JsonPropertyName("length")]
        public int? Length { get; set; }
        
        [JsonPropertyName("weight")]
        public decimal? Weight { get; set; }

        /// <summary>
        /// Creates a MarketplaceProductDetailsDto from a standard ProductDetailsDto
        /// </summary>
        public static MarketplaceProductDetailsDto FromProductDetailsDto(ProductDetailsDto dto)
        {
            return new MarketplaceProductDetailsDto
            {
                RefinedTitle = dto.RefinedTitle,
                Description = dto.Description,
                Price = dto.Price,
                Images = dto.Images,
                Attributes = dto.Attributes,
                Category = dto.Category,
                Tags = dto.Tags,
                
                // Set extended properties with defaults
                RefinedDescription = dto.Description,
                Keywords = dto.Tags,
                Currency = "UAH", // Default currency
                RecommendedPrice = dto.Price ?? 0m,
                InstagramCaption = null,
                
                // Ініціалізуємо словники для багатомовності
                NameMultilang = new MultiLanguageText(),
                DescriptionMultilang = new MultiLanguageText(),
                KeywordsMultilang = new MultiLanguageKeywords(),
                
                // Ініціалізуємо SEO поля
                MetaTitle = dto.RefinedTitle,
                MetaDescription = dto.Description?.Length > 160 
                    ? dto.Description.Substring(0, 157) + "..." 
                    : dto.Description,
                SeoUrl = GenerateSeoUrl(dto.RefinedTitle),
                
                // Ініціалізуємо додаткові поля для Prom.ua
                TagsString = dto.Tags != null ? string.Join(", ", dto.Tags) : "",
                TagsMultilang = new Dictionary<string, string>(), // Буде заповнено пізніше
                MeasureUnit = "шт.", // За замовчуванням
                Availability = "в наявності", // За замовчуванням
                MinimumOrderQuantity = 1, // За замовчуванням
                
                // Ініціалізуємо габаритні розміри
                Width = null,
                Height = null,
                Length = null,
                Weight = null
            };
        }

        /// <summary>
        /// Генерує SEO-URL з назви товару
        /// </summary>
        private static string? GenerateSeoUrl(string? title)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            // Словник транслітерації українських символів
            var ukrainianToLatin = new Dictionary<char, string>
            {
                {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "h"}, {'ґ', "g"}, {'д', "d"},
                {'е', "e"}, {'є', "ie"}, {'ж', "zh"}, {'з', "z"}, {'и', "y"}, {'і', "i"},
                {'ї', "i"}, {'й', "i"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"},
                {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"},
                {'ф', "f"}, {'х', "kh"}, {'ц', "ts"}, {'ч', "ch"}, {'ш', "sh"}, {'щ', "shch"},
                {'ь', ""}, {'ю', "iu"}, {'я', "ia"},
                {'А', "a"}, {'Б', "b"}, {'В', "v"}, {'Г', "h"}, {'Ґ', "g"}, {'Д', "d"},
                {'Е', "e"}, {'Є', "ie"}, {'Ж', "zh"}, {'З', "z"}, {'И', "y"}, {'І', "i"},
                {'Ї', "i"}, {'Й', "i"}, {'К', "k"}, {'Л', "l"}, {'М', "m"}, {'Н', "n"},
                {'О', "o"}, {'П', "p"}, {'Р', "r"}, {'С', "s"}, {'Т', "t"}, {'У', "u"},
                {'Ф', "f"}, {'Х', "kh"}, {'Ц', "ts"}, {'Ч', "ch"}, {'Ш', "sh"}, {'Щ', "shch"},
                {'Ь', ""}, {'Ю', "iu"}, {'Я', "ia"}
            };

            // Транслітерація
            var transliterated = new StringBuilder();
            foreach (char c in title.ToLower())
            {
                if (ukrainianToLatin.TryGetValue(c, out string? latinChar))
                {
                    transliterated.Append(latinChar);
                }
                else if (c == ' ')
                {
                    transliterated.Append('-');
                }
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')
                {
                    transliterated.Append(c);
                }
                // інші символи ігноруємо
            }

            // Конвертуємо до рядка
            string seoUrl = transliterated.ToString();
            
            // Видаляємо подвійні дефіси
            while (seoUrl.Contains("--"))
            {
                seoUrl = seoUrl.Replace("--", "-");
            }
            
            // Обрізаємо дефіси на початку і в кінці
            return seoUrl.Trim('-');
        }

        /// <summary>
        /// Метод для конвертації MultiLanguageText у Dictionary для сумісності з API
        /// </summary>
        /// <param name="text">Об'єкт MultiLanguageText для конвертації</param>
        /// <returns>Словник з мовними кодами та текстами</returns>
        public Dictionary<string, string> ToNameMultilangDictionary()
        {
            var dict = new Dictionary<string, string>();
            if (NameMultilang != null)
            {
                dict["uk"] = NameMultilang.Uk;
                dict["ru"] = NameMultilang.Ru;
            }
            return dict;
        }
        
        /// <summary>
        /// Метод для конвертації MultiLanguageText у Dictionary для сумісності з API
        /// </summary>
        /// <param name="text">Об'єкт MultiLanguageText для конвертації</param>
        /// <returns>Словник з мовними кодами та текстами</returns>
        public Dictionary<string, string> ToDescriptionMultilangDictionary()
        {
            var dict = new Dictionary<string, string>();
            if (DescriptionMultilang != null)
            {
                dict["uk"] = DescriptionMultilang.Uk;
                dict["ru"] = DescriptionMultilang.Ru;
            }
            return dict;
        }
        
        /// <summary>
        /// Метод для конвертації MultiLanguageKeywords у Dictionary для сумісності з API
        /// </summary>
        /// <returns>Словник з мовними кодами та списками ключових слів</returns>
        public Dictionary<string, List<string>> ToKeywordsMultilangDictionary()
        {
            var dict = new Dictionary<string, List<string>>();
            if (KeywordsMultilang != null)
            {
                dict["uk"] = KeywordsMultilang.Uk;
                dict["ru"] = KeywordsMultilang.Ru;
            }
            return dict;
        }
    }
} 