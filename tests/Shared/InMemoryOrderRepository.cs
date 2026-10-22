using System.Collections.Concurrent;
using System.Globalization;
using Orders.Domain;
using Orders.Domain.Abstractions;

namespace Orders.Tests.Support;

/// <summary>Repositório em memória com a mesma semântica do Cosmos: conflito no Id e ETag por versão.</summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, (Order Order, int Version)> _orders = new();

    public int Count => _orders.Count;

    public Task<bool> TryAddAsync(Order order, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.TryAdd(order.Id, (Snapshot(order, 1), 1)));

    public Task<Order?> GetAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.TryGetValue(id, out var entry) ? Snapshot(entry.Order, entry.Version) : null);

    public Task<IReadOnlyList<Order>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Order>>([.. _orders.Values
            .Where(e => e.Order.CustomerId == customerId)
            .Select(e => Snapshot(e.Order, e.Version))
            .OrderByDescending(o => o.CreatedAt)]);

    public Task UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        if (!_orders.TryGetValue(order.Id, out var current)
            || order.ConcurrencyToken != current.Version.ToString(CultureInfo.InvariantCulture)
            || !_orders.TryUpdate(order.Id, (Snapshot(order, current.Version + 1), current.Version + 1), current))
        {
            throw new ConcurrencyConflictException($"Order {order.Id} changed.");
        }
        return Task.CompletedTask;
    }

    /// <summary>Simula outra instância do worker alterando o pedido por fora.</summary>
    public void ForceUpdate(Order order)
    {
        var current = _orders[order.Id];
        _orders[order.Id] = (Snapshot(order, current.Version + 1), current.Version + 1);
    }

    private static Order Snapshot(Order o, int version) => Order.Rehydrate(
        o.Id, o.CustomerId, o.Items, o.Status, o.RejectionReason, o.CreatedAt, o.UpdatedAt,
        version.ToString(CultureInfo.InvariantCulture));
}
