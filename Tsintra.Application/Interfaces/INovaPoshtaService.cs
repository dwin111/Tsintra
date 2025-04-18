using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models.NovaPost;

namespace Tsintra.Application.Interfaces
{
    public interface INovaPoshtaService
    {
        /// <summary>
        /// Отримати список міст
        /// </summary>
        /// <param name="findByString">Пошуковий рядок (назва міста)</param>
        /// <param name="limit">Кількість записів, що повертаються</param>
        Task<List<City>> GetCitiesAsync(string findByString, int limit = 20);

        /// <summary>
        /// Отримати місто за назвою та областю
        /// </summary>
        /// <param name="cityName">Назва міста</param>
        /// <param name="regionName">Назва області</param>
        Task<City> GetCityAsync(string cityName, string regionName);

        /// <summary>
        /// Отримати список відділень у місті
        /// </summary>
        /// <param name="cityRef">Код міста</param>
        /// <param name="findByString">Пошуковий рядок (назва або адреса відділення)</param>
        Task<List<Warehouse>> GetWarehousesAsync(string cityRef, string findByString = null);

        /// <summary>
        /// Отримати список відділень у місті за назвою міста
        /// </summary>
        /// <param name="cityName">Назва міста</param>
        /// <param name="regionName">Назва області (опціонально)</param>
        /// <param name="findByString">Пошуковий рядок (назва або адреса відділення)</param>
        Task<List<Warehouse>> GetWarehousesByCityNameAsync(string cityName, string regionName = null, string findByString = null);

        /// <summary>
        /// Відстежити вантаж за номером ТТН
        /// </summary>
        /// <param name="trackingNumber">Номер ТТН</param>
        Task<TrackingDocument> TrackDocumentAsync(string trackingNumber);

        /// <summary>
        /// Створити нову ТТН
        /// </summary>
        /// <param name="requestData">Дані для створення ТТН</param>
        Task<InternetDocument> CreateInternetDocumentAsync(InternetDocumentRequest requestData);

        /// <summary>
        /// Розрахувати вартість доставки
        /// </summary>
        /// <param name="citySender">Код міста відправника</param>
        /// <param name="cityRecipient">Код міста отримувача</param>
        /// <param name="serviceType">Тип доставки (WarehouseWarehouse або WarehouseDoors)</param>
        /// <param name="weight">Вага вантажу</param>
        /// <param name="cost">Оголошена вартість</param>
        Task<decimal> GetDocumentPriceAsync(string citySender, string cityRecipient, string serviceType, decimal weight, decimal cost);

        /// <summary>
        /// Отримати орієнтовну дату доставки
        /// </summary>
        /// <param name="citySender">Код міста відправника</param>
        /// <param name="cityRecipient">Код міста отримувача</param>
        /// <param name="serviceType">Тип доставки</param>
        /// <param name="date">Дата відправлення</param>
        Task<string> GetDocumentDeliveryDateAsync(string citySender, string cityRecipient, string serviceType, string date);
    }
}