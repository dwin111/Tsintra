using Microsoft.AspNetCore.Mvc;
using Tsintra.Domain.Interfaces;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Tsintra.Core.Models;
using Tsintra.Domain.DTOs;

namespace Tsintra.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketplaceController : ControllerBase
    {
        private readonly ILLMClient _llmClient;
        private readonly IMarketplaceClient _marketplaceClient;

        public MarketplaceController(ILLMClient llmClient, IMarketplaceClient marketplaceClient)
        {
            _llmClient = llmClient;
            _marketplaceClient = marketplaceClient;
        }

        [HttpGet("openai/generate")]
        public async Task<IActionResult> GenerateText([FromQuery] string prompt, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return BadRequest("Prompt cannot be empty.");
            }

            var result = await _llmClient.GenerateTextAsync(prompt, ct);
            return Ok(result);
        }

        [HttpGet("promua/products")]
        public async Task<IActionResult> GetPromUaProducts(CancellationToken ct)
        {
            var products = await _marketplaceClient.GetProductsAsync(ct);
            return Ok(products);
        }

        [HttpPost("promua/products")]
        public async Task<IActionResult> AddPromUaProduct([FromBody] MinimalCreateProductDto request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var productToAdd = new MarketplaceProduct(
                Id: null, // ID буде присвоєно Prom.ua
                Name: request.Name,
                Price: request.Price,
                Description: string.Empty,
                SpecificAttributes: new Dictionary<string, object>()
            );

            var addedProduct = await _marketplaceClient.AddProductAsync(productToAdd, ct);
            return CreatedAtAction(nameof(GetPromUaProductById), new { id = addedProduct.Id }, addedProduct);
        }

        [HttpGet("promua/products/{id}")]
        public async Task<IActionResult> GetPromUaProductById(string id, CancellationToken ct)
        {
            var product = await _marketplaceClient.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return Ok(product);
        }

        [HttpPut("promua/products/{id}")]
        public async Task<IActionResult> UpdatePromUaProduct(string id, [FromBody] UpdateProductRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingProduct = await _marketplaceClient.GetProductByIdAsync(id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            var productToUpdate = new MarketplaceProduct(
                Id: id,
                Name: request.Name ?? existingProduct.Name,
                Price: request.Price ?? existingProduct.Price,
                Description: request.Description ?? existingProduct.Description,
                SpecificAttributes: request.SpecificAttributes ?? existingProduct.SpecificAttributes
            );

            var updatedProduct = await _marketplaceClient.UpdateProductAsync(productToUpdate, ct);
            return Ok(updatedProduct);
        }
    }

    public class UpdateProductRequest
    {
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, object>? SpecificAttributes { get; set; }
    }
}