using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Tsintra.Domain.Models.NovaPost;

// Цей скрипт демонструє, як викликати API ендпоінти для роботи з Новою Поштою
public class NovaPoshtaApiTest
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://your-api-domain.com/api/crm";

    public NovaPoshtaApiTest()
    {
        _httpClient = new HttpClient();
    }

    public async Task RunTests()
    {
        Console.WriteLine("=== Тестування інтеграції з API Нової Пошти ===\n");

        try
        {
            // 1. Пошук міста
            Console.WriteLine("1. Пошук міста 'Київ'");
            var cities = await _httpClient.GetFromJsonAsync<City[]>($"{_baseUrl}/nova-poshta/cities?searchText=Київ");
            Console.WriteLine($"Знайдено {cities.Length} міст");
            if (cities.Length > 0)
            {
                var kyivCity = cities[0];
                Console.WriteLine($"Перше місто: {kyivCity.Description}, Ref: {kyivCity.Ref}");

                // 2. Пошук відділень у місті
                Console.WriteLine("\n2. Пошук відділень в місті");
                var warehouses = await _httpClient.GetFromJsonAsync<Warehouse[]>($"{_baseUrl}/nova-poshta/warehouses?cityRef={kyivCity.Ref}");
                Console.WriteLine($"Знайдено {warehouses.Length} відділень");
                if (warehouses.Length > 0)
                {
                    Console.WriteLine($"Перше відділення: {warehouses[0].Description}, Адреса: {warehouses[0].ShortAddress}");
                }

                // 3. Розрахунок вартості доставки
                Console.WriteLine("\n3. Розрахунок вартості доставки");
                var odessaRef = "сюди_потрібно_вставити_ref_одеси"; // У реальному тесті отримуємо через пошук
                var cost = await _httpClient.GetFromJsonAsync<decimal>($"{_baseUrl}/nova-poshta/calculate-cost?citySender={kyivCity.Ref}&cityRecipient={odessaRef}&serviceType=WarehouseWarehouse&weight=1&declaredValue=500");
                Console.WriteLine($"Вартість доставки: {cost} грн");

                // 4. Розрахунок дати доставки
                Console.WriteLine("\n4. Розрахунок дати доставки");
                var today = DateTime.Now.ToString("dd.MM.yyyy");
                var deliveryDate = await _httpClient.GetFromJsonAsync<string>($"{_baseUrl}/nova-poshta/delivery-date?citySender={kyivCity.Ref}&cityRecipient={odessaRef}&serviceType=WarehouseWarehouse&date={today}");
                Console.WriteLine($"Орієнтовна дата доставки: {deliveryDate}");
            }

            // 5. Відстеження посилки
            Console.WriteLine("\n5. Відстеження посилки");
            try
            {
                var trackingNumber = "59000000000000"; // Тестовий номер
                var tracking = await _httpClient.GetFromJsonAsync<TrackingDocument>($"{_baseUrl}/nova-poshta/tracking/{trackingNumber}");
                Console.WriteLine($"Статус посилки: {tracking.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при відстеженні: {ex.Message}");
            }

            Console.WriteLine("\nТестування завершено успішно!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nТестування перервано через помилку: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    public static async Task Main()
    {
        var test = new NovaPoshtaApiTest();
        await test.RunTests();
        Console.WriteLine("\nНатисніть будь-яку клавішу для виходу...");
        Console.ReadKey();
    }
} 