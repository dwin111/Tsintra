# Tsintra

## CRM System with Marketplace Integrations

The Tsintra project is a comprehensive CRM system with marketplace integrations, including Prom.ua.

## Prom.ua API Integration

The CRM system integrates with Prom.ua through a dedicated API service.

### Features

- Product synchronization (import/export between CRM and Prom.ua)
- Order management
- Inventory updates
- Status updates

### API Endpoints

#### Products

- `GET /api/prom/products` - Get all products from Prom.ua
- `GET /api/prom/products/{marketplaceProductId}` - Get product by marketplace ID
- `POST /api/prom/products` - Create a new product in Prom.ua
- `PUT /api/prom/products/{marketplaceProductId}` - Update an existing product
- `DELETE /api/prom/products/{marketplaceProductId}` - Delete a product
- `POST /api/prom/products/import` - Import products from Prom.ua to the CRM
- `POST /api/prom/products/export` - Export products from the CRM to Prom.ua
- `PUT /api/prom/products/{marketplaceProductId}/inventory` - Update product inventory
- `POST /api/prom/products/{productId}/publish` - Publish a specific product to Prom.ua by its ID
- `GET /api/prom/products/all` - Get all products from Prom.ua and save them to the database
- `GET /api/prom/db/products` - Get all Prom.ua products from the database

#### Orders

- `GET /api/prom/orders` - Get orders from Prom.ua
- `POST /api/prom/orders/import` - Import orders from Prom.ua to the CRM
- `PUT /api/prom/orders/{marketplaceOrderId}/status` - Update order status

### Configuration

To configure the Prom.ua integration, update the `appsettings.json` file with the following section:

```json
"PromUa": {
  "ApiKey": "your-prom-api-key",
  "BaseUrl": "https://my.prom.ua/api/v1/",
  "Settings": {
    "DefaultCurrency": "UAH",
    "DefaultLanguage": "uk",
    "RequestTimeoutSeconds": 30,
    "RetryAttempts": 3,
    "ImportEnabled": true,
    "ExportEnabled": true
  }
}
```