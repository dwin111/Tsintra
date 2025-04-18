using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Tsintra.Application.Configuration;
using Tsintra.Application.Services;
using Tsintra.Domain.Models.NovaPost;

namespace Tsintra.Tests
{
    public class NovaPoshtaServiceTests
    {
        private readonly Mock<IOptions<NovaPoshtaConfig>> _mockOptions;
        private readonly Mock<ILogger<NovaPoshtaService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly NovaPoshtaConfig _novaPoshtaConfig;
        private readonly HttpClient _httpClient;

        public NovaPoshtaServiceTests()
        {
            // Налаштування конфігурації
            _novaPoshtaConfig = new NovaPoshtaConfig
            {
                ApiKey = "тестовий_ключ_API",
                ApiUrl = "https://api.novaposhta.ua/v2.0/json/"
            };

            _mockOptions = new Mock<IOptions<NovaPoshtaConfig>>();
            _mockOptions.Setup(x => x.Value).Returns(_novaPoshtaConfig);

            // Налаштування логера
            _mockLogger = new Mock<ILogger<NovaPoshtaService>>();

            // Налаштування HTTP клієнта
            _httpClient = new HttpClient();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }

        [Fact]
        public void Constructor_InitializesCorrectly()
        {
            // Act
            var service = new NovaPoshtaService(_mockOptions.Object, _mockLogger.Object, _mockHttpClientFactory.Object);

            // Assert - якщо конструктор виконався без винятків, тест проходить
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_ThrowsException_WhenApiKeyIsNull()
        {
            // Arrange
            var nullKeyConfig = new NovaPoshtaConfig { ApiKey = null, ApiUrl = "https://api.novaposhta.ua/v2.0/json/" };
            var mockNullOptions = new Mock<IOptions<NovaPoshtaConfig>>();
            mockNullOptions.Setup(x => x.Value).Returns(nullKeyConfig);

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new NovaPoshtaService(mockNullOptions.Object, _mockLogger.Object, _mockHttpClientFactory.Object));

            Assert.Equal("ApiKey", exception.ParamName);
        }

        [Fact(Skip = "Потребує справжнього API ключа")]
        public async Task GetCitiesAsync_ReturnsData_WhenApiIsAvailable()
        {
            // Arrange
            var service = new NovaPoshtaService(_mockOptions.Object, _mockLogger.Object, _mockHttpClientFactory.Object);

            // Act
            var cities = await service.GetCitiesAsync("Київ");

            // Assert
            Assert.NotNull(cities);
            Assert.NotEmpty(cities);
        }
    }
} 