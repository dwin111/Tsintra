using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Application.Interfaces;
using Tsintra.Domain.Models.NovaPost;

namespace Tsintra.Api.Controllers
{
    [Route("api/nova-poshta")]
    [ApiController]
    public class NovaPoshtaController : ControllerBase
    {
        private readonly INovaPoshtaService _novaPoshtaService;
        private readonly ILogger<NovaPoshtaController> _logger;

        public NovaPoshtaController(
            INovaPoshtaService novaPoshtaService,
            ILogger<NovaPoshtaController> logger)
        {
            _novaPoshtaService = novaPoshtaService ?? throw new ArgumentNullException(nameof(novaPoshtaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Пошук міст за текстом
        /// </summary>
        [HttpGet("cities")]
        [ProducesResponseType(typeof(List<City>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<City>>> GetCities([FromQuery] string searchText, [FromQuery] int limit = 20)
        {
            try
            {
                _logger.LogInformation("Пошук міст за текстом: '{SearchText}', ліміт: {Limit}", searchText, limit);
                var cities = await _novaPoshtaService.GetCitiesAsync(searchText, limit);
                return Ok(cities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при пошуку міст: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при пошуку міст.");
            }
        }

        /// <summary>
        /// Отримання даних міста за назвою та областю
        /// </summary>
        [HttpGet("city")]
        [ProducesResponseType(typeof(City), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<City>> GetCity([FromQuery] string cityName, [FromQuery] string regionName)
        {
            try
            {
                _logger.LogInformation("Отримання даних міста: '{CityName}', область: '{RegionName}'", cityName, regionName);
                var city = await _novaPoshtaService.GetCityAsync(cityName, regionName);
                if (city == null)
                {
                    return NotFound($"Місто '{cityName}' в області '{regionName}' не знайдено.");
                }
                return Ok(city);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні даних міста: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при отриманні даних міста.");
            }
        }

        /// <summary>
        /// Отримання списку відділень у місті
        /// </summary>
        [HttpGet("warehouses")]
        [ProducesResponseType(typeof(List<Warehouse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<Warehouse>>> GetWarehouses([FromQuery] string cityRef, [FromQuery] string searchText = null)
        {
            try
            {
                _logger.LogInformation("Отримання списку відділень у місті: '{CityRef}', пошук: '{SearchText}'", cityRef, searchText);
                var warehouses = await _novaPoshtaService.GetWarehousesAsync(cityRef, searchText);
                return Ok(warehouses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку відділень: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при отриманні списку відділень.");
            }
        }

        /// <summary>
        /// Отримання списку відділень за назвою населеного пункту
        /// </summary>
        [HttpGet("warehouses-by-city")]
        [ProducesResponseType(typeof(List<Warehouse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<Warehouse>>> GetWarehousesByCity(
            [FromQuery] string cityName, 
            [FromQuery] string regionName = null, 
            [FromQuery] string searchText = null)
        {
            try
            {
                _logger.LogInformation("Отримання списку відділень за назвою міста: '{CityName}', область: '{RegionName}', пошук: '{SearchText}'", 
                    cityName, regionName, searchText);
                
                var warehouses = await _novaPoshtaService.GetWarehousesByCityNameAsync(cityName, regionName, searchText);
                return Ok(warehouses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку відділень за назвою міста: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при отриманні списку відділень за назвою міста.");
            }
        }

        /// <summary>
        /// Відстеження посилки за номером ТТН
        /// </summary>
        [HttpGet("tracking/{trackingNumber}")]
        [ProducesResponseType(typeof(TrackingDocument), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TrackingDocument>> TrackDocument(string trackingNumber)
        {
            try
            {
                _logger.LogInformation("Відстеження посилки за номером: '{TrackingNumber}'", trackingNumber);
                var document = await _novaPoshtaService.TrackDocumentAsync(trackingNumber);
                if (document == null)
                {
                    return NotFound($"Посилка з номером '{trackingNumber}' не знайдена.");
                }
                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при відстеженні посилки: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при відстеженні посилки.");
            }
        }

        /// <summary>
        /// Створення нової ТТН
        /// </summary>
        [HttpPost("shipments")]
        [ProducesResponseType(typeof(InternetDocument), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<InternetDocument>> CreateShipment([FromBody] InternetDocumentRequest request)
        {
            try
            {
                _logger.LogInformation("Створення нової ТТН");
                var document = await _novaPoshtaService.CreateInternetDocumentAsync(request);
                if (document == null)
                {
                    return BadRequest("Не вдалося створити ТТН. Перевірте дані запиту.");
                }
                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні ТТН: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при створенні ТТН.");
            }
        }

        /// <summary>
        /// Розрахунок вартості доставки
        /// </summary>
        [HttpGet("calculate-cost")]
        [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<decimal>> CalculateShippingCost(
            [FromQuery] string citySender,
            [FromQuery] string cityRecipient,
            [FromQuery] string serviceType,
            [FromQuery] decimal weight,
            [FromQuery] decimal declaredValue)
        {
            try
            {
                _logger.LogInformation(
                    "Розрахунок вартості доставки: з міста '{CitySender}' до міста '{CityRecipient}', " +
                    "тип сервісу: '{ServiceType}', вага: {Weight}, оголошена вартість: {DeclaredValue}",
                    citySender, cityRecipient, serviceType, weight, declaredValue);

                var cost = await _novaPoshtaService.GetDocumentPriceAsync(
                    citySender, cityRecipient, serviceType, weight, declaredValue);
                
                return Ok(cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при розрахунку вартості доставки: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при розрахунку вартості доставки.");
            }
        }

        /// <summary>
        /// Отримання орієнтовної дати доставки
        /// </summary>
        [HttpGet("delivery-date")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<string>> GetDeliveryDate(
            [FromQuery] string citySender,
            [FromQuery] string cityRecipient,
            [FromQuery] string serviceType,
            [FromQuery] string date)
        {
            try
            {
                _logger.LogInformation(
                    "Отримання орієнтовної дати доставки: з міста '{CitySender}' до міста '{CityRecipient}', " +
                    "тип сервісу: '{ServiceType}', дата відправлення: '{Date}'",
                    citySender, cityRecipient, serviceType, date);

                var deliveryDate = await _novaPoshtaService.GetDocumentDeliveryDateAsync(
                    citySender, cityRecipient, serviceType, date);
                
                if (deliveryDate == null)
                {
                    return BadRequest("Не вдалося розрахувати дату доставки. Перевірте дані запиту.");
                }
                
                return Ok(deliveryDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні орієнтовної дати доставки: {Message}", ex.Message);
                return StatusCode(500, "Сталася помилка при отриманні орієнтовної дати доставки.");
            }
        }
    }
} 