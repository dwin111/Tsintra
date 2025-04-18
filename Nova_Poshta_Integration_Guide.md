# Інструкція з інтеграції та перевірки API Нової Пошти

## Налаштування

1. Отримайте API-ключ на сайті Нової Пошти
   - Зареєструйтесь на сайті https://novaposhta.ua
   - Увійдіть в особистий кабінет
   - Перейдіть в розділ "Мої ключі API" (https://new.novaposhta.ua/dashboard/settings/developers)
   - Створіть новий ключ API 2.0
   
2. Впишіть отриманий ключ в конфігурацію
   - Відкрийте файл `appsettings.json` в проекті Tsintra.Api
   - Замініть значення параметра `NovaPoshtaApiKey` на ваш API-ключ
   ```json
   "NovaPoshtaApiKey": "ВАШ_API_КЛЮЧ_ТУТ"
   ```

## Перевірка роботи API

### Метод 1: Використання Swagger

1. Запустіть API проект
   ```
   dotnet run --project Tsintra.Api
   ```

2. Відкрийте Swagger UI за адресою
   ```
   https://localhost:5001/swagger
   ```

3. Знайдіть розділ з ендпоінтами "NovaPoshtaController" та перевірте:
   - `GET /api/nova-poshta/cities` - для пошуку міст
   - `GET /api/nova-poshta/warehouses` - для пошуку відділень
   - `GET /api/nova-poshta/tracking/{trackingNumber}` - для відстеження посилки

### Метод 2: Використання тестового скрипту

1. Відредагуйте файл `NovaPoshtaApiTest.cs`:
   - Змініть `_baseUrl` на адресу вашого API
   - Виконайте скрипт для тестування основних функцій API

### Метод 3: Використання Postman

1. Створіть колекцію в Postman з запитами:

   **Пошук міст**
   ```
   GET {{baseUrl}}/api/nova-poshta/cities?searchText=Київ
   ```

   **Пошук відділень**
   ```
   GET {{baseUrl}}/api/nova-poshta/warehouses?cityRef=YOUR_CITY_REF
   ```

   **Відстеження**
   ```
   GET {{baseUrl}}/api/nova-poshta/tracking/59000000000000
   ```

   **Розрахунок вартості**
   ```
   GET {{baseUrl}}/api/nova-poshta/calculate-cost?citySender=CITY_REF_1&cityRecipient=CITY_REF_2&serviceType=WarehouseWarehouse&weight=1&declaredValue=500
   ```

## Усунення неполадок

1. **Помилка "Unauthorized"**
   - Перевірте правильність API-ключа
   - Перевірте, що ключ активований в особистому кабінеті

2. **Помилка "Not Found" при пошуку міст або відділень**
   - Перевірте параметри пошуку
   - Переконайтеся, що використовуєте правильні референси (Ref)

3. **Помилка під час розрахунку вартості**
   - Перевірте коректність параметрів (вага, тип доставки)
   - Переконайтеся, що вказані коректні референси міст (Ref)

## Використання у коді

### Приклад створення нової накладної

```csharp
// Отримати місто відправлення
var senderCity = await _novaPoshtaService.GetCityAsync("Київ", "Київська");

// Отримати місто призначення
var recipientCity = await _novaPoshtaService.GetCityAsync("Одеса", "Одеська");

// Отримати відділення в місті призначення
var warehouses = await _novaPoshtaService.GetWarehousesAsync(recipientCity.Ref);
var recipientWarehouse = warehouses.FirstOrDefault();

// Створити запит на нову накладну
var shipmentRequest = new InternetDocumentRequest
{
    CitySender = senderCity.Ref,
    CityRecipient = recipientCity.Ref,
    SenderAddress = "SENDER_WAREHOUSE_REF", // Використовуйте фактичний Ref складу відправлення
    RecipientAddress = recipientWarehouse.Ref,
    ServiceType = "WarehouseWarehouse",
    PayerType = "Recipient",
    PaymentMethod = "Cash",
    CargoType = "Cargo",
    Weight = 1.0m,
    SeatsAmount = 1,
    Description = "Товари зі складу",
    Cost = 500.0m,
    ContactSender = "КОНТАКТ_ВІДПРАВНИКА",
    SendersPhone = "380XXXXXXXXX",
    ContactRecipient = "Петренко Петро",
    RecipientsPhone = "380XXXXXXXXX"
};

// Створити накладну
var document = await _novaPoshtaService.CreateInternetDocumentAsync(shipmentRequest);

// Оновити замовлення з номером накладної
if (document != null)
{
    await _crmService.UpdateOrderWithTrackingAsync(orderId, document.IntDocNumber);
}
```

### Приклад відстеження посилки

```csharp
// Отримати інформацію про відстеження
var tracking = await _novaPoshtaService.TrackDocumentAsync("59000000000000");

// Використати статус
if (tracking != null)
{
    Console.WriteLine($"Статус: {tracking.Status}");
    Console.WriteLine($"Орієнтовна дата доставки: {tracking.ScheduledDeliveryDate}");
}
``` 