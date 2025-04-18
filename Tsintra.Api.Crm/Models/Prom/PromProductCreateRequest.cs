using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Tsintra.Api.Crm.Models.Prom
{
    /// <summary>
    /// Модель запиту для створення або оновлення товару на Prom.ua
    /// </summary>
    public class PromProductRequest
    {
        /// <summary>
        /// Інформація про товар
        /// </summary>
        [JsonPropertyName("product")]
        public PromProductData Product { get; set; }
    }

    /// <summary>
    /// Дані товару для створення або оновлення на Prom.ua
    /// </summary>
    public class PromProductData
    {
        /// <summary>
        /// ID товару (потрібно для оновлення)
        /// </summary>
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        /// <summary>
        /// Назва товару
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Опис товару
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Ціна товару
        /// </summary>
        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        /// <summary>
        /// Валюта (UAH, USD, EUR)
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "UAH";

        /// <summary>
        /// Артикул товару
        /// </summary>
        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        /// <summary>
        /// Кількість товару на складі
        /// </summary>
        [JsonPropertyName("quantity_in_stock")]
        public int? QuantityInStock { get; set; }

        /// <summary>
        /// Ключові слова для пошуку
        /// </summary>
        [JsonPropertyName("keywords")]
        public string Keywords { get; set; }

        /// <summary>
        /// Наявність товару на складі (доступні значення: available, not_available, under_the_order)
        /// </summary>
        [JsonPropertyName("presence")]
        public string Presence { get; set; }

        /// <summary>
        /// ID групи товару
        /// </summary>
        [JsonPropertyName("group_id")]
        public long? GroupId { get; set; }

        /// <summary>
        /// Статус товару: on_display (відображається), draft (чернетка), delete (видалений)
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }

        /// <summary>
        /// Назва товару на різних мовах
        /// </summary>
        [JsonPropertyName("name_multilang")]
        public Dictionary<string, string> NameMultilang { get; set; }

        /// <summary>
        /// Опис товару на різних мовах
        /// </summary>
        [JsonPropertyName("description_multilang")]
        public Dictionary<string, string> DescriptionMultilang { get; set; }

        /// <summary>
        /// Посилання на зображення
        /// </summary>
        [JsonPropertyName("images")]
        public List<string> Images { get; set; }

        /// <summary>
        /// Посилання на головне зображення
        /// </summary>
        [JsonPropertyName("main_image")]
        public string MainImage { get; set; }

        /// <summary>
        /// Одиниця виміру товару
        /// </summary>
        [JsonPropertyName("measure_unit")]
        public string MeasureUnit { get; set; }

        /// <summary>
        /// Знижка на товар у відсотках
        /// </summary>
        [JsonPropertyName("discount")]
        public decimal? Discount { get; set; }

        /// <summary>
        /// Мінімальна кількість для замовлення
        /// </summary>
        [JsonPropertyName("minimum_order_quantity")]
        public int? MinimumOrderQuantity { get; set; }

        /// <summary>
        /// ID категорії товару
        /// </summary>
        [JsonPropertyName("category_id")]
        public long? CategoryId { get; set; }

        /// <summary>
        /// Чи товар є варіацією
        /// </summary>
        [JsonPropertyName("is_variation")]
        public bool? IsVariation { get; set; }

        /// <summary>
        /// ID базового товару для варіацій
        /// </summary>
        [JsonPropertyName("variation_base_id")]
        public long? VariationBaseId { get; set; }

        /// <summary>
        /// ID групи варіацій
        /// </summary>
        [JsonPropertyName("variation_group_id")]
        public long? VariationGroupId { get; set; }

        /// <summary>
        /// Зовнішній ID товару
        /// </summary>
        [JsonPropertyName("external_id")]
        public string ExternalId { get; set; }
    }
} 