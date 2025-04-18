using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Tsintra.Api.Crm.Models.Prom
{
    /// <summary>
    /// Модель замовлення Prom.ua
    /// </summary>
    public class PromOrder
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("date_created")]
        public DateTime DateCreated { get; set; }

        [JsonPropertyName("date_modified")]
        public DateTime DateModified { get; set; }

        [JsonPropertyName("client_first_name")]
        public string ClientFirstName { get; set; } = string.Empty;

        [JsonPropertyName("client_second_name")]
        public string ClientSecondName { get; set; } = string.Empty;

        [JsonPropertyName("client_last_name")]
        public string ClientLastName { get; set; } = string.Empty;

        [JsonPropertyName("client_id")]
        public long? ClientId { get; set; }

        [JsonPropertyName("client")]
        public PromClient? Client { get; set; }

        [JsonPropertyName("delivery_recipient")]
        public PromDeliveryRecipient? DeliveryRecipient { get; set; }

        [JsonPropertyName("client_notes")]
        public string ClientNotes { get; set; } = string.Empty;

        [JsonPropertyName("products")]
        public List<PromOrderProduct> Products { get; set; } = new();

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;

        [JsonPropertyName("full_price")]
        public string FullPrice { get; set; } = string.Empty;

        [JsonPropertyName("delivery_option")]
        public PromDeliveryOption DeliveryOption { get; set; }

        [JsonPropertyName("delivery_provider_data")]
        public PromDeliveryProviderData DeliveryProviderData { get; set; }

        [JsonPropertyName("delivery_address")]
        public string DeliveryAddress { get; set; }

        [JsonPropertyName("delivery_cost")]
        public decimal DeliveryCost { get; set; }

        [JsonPropertyName("payment_option")]
        public PromPaymentOption PaymentOption { get; set; }

        [JsonPropertyName("payment_data")]
        public PromPaymentData PaymentData { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("status_name")]
        public string StatusName { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }

        [JsonPropertyName("price_with_special_offer")]
        public string? PriceWithSpecialOffer { get; set; }

        [JsonPropertyName("special_offer_discount")]
        public string? SpecialOfferDiscount { get; set; }

        [JsonPropertyName("special_offer_promocode")]
        public string? SpecialOfferPromocode { get; set; }

        [JsonPropertyName("has_order_promo_free_delivery")]
        public bool HasOrderPromoFreeDelivery { get; set; }

        [JsonPropertyName("cpa_commission")]
        public PromCpaCommission CpaCommission { get; set; }

        [JsonPropertyName("utm")]
        public PromUtm Utm { get; set; }

        [JsonPropertyName("dont_call_customer_back")]
        public bool DontCallCustomerBack { get; set; }

        [JsonPropertyName("ps_promotion")]
        public PromPromotion PsPromotion { get; set; }

        [JsonPropertyName("cancellation")]
        public PromCancellation Cancellation { get; set; }
    }

    /// <summary>
    /// Дані клієнта Prom.ua
    /// </summary>
    public class PromClient
    {
        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = null!;

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = null!;

        [JsonPropertyName("second_name")]
        public string? SecondName { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = null!;

        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    /// <summary>
    /// Дані отримувача замовлення Prom.ua
    /// </summary>
    public class PromDeliveryRecipient
    {
        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = null!;

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = null!;

        [JsonPropertyName("second_name")]
        public string? SecondName { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = null!;
    }

    /// <summary>
    /// Товар у замовленні Prom.ua
    /// </summary>
    public class PromOrderProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("external_id")]
        public string ExternalId { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public string Image { get; set; }

        [JsonPropertyName("quantity")]
        [JsonConverter(typeof(QuantityConverter))]
        public string QuantityStr { get; set; }
        
        [JsonIgnore]
        public int Quantity 
        { 
            get 
            {
                if (int.TryParse(QuantityStr, out int result))
                {
                    return result;
                }
                return 0;
            }
        }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("name_multilang")]
        public Dictionary<string, string> NameMultilang { get; set; }

        [JsonPropertyName("total_price")]
        public decimal TotalPrice { get; set; }

        [JsonPropertyName("measure_unit")]
        public string MeasureUnit { get; set; }

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("cpa_commission")]
        public PromCpaCommission CpaCommission { get; set; }
    }

    /// <summary>
    /// Конвертер для поля quantity, який може обробляти як рядкові, так і числові значення
    /// </summary>
    public class QuantityConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetDouble(out double value))
                {
                    return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            
            return string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// Варіант доставки Prom.ua
    /// </summary>
    public class PromDeliveryOption
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("shipping_service")]
        public string ShippingService { get; set; }
    }

    /// <summary>
    /// Дані провайдера доставки Prom.ua
    /// </summary>
    public class PromDeliveryProviderData
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = null!;

        [JsonPropertyName("type")]
        public string Type { get; set; } = null!;

        [JsonPropertyName("sender_warehouse_id")]
        public string SenderWarehouseId { get; set; } = null!;

        [JsonPropertyName("recipient_warehouse_id")]
        public string RecipientWarehouseId { get; set; } = null!;

        [JsonPropertyName("declaration_number")]
        public string DeclarationNumber { get; set; } = null!;

        [JsonPropertyName("unified_status")]
        public string UnifiedStatus { get; set; } = null!;
        
        [JsonPropertyName("recipient_address")]
        public PromRecipientAddress? RecipientAddress { get; set; }
    }

    /// <summary>
    /// Адреса отримувача Нової Пошти
    /// </summary>
    public class PromRecipientAddress
    {
        [JsonPropertyName("city_id")]
        public string CityId { get; set; } = null!;
        
        [JsonPropertyName("city_name")]
        public string CityName { get; set; } = null!;
        
        [JsonPropertyName("city_katottg")]
        public string CityKatottg { get; set; } = null!;
        
        [JsonPropertyName("warehouse_id")]
        public string WarehouseId { get; set; } = null!;
        
        [JsonPropertyName("street_id")]
        public string? StreetId { get; set; }
        
        [JsonPropertyName("street_name")]
        public string? StreetName { get; set; }
        
        [JsonPropertyName("building_number")]
        public string? BuildingNumber { get; set; }
        
        [JsonPropertyName("apartment_number")]
        public string? ApartmentNumber { get; set; }
    }

    /// <summary>
    /// Варіант оплати Prom.ua
    /// </summary>
    public class PromPaymentOption
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    /// <summary>
    /// Дані оплати Prom.ua
    /// </summary>
    public class PromPaymentData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("status_modified")]
        public string StatusModified { get; set; }
    }

    /// <summary>
    /// Комісія CPA Prom.ua
    /// </summary>
    public class PromCpaCommission
    {
        [JsonPropertyName("amount")]
        public string Amount { get; set; }

        [JsonPropertyName("is_refunded")]
        public bool IsRefunded { get; set; }
    }

    /// <summary>
    /// UTM-мітки Prom.ua
    /// </summary>
    public class PromUtm
    {
        [JsonPropertyName("medium")]
        public string Medium { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }

        [JsonPropertyName("campaign")]
        public string Campaign { get; set; }
    }

    /// <summary>
    /// Промоакція Prom.ua
    /// </summary>
    public class PromPromotion
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("conditions")]
        public List<string> Conditions { get; set; }
    }

    /// <summary>
    /// Дані скасування замовлення Prom.ua
    /// </summary>
    public class PromCancellation
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("initiator")]
        public string Initiator { get; set; }
    }

    /// <summary>
    /// Відповідь API Prom.ua на запит списку замовлень
    /// </summary>
    public class PromOrdersResponse
    {
        [JsonPropertyName("orders")]
        public List<PromOrder> Orders { get; set; }
    }

    /// <summary>
    /// Запит на оновлення статусу замовлення Prom.ua
    /// </summary>
    public class PromStatusUpdateRequest
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("cancellation_reason")]
        public string CancellationReason { get; set; }
    }
} 