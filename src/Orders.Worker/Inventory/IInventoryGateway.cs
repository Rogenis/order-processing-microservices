using Orders.Domain;

namespace Orders.Worker.Inventory;

public sealed record StockReservation(bool Reserved, string? Reason);

public interface IInventoryGateway
{
    Task<StockReservation> ReserveAsync(Order order, CancellationToken cancellationToken);
}
