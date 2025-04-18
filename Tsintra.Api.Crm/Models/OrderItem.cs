using System;
using System.Text.Json.Serialization;

namespace Tsintra.Api.Crm.Models
{
    /// <summary>
    /// Позиція замовлення
    /// </summary>
    public class OrderItem
    {
        /// <summary>
        /// Ідентифікатор позиції
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Ідентифікатор товару
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Назва товару
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Ідентифікатор на маркетплейсі
        /// </summary>
        public string? MarketplaceProductId { get; set; }

        /// <summary>
        /// Артикул товару
        /// </summary>
        public string? Sku { get; set; }

        /// <summary>
        /// Кількість
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Ціна за одиницю
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Загальна вартість позиції
        /// </summary>
        [JsonIgnore]
        public decimal TotalPrice => UnitPrice * Quantity;

        /// <summary>
        /// Знижка
        /// </summary>
        public decimal? Discount { get; set; }

        /// <summary>
        /// URL зображення товару
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Службова інформація
        /// </summary>
        public string? Notes { get; set; }
    }
} 