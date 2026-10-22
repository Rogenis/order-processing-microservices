using Grpc.Core;
using Inventory.Contracts;
using Inventory.Grpc.Domain;

namespace Inventory.Grpc.Services;

public sealed class InventoryService(StockStore store, ILogger<InventoryService> logger) : Contracts.Inventory.InventoryBase
{
    public override Task<ReserveStockReply> ReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "order_id is required."));
        }

        var result = store.Reserve(request.OrderId, [.. request.Lines.Select(l => (l.Sku, l.Quantity))]);
        logger.LogInformation(
            "Reservation for order {OrderId}: {Reserved} {Reason}", request.OrderId, result.Reserved, result.Reason);

        return Task.FromResult(new ReserveStockReply { Reserved = result.Reserved, Reason = result.Reason ?? string.Empty });
    }

    public override Task<GetStockReply> GetStock(GetStockRequest request, ServerCallContext context) =>
        Task.FromResult(new GetStockReply { Sku = request.Sku, Available = store.GetAvailable(request.Sku) });
}
