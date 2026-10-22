using Orders.Domain.Events;
using Orders.Infrastructure.Messaging;

namespace Orders.Worker.Tests;

public class OrderMessageSerializerTests
{
    [Fact]
    public void Round_trips_OrderCreated()
    {
        var message = new OrderCreated("order-1", "customer-1", 120.90m, new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));

        var restored = OrderMessageSerializer.TryDeserialize(OrderMessageSerializer.Serialize(message));

        Assert.Equal(message, restored);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"orderId\":\"\"}")]
    public void Invalid_bodies_are_rejected(string body)
    {
        Assert.Null(OrderMessageSerializer.TryDeserialize(BinaryData.FromString(body)));
    }
}
