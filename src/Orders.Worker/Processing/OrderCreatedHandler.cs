using Orders.Domain;
using Orders.Domain.Abstractions;
using Orders.Domain.Events;
using Orders.Worker.Inventory;

namespace Orders.Worker.Processing;

public enum HandleOutcome
{
    Confirmed,
    Rejected,
    AlreadyProcessed,
    OrderNotFound,
}

/// <summary>
/// Regra de negócio do worker, separada do Service Bus para ser testada sem fila.
/// Segura para reentrega (at-least-once): pedido fora de Pending é ignorado, e a reserva
/// no estoque é idempotente por Id do pedido.
/// </summary>
public sealed class OrderCreatedHandler(
    IOrderRepository repository,
    IInventoryGateway inventory,
    TimeProvider clock,
    ILogger<OrderCreatedHandler> logger)
{
    public async Task<HandleOutcome> HandleAsync(OrderCreated message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var order = await repository.GetAsync(message.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            logger.LogWarning("Order {OrderId} from message not found", message.OrderId);
            return HandleOutcome.OrderNotFound;
        }

        if (order.Status != OrderStatus.Pending)
        {
            logger.LogInformation("Order {OrderId} already {Status}; skipping redelivery", order.Id, order.Status);
            return HandleOutcome.AlreadyProcessed;
        }

        var reservation = await inventory.ReserveAsync(order, cancellationToken).ConfigureAwait(false);
        if (reservation.Reserved)
        {
            order.Confirm(clock.GetUtcNow());
        }
        else
        {
            order.Reject(reservation.Reason ?? "Stock reservation failed.", clock.GetUtcNow());
        }

        try
        {
            await repository.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            // Outra instância processou o mesmo pedido entre a leitura e a escrita.
            var current = await repository.GetAsync(order.Id, cancellationToken).ConfigureAwait(false);
            if (current is not null && current.Status != OrderStatus.Pending)
            {
                return HandleOutcome.AlreadyProcessed;
            }
            throw;
        }

        logger.LogInformation("Order {OrderId} {Status}", order.Id, order.Status);
        return reservation.Reserved ? HandleOutcome.Confirmed : HandleOutcome.Rejected;
    }
}
