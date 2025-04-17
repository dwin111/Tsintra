
using Microsoft.AspNetCore.Mvc;
using Tsintra.App.Interfaces;
using Tsintra.App.Models;
using Tsintra.Integrations.OpenAI; // Потрібно для OpenAiLLMClient

namespace Tsintra.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly OpenAiLLMClient _imageService; // Використовуємо ін'єктований сервіс

        // Конструктор залишається без змін
        public TestController(OpenAiLLMClient imageService)
        {
            _imageService = imageService;
        }


        [HttpPost("describe")]
        [RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Describe([FromForm] string prompt, [FromForm] List<IFormFile> files, [FromForm] List<string> urls)
        {
            var hasFiles = files != null && files.Any(f => f.Length > 0);
            var hasUrls = urls != null && urls.Any(u => !string.IsNullOrWhiteSpace(u));

            //if (string.IsNullOrWhiteSpace(prompt) || (!hasFiles && !hasUrls) || (hasFiles && hasUrls))
            //{
            //    return BadRequest("Будь ласка, надайте опис та або хоча б один файл зображення, або хоча б одне посилання на зображення.");
            //}

            var imageSources = new List<ImageSource>();

            if (hasFiles)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await file.CopyToAsync(memoryStream);
                            imageSources.Add(new ImageSource(memoryStream.ToArray(), file.FileName, file.ContentType, null));
                        }
                    }
                }
            }
           if (hasUrls)
            {
                imageSources.AddRange(urls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => new ImageSource(null, null, null, u)));
            }

            if (!imageSources.Any())
            {
                return BadRequest("Не знайдено жодного дійсного зображення.");
            }

            try
            {
                var description = await _imageService.DescribeImagesAsync(prompt, imageSources);
                return Ok(description);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Помилка опису зображень: {ex.Message}");
            }
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] ImageOptions req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Prompt))
            {
                return BadRequest("Тіло запиту відсутнє або 'prompt' порожній.");
            }

            try
            {
                object imageResult = await _imageService.GenerateImageAsync(req.Prompt, req);

                return req.Format switch
                {
                    ImageFormat.Bytes =>
                        File(((BinaryData)imageResult).ToArray(), "image/png", $"generated_image_{Guid.NewGuid()}.png"),
                    ImageFormat.Uri =>
                        Ok((string)imageResult),// Правильне приведення до Uri та отримання рядкового представлення
                    _ => BadRequest("Непідтримуваний формат запиту.")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Image Generation Error: {ex}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Сталася помилка під час генерації зображення: {ex.Message}");
            }
        }
    }
}