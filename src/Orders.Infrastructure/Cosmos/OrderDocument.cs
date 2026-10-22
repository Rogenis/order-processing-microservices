using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Orders.Domain;

namespace Orders.Infrastructure.Cosmos;

/// <summary>Formato persistido no Cosmos DB. Partição por /id: leituras pontuais pelo Id do pedido.</summary>
internal sealed class OrderDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public List<OrderItemDocument> Items { get; set; } = [];

    public decimal Total { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public OrderStatus Status { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    public static OrderDocument FromDomain(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Items = [.. order.Items.Select(i => new OrderItemDocument { Sku = i.Sku, Quantity = i.Quantity, UnitPrice = i.UnitPrice })],
        Total = order.Total,
        Status = order.Status,
        RejectionReason = order.RejectionReason,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
    };

    public Order ToDomain() => Order.Rehydrate(
        Id,
        CustomerId,
        [.. Items.Select(i => new OrderItem(i.Sku, i.Quantity, i.UnitPrice))],
        Status,
        RejectionReason,
        CreatedAt,
        UpdatedAt,
        ETag);
}

internal sealed class OrderItemDocument
{
    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
