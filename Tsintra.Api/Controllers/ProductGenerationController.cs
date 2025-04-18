using Microsoft.AspNetCore.Mvc;
using Tsintra.Application.Interfaces; // For IProductGenerationService
using Tsintra.Domain.DTOs; // For ProductDetailsDto
using System.Security.Claims; // Для доступу до Claims
using Tsintra.Domain.Interfaces; // Для доступу до IUserRepository
using Microsoft.AspNetCore.Authorization; // Додаємо бібліотеку для атрибута Authorize

namespace Tsintra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize] // Додаємо обов'язкову авторизацію для всіх методів
public class ProductGenerationController : ControllerBase
{
    private readonly ILogger<ProductGenerationController> _logger;
    private readonly IProductGenerationService _productGenerationService;
    private readonly IUserRepository _userRepository; // Додаємо репозиторій для доступу до користувачів

    public ProductGenerationController(
        ILogger<ProductGenerationController> logger,
        IProductGenerationService productGenerationService,
        IUserRepository userRepository) // Додаємо залежність
    {
        _logger = logger;
        _productGenerationService = productGenerationService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Отримати Guid користувача з бази даних на основі GoogleId з клеймів авторизації
    /// </summary>
    /// <returns>ID користувача або null якщо користувач не знайдений</returns>
    private async Task<Guid?> GetAuthorizedUserIdAsync()
    {
        // Перевіряємо чи користувач авторизований
        if (User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Спроба доступу від неавторизованого користувача");
            return null;
        }

        // Спробуємо отримати ID користувача безпосередньо з клеймів
        var nameIdentifierClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(nameIdentifierClaim))
        {
            _logger.LogWarning("Не знайдено NameIdentifier у клеймах користувача");
            return null;
        }

        _logger.LogInformation("Знайдено NameIdentifier: {NameIdentifier} у клеймах", nameIdentifierClaim);

        // Спробуємо використати NameIdentifier безпосередньо як Guid
        if (Guid.TryParse(nameIdentifierClaim, out var userId))
        {
            _logger.LogInformation("Успішно отримано Guid з NameIdentifier: {UserId}", userId);
            
            // Перевіряємо, чи існує користувач з таким ID в базі
            var userById = await _userRepository.GetByIdAsync(userId);
            if (userById != null)
            {
                _logger.LogInformation("Знайдено користувача з ID: {UserId}", userId);
                return userId;
            }
        }

        // Якщо це не Guid, це може бути GoogleId; спробуємо знайти користувача за іншими клеймами
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email))
        {
            _logger.LogInformation("Знайдено Email: {Email} у клеймах", email);
            
            // Спробуємо знайти користувача за Email
            var userByEmail = await _userRepository.GetByEmailAsync(email);
            if (userByEmail != null)
            {
                _logger.LogInformation("Знайдено користувача з ID: {UserId} за Email: {Email}", userByEmail.Id, email);
                return userByEmail.Id;
            }
        }

        _logger.LogWarning("Не вдалося знайти користувача в базі даних за клеймами");
        return null;
    }

    /// <summary>
    /// Generates product details based on provided images and optional hints.
    /// </summary>
    /// <param name="images">Image files uploaded via form-data.</param>
    /// <param name="userHints">Optional user hints provided as a form field.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated product details or an error response.</returns>
    [HttpPost("generate")]
    [Consumes("multipart/form-data")] // Specify expected content type
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateProduct([FromForm] List<IFormFile> images, [FromForm] string? userHints, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received request to generate product with {Count} files.", images?.Count ?? 0);

        // Перевіряємо авторизацію і отримуємо ID користувача
        var userId = await GetAuthorizedUserIdAsync();
        if (!userId.HasValue)
        {
            return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
        }

        if (images == null || !images.Any())
        {
            return BadRequest("No image files provided.");
        }

        // Optional: Add validation for file types, sizes etc.
        // For example:
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
        foreach (var imageFile in images)
        {
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            {
                 _logger.LogWarning("Invalid file type uploaded: {FileName}", imageFile.FileName);
                 return BadRequest($"Invalid file type: {imageFile.FileName}. Allowed types are: {string.Join(", ", allowedExtensions)}");
            }
             // Add size check if needed: if (imageFile.Length > MAX_SIZE) ...
        }

        var base64Images = new List<string>();
        try
        {
            foreach (var imageFile in images)
            {
                 using var memoryStream = new MemoryStream();
                 await imageFile.CopyToAsync(memoryStream, cancellationToken);
                 base64Images.Add(Convert.ToBase64String(memoryStream.ToArray()));
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error reading uploaded files.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error processing uploaded files.");
        }
        

        try
        {
            // Встановлюємо ID користувача для агента
            _productGenerationService.SetUserId(userId.Value);
            _logger.LogInformation("Встановлено ID користувача для агента: {UserId}", userId.Value);
            
            var productDetails = await _productGenerationService.GenerateProductAsync(
                base64Images, // Pass converted base64 strings
                userHints,
                cancellationToken);

            if (productDetails == null)
            {
                _logger.LogWarning("Product generation service returned null.");
                return BadRequest(new { message = "Failed to generate product details. Check logs for more information." });
            }

            _logger.LogInformation("Successfully generated product: {ProductName}", productDetails.RefinedTitle);
            return Ok(productDetails);
        }
        catch (OperationCanceledException) 
        {
             _logger.LogInformation("Product generation request was cancelled.");
             return StatusCode(499, "Request cancelled by client."); 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during product generation endpoint execution.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Publishes product details to the marketplace.
    /// </summary>
    /// <param name="productDetails">Product details generated previously.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the publishing operation.</returns>
    [HttpPost("publish")]
    [ProducesResponseType(typeof(Tsintra.Domain.DTOs.PublishResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PublishProduct([FromBody] ProductDetailsDto productDetails, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received request to publish product: {ProductName}", productDetails.RefinedTitle);

        // Перевіряємо авторизацію і отримуємо ID користувача
        var userId = await GetAuthorizedUserIdAsync();
        if (!userId.HasValue)
        {
            return Unauthorized("Користувач не авторизований або не знайдений у базі даних");
        }

        if (productDetails == null) // Basic validation
        {
            return BadRequest("Product details cannot be null.");
        }

         try
        {
            // Встановлюємо ID користувача для агента
            _productGenerationService.SetUserId(userId.Value);
            _logger.LogInformation("Встановлено ID користувача для агента: {UserId}", userId.Value);
            
            var publishResult = await _productGenerationService.PublishProductAsync(productDetails, cancellationToken);

            if (!publishResult.Success)
            {
                 _logger.LogWarning("Publishing failed: {Message}", publishResult.Message);
                 // Повертаємо OK, але з результатом, що вказує на невдачу, або BadRequest
                 return Ok(publishResult); // Або BadRequest(publishResult) ?
            }

            _logger.LogInformation("Successfully published product (or attempted publish): {ProductName}", productDetails.RefinedTitle);
            return Ok(publishResult);
        }
        catch (OperationCanceledException) 
        {
             _logger.LogInformation("Product publishing request was cancelled.");
             return StatusCode(499, "Request cancelled by client."); // 499 Client Closed Request
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during product publishing endpoint execution.");
            // Повертаємо результат помилки, а не просто 500
            return Ok(new Tsintra.Domain.DTOs.PublishResultDto { Success = false, Message = $"An unexpected server error occurred: {ex.Message}"});
           // return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred during publishing.");
        }

    }
} 