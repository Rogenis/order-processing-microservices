namespace Orders.Domain.Events;

/// <summary>Mensagem publicada na fila quando um pedido é aceito. O worker relê o pedido pelo Id.</summary>
public sealed record OrderCreated(string OrderId, string CustomerId, decimal Total, DateTimeOffset OccurredAt)
{
    public static OrderCreated From(Order order) => new(order.Id, order.CustomerId, order.Total, order.CreatedAt);
}
