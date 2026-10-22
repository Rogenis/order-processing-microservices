using Azure.Messaging.ServiceBus;
using Orders.Domain.Abstractions;
using Orders.Domain.Events;

namespace Orders.Infrastructure.Messaging;

internal sealed class ServiceBusOrderEventPublisher(ServiceBusSender sender) : IOrderEventPublisher
{
    public Task PublishAsync(OrderCreated orderCreated, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(OrderMessageSerializer.Serialize(orderCreated))
        {
            // MessageId = Id do pedido: com detecção de duplicidade na fila, republicar é seguro.
            MessageId = orderCreated.OrderId,
            ContentType = OrderMessageSerializer.ContentType,
            Subject = OrderMessageSerializer.OrderCreatedSubject,
        };
        return sender.SendMessageAsync(message, cancellationToken);
    }
}
