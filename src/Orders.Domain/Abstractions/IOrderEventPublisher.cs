using Orders.Domain.Events;

namespace Orders.Domain.Abstractions;

public interface IOrderEventPublisher
{
    Task PublishAsync(OrderCreated orderCreated, CancellationToken cancellationToken);
}
