using Inventory.Contracts;
using Orders.Domain;
using InventoryClient = Inventory.Contracts.Inventory.InventoryClient;

namespace Orders.Worker.Inventory;

internal sealed class GrpcInventoryGateway(InventoryClient client) : IInventoryGateway
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public async Task<StockReservation> ReserveAsync(Order order, CancellationToken cancellationToken)
    {
        var request = new ReserveStockRequest { OrderId = order.Id };
        request.Lines.AddRange(order.Items.Select(i => new StockLine { Sku = i.Sku, Quantity = i.Quantity }));

        var reply = await client.ReserveStockAsync(
            request,
            deadline: DateTime.UtcNow.Add(Deadline),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new StockReservation(reply.Reserved, string.IsNullOrEmpty(reply.Reason) ? null : reply.Reason);
    }
}
