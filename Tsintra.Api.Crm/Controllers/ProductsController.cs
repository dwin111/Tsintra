using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.Domain.DTOs;
using System.Linq;
using System.Security.Claims;
using Tsintra.Api.Crm.Services;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<ProductsController> _logger;
        private readonly ISharedMemoryService _sharedMemory;

        public ProductsController(
            IProductRepository productRepository, 
            ILogger<ProductsController> logger,
            ISharedMemoryService sharedMemory)
        {
            _productRepository = productRepository;
            _logger = logger;
            _sharedMemory = sharedMemory;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetAllProducts()
        {
            try
            {
                var products = await _productRepository.GetAllAsync();
                var productDtos = products.Select(MapToProductDto).ToList();
                return Ok(productDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> GetProduct(Guid id)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    return NotFound();
                }
                return Ok(MapToProductDto(product));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product with ID: {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductDto productDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var product = MapToProduct(productDto);
                var id = await _productRepository.CreateAsync(product);
                var createdProduct = await _productRepository.GetByIdAsync(id);
                var resultDto = MapToProductDto(createdProduct);
                
                // Зберігаємо продукт у спільній пам'яті для подальшого використання в чатах та інших сервісах
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    await _sharedMemory.StoreProductMemory(userId, resultDto);
                    _logger.LogInformation("Product {ProductId} stored in shared memory for user {UserId}", resultDto.Id, userId);
                }
                
                return CreatedAtAction(
                    nameof(GetProduct), 
                    new { id = resultDto.Id }, 
                    resultDto
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product: {Error}", ex.Message);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("simple")]
        public async Task<ActionResult<ProductDto>> CreateSimpleProduct([FromBody] CreateSimpleProductDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Конвертуємо спрощений DTO у повну модель продукту
                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = dto.Name,
                    Sku = dto.Sku,
                    Price = dto.Price,
                    Description = dto.Description,
                    MainImage = dto.MainImage,
                    CategoryName = dto.CategoryName,
                    QuantityInStock = dto.QuantityInStock,
                    InStock = dto.QuantityInStock.HasValue && dto.QuantityInStock.Value > 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Status = "active"
                };
                
                var id = await _productRepository.CreateAsync(product);
                var createdProduct = await _productRepository.GetByIdAsync(id);
                var resultDto = MapToProductDto(createdProduct);
                
                // Зберігаємо продукт у спільній пам'яті для подальшого використання в чатах та інших сервісах
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    await _sharedMemory.StoreProductMemory(userId, resultDto);
                    _logger.LogInformation("Simple product {ProductId} stored in shared memory for user {UserId}", resultDto.Id, userId);
                }
                
                return CreatedAtAction(
                    nameof(GetProduct), 
                    new { id = resultDto.Id }, 
                    resultDto
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating simple product: {Error}", ex.Message);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] CreateProductDto productDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return NotFound();
                }

                // Оновлюємо існуючий продукт даними з DTO
                UpdateProductFromDto(existingProduct, productDto);
                
                await _productRepository.UpdateAsync(existingProduct);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID: {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            try
            {
                await _productRepository.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product with ID: {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("marketplace/{marketplaceId}/{marketplaceType}")]
        public async Task<ActionResult<Product>> GetProductByMarketplace(string marketplaceId, string marketplaceType)
        {
            try
            {
                var product = await _productRepository.GetByMarketplaceIdAsync(marketplaceId, marketplaceType);
                if (product == null)
                {
                    return NotFound();
                }
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by marketplace ID: {MarketplaceId} and type: {MarketplaceType}", 
                    marketplaceId, marketplaceType);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("marketplace/{marketplaceType}")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProductsByMarketplaceType(string marketplaceType)
        {
            try
            {
                var products = await _productRepository.GetByMarketplaceTypeAsync(marketplaceType);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by marketplace type: {MarketplaceType}", marketplaceType);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/variants")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProductVariants(Guid id)
        {
            try
            {
                var variants = await _productRepository.GetVariantsAsync(id);
                return Ok(variants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting variants for product ID: {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/stock")]
        public async Task<IActionResult> UpdateProductStock(Guid id, [FromBody] int quantity)
        {
            try
            {
                var success = await _productRepository.UpdateStockAsync(id, quantity);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for product ID: {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/price")]
        public async Task<IActionResult> UpdateProductPrice(Guid id, [FromBody] decimal price)
        {
            try
            {
                var success = await _productRepository.UpdatePriceAsync(id, price);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating price for product ID: {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Product>>> SearchProducts([FromQuery] string searchTerm)
        {
            try
            {
                var products = await _productRepository.SearchAsync(searchTerm);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with term: {SearchTerm}", searchTerm);
                return StatusCode(500, "Internal server error");
            }
        }

        // Додаємо новий ендпоінт для отримання контексту продуктів для чату
        [HttpGet("context/chat")]
        public async Task<ActionResult<object>> GetProductContextForChat()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }
                
                var context = await _sharedMemory.GetUserContext(userId);
                return Ok(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product context for chat");
                return StatusCode(500, "Internal server error");
            }
        }

        #region Helper Methods

        private Product MapToProduct(CreateProductDto dto)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Sku = dto.Sku,
                Keywords = dto.Keywords,
                Price = dto.Price,
                OldPrice = dto.OldPrice,
                Currency = dto.Currency,
                QuantityInStock = dto.QuantityInStock,
                InStock = dto.InStock,
                Description = dto.Description,
                MainImage = dto.MainImage,
                Images = dto.Images,
                Status = dto.Status,
                CategoryId = dto.CategoryId,
                CategoryName = dto.CategoryName,
                GroupId = dto.GroupId,
                GroupName = dto.GroupName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Додаємо властивості
            if (dto.Properties != null && dto.Properties.Any())
            {
                product.Properties = dto.Properties.Select(p => new ProductProperty
                {
                    Name = p.Name,
                    Value = p.Value,
                    Unit = p.Unit
                }).ToList();
            }

            // Додаємо варіанти
            if (dto.Variants != null && dto.Variants.Any())
            {
                product.ProductVariants = dto.Variants.Select(v => new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Name = v.Name,
                    Sku = v.Sku,
                    Price = v.Price,
                    OldPrice = v.OldPrice,
                    Currency = v.Currency,
                    QuantityInStock = v.QuantityInStock,
                    InStock = v.InStock,
                    Status = v.Status,
                    MainImage = v.MainImage,
                    Images = v.Images,
                    VariantAttributes = v.VariantAttributes,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
            }

            return product;
        }

        private void UpdateProductFromDto(Product product, CreateProductDto dto)
        {
            product.Name = dto.Name;
            product.Sku = dto.Sku;
            product.Keywords = dto.Keywords;
            product.Price = dto.Price;
            product.OldPrice = dto.OldPrice;
            product.Currency = dto.Currency;
            product.QuantityInStock = dto.QuantityInStock;
            product.InStock = dto.InStock;
            product.Description = dto.Description;
            product.MainImage = dto.MainImage;
            product.Images = dto.Images;
            product.Status = dto.Status;
            product.CategoryId = dto.CategoryId;
            product.CategoryName = dto.CategoryName;
            product.GroupId = dto.GroupId;
            product.GroupName = dto.GroupName;
            product.UpdatedAt = DateTime.UtcNow;
            
            // Оновлюємо властивості
            if (dto.Properties != null)
            {
                product.Properties = dto.Properties.Select(p => new ProductProperty
                {
                    Name = p.Name,
                    Value = p.Value,
                    Unit = p.Unit
                }).ToList();
            }
            
            // Обробка варіантів потребує складнішої логіки, тому її тут не включено
        }

        private ProductDto MapToProductDto(Product product)
        {
            var dto = new ProductDto
            {
                Id = product.Id,
                ExternalId = product.ExternalId,
                Name = product.Name,
                Sku = product.Sku,
                Keywords = product.Keywords,
                Price = product.Price,
                OldPrice = product.OldPrice,
                Currency = product.Currency,
                QuantityInStock = product.QuantityInStock,
                InStock = product.InStock,
                Description = product.Description,
                MainImage = product.MainImage,
                Images = product.Images,
                Status = product.Status,
                CategoryId = product.CategoryId,
                CategoryName = product.CategoryName,
                GroupId = product.GroupId,
                GroupName = product.GroupName,
                IsVariant = product.IsVariant,
                VariantGroupId = product.VariantGroupId,
                ParentProductId = product.ParentProductId,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };

            // Додаємо властивості
            if (product.Properties != null && product.Properties.Any())
            {
                dto.Properties = product.Properties.Select(p => new ProductPropertyDto
                {
                    Name = p.Name,
                    Value = p.Value,
                    Unit = p.Unit
                }).ToList();
            }

            // Додаємо варіанти
            if (product.ProductVariants != null && product.ProductVariants.Any())
            {
                dto.Variants = product.ProductVariants.Select(v => new ProductVariantDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Sku = v.Sku,
                    Price = v.Price,
                    OldPrice = v.OldPrice,
                    Currency = v.Currency,
                    QuantityInStock = v.QuantityInStock,
                    InStock = v.InStock,
                    Status = v.Status,
                    MainImage = v.MainImage,
                    Images = v.Images,
                    VariantAttributes = v.VariantAttributes
                }).ToList();
            }

            return dto;
        }

        #endregion
    }
} 