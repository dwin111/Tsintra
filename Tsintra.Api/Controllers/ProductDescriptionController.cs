using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tsintra.Domain.Models;
using Tsintra.Domain.Interfaces;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.Persistence.Repositories;

namespace Tsintra.Api.Controllers
{
    public class GenerateDescriptionRequest
    {
        public Guid ProductId { get; set; }
        public string? UserPreferences { get; set; }
    }

    public class RefineDescriptionRequest
    {
        public Guid ProductId { get; set; }
        public string CurrentDescription { get; set; } = string.Empty;
        public string UserFeedback { get; set; } = string.Empty;
    }

    public class ProductDescriptionResponse
    {
        public string Description { get; set; } = string.Empty;
        public string Hashtags { get; set; } = string.Empty;
        public string CallToAction { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductDescriptionController : ControllerBase
    {
        private readonly IProductDescriptionAgent _descriptionAgent;
        private readonly IProductRepository _productRepository;
        private readonly ILogger<ProductDescriptionController> _logger;

        public ProductDescriptionController(
            IProductDescriptionAgent descriptionAgent,
            IProductRepository productRepository,
            ILogger<ProductDescriptionController> logger)
        {
            _descriptionAgent = descriptionAgent;
            _productRepository = productRepository;
            _logger = logger;
        }

        [HttpPost("generate")]
        public async Task<ActionResult<ProductDescriptionResponse>> GenerateDescription([FromBody] GenerateDescriptionRequest request)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(request.ProductId);
                if (product == null)
                {
                    return NotFound($"Product with ID {request.ProductId} not found");
                }

                var description = await _descriptionAgent.GenerateDescriptionAsync(product, request.UserPreferences);
                var hashtags = await _descriptionAgent.GenerateHashtagsAsync(product);
                var callToAction = await _descriptionAgent.GenerateCallToActionAsync(product);

                return Ok(new ProductDescriptionResponse
                {
                    Description = description,
                    Hashtags = hashtags,
                    CallToAction = callToAction
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product description for product {ProductId}", request.ProductId);
                return StatusCode(500, "An error occurred while generating the description");
            }
        }

        [HttpPost("refine")]
        public async Task<ActionResult<string>> RefineDescription([FromBody] RefineDescriptionRequest request)
        {
            try
            {
                var refinedDescription = await _descriptionAgent.RefineDescriptionAsync(
                    request.CurrentDescription, 
                    request.UserFeedback);

                return Ok(refinedDescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refining product description");
                return StatusCode(500, "An error occurred while refining the description");
            }
        }
    }
} 