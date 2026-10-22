namespace Orders.Domain;

public sealed class Order
{
    private Order(
        string id,
        string customerId,
        IReadOnlyList<OrderItem> items,
        OrderStatus status,
        string? rejectionReason,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? concurrencyToken)
    {
        Id = id;
        CustomerId = customerId;
        Items = items;
        Status = status;
        RejectionReason = rejectionReason;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ConcurrencyToken = concurrencyToken;
    }

    public string Id { get; }
    public string CustomerId { get; }
    public IReadOnlyList<OrderItem> Items { get; }
    public decimal Total => Items.Sum(i => i.Subtotal);
    public OrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Versão opaca do armazenamento (ETag), usada para concorrência otimista.</summary>
    public string? ConcurrencyToken { get; }

    public static Order Create(string id, string customerId, IReadOnlyList<OrderItem> items, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var errors = OrderValidator.Validate(customerId, items);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors.SelectMany(e => e.Value)), nameof(items));
        }

        return new Order(id, customerId, [.. items], OrderStatus.Pending, null, now, now, null);
    }

    /// <summary>Reconstrói um pedido já persistido, sem revalidar regras de criação.</summary>
    public static Order Rehydrate(
        string id,
        string customerId,
        IReadOnlyList<OrderItem> items,
        OrderStatus status,
        string? rejectionReason,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? concurrencyToken) =>
        new(id, customerId, items, status, rejectionReason, createdAt, updatedAt, concurrencyToken);

    public void Confirm(DateTimeOffset now)
    {
        EnsurePending();
        Status = OrderStatus.Confirmed;
        UpdatedAt = now;
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsurePending();
        Status = OrderStatus.Rejected;
        RejectionReason = reason;
        UpdatedAt = now;
    }

    /// <summary>Mesmo cliente e mesmos itens: usado para reconhecer a repetição de um pedido idempotente.</summary>
    public bool HasSameContentAs(string customerId, IReadOnlyList<OrderItem> items) =>
        string.Equals(CustomerId, customerId, StringComparison.Ordinal)
        && Items.Count == items.Count
        && Items.OrderBy(i => i.Sku, StringComparer.Ordinal)
            .SequenceEqual(items.OrderBy(i => i.Sku, StringComparer.Ordinal));

    private void EnsurePending()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Order {Id} is already {Status}.");
        }
    }
}
