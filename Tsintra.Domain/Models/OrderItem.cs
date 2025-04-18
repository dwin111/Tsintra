using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models
{
    /// <summary>
    /// Модель позиції товару в замовленні
    /// </summary>
    public class OrderItem
    {
        /// <summary>
        /// Унікальний ідентифікатор позиції замовлення
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Ідентифікатор замовлення
        /// </summary>
        public Guid OrderId { get; set; }

        /// <summary>
        /// Ідентифікатор товару
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// Артикул товару
        /// </summary>
        public string ProductSku { get; set; }

        /// <summary>
        /// Зовнішній ідентифікатор товару
        /// </summary>
        public string ExternalId { get; set; }

        /// <summary>
        /// Назва товару
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Кількість товару
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Ціна за одиницю товару
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Загальна вартість позиції замовлення
        /// </summary>
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Валюта
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// URL зображення товару
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// URL сторінки товару
        /// </summary>
        public string ProductUrl { get; set; }

        /// <summary>
        /// Специфічні дані товару для різних маркетплейсів
        /// </summary>
        public Dictionary<string, object> SpecificData { get; set; }
        
        /// <summary>
        /// Зв'язок з товаром
        /// </summary>
        public Product Product { get; set; }
        
        /// <summary>
        /// Зв'язок з варіантом товару
        /// </summary>
        public ProductVariant ProductVariant { get; set; }
    }
} 