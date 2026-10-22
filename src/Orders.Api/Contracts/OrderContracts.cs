using Orders.Domain;

namespace Orders.Api.Contracts;

public sealed record CreateOrderRequest(string? CustomerId, IReadOnlyList<OrderItemRequest>? Items);

public sealed record OrderItemRequest(string? Sku, int Quantity, decimal UnitPrice);

public sealed record OrderItemResponse(string Sku, int Quantity, decimal UnitPrice, decimal Subtotal);

public sealed record OrderResponse(
    string Id,
    string CustomerId,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Total,
    OrderStatus Status,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.CustomerId,
        [.. order.Items.Select(i => new OrderItemResponse(i.Sku, i.Quantity, i.UnitPrice, i.Subtotal))],
        order.Total,
        order.Status,
        order.RejectionReason,
        order.CreatedAt,
        order.UpdatedAt);
}
