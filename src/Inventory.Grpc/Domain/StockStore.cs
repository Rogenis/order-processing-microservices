namespace Inventory.Grpc.Domain;

public sealed record ReservationResult(bool Reserved, string? Reason)
{
    public static ReservationResult Success { get; } = new(true, null);

    public static ReservationResult Failure(string reason) => new(false, reason);
}

/// <summary>
/// Estoque em memória, semeado pela configuração. A reserva é tudo-ou-nada e idempotente por pedido:
/// o worker pode repetir a chamada depois de uma falha sem baixar o estoque duas vezes.
/// </summary>
public sealed class StockStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _available;
    private readonly Dictionary<string, ReservationResult> _reservations = new(StringComparer.Ordinal);

    public StockStore(IReadOnlyDictionary<string, int> initialStock)
    {
        _available = new Dictionary<string, int>(initialStock, StringComparer.OrdinalIgnoreCase);
    }

    public int GetAvailable(string sku)
    {
        lock (_gate)
        {
            return _available.GetValueOrDefault(sku);
        }
    }

    public ReservationResult Reserve(string orderId, IReadOnlyList<(string Sku, int Quantity)> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        lock (_gate)
        {
            if (_reservations.TryGetValue(orderId, out var previous))
            {
                return previous;
            }

            var result = TryReserve(lines);
            _reservations[orderId] = result;
            return result;
        }
    }

    private ReservationResult TryReserve(IReadOnlyList<(string Sku, int Quantity)> lines)
    {
        if (lines.Count == 0)
        {
            return ReservationResult.Failure("No items to reserve.");
        }

        foreach (var (sku, quantity) in lines)
        {
            if (quantity <= 0)
            {
                return ReservationResult.Failure($"Invalid quantity for {sku}.");
            }
            if (!_available.TryGetValue(sku, out var available))
            {
                return ReservationResult.Failure($"Unknown SKU {sku}.");
            }
            if (available < quantity)
            {
                return ReservationResult.Failure($"Insufficient stock for {sku}: requested {quantity}, available {available}.");
            }
        }

        foreach (var (sku, quantity) in lines)
        {
            _available[sku] -= quantity;
        }
        return ReservationResult.Success;
    }
}
