using System.ComponentModel.DataAnnotations;

namespace Tsintra.Domain.DTOs
{
    /// <summary>
    /// Спрощений DTO для створення продукту без варіантів
    /// </summary>
    public class CreateSimpleProductDto
    {
        // Базові властивості
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Sku { get; set; } = string.Empty;
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Ціна повинна бути більше 0")]
        public decimal Price { get; set; }
        
        public string? Description { get; set; }
        
        public string? MainImage { get; set; }
        
        public string? CategoryName { get; set; }
        
        public int? QuantityInStock { get; set; }
    }
} 