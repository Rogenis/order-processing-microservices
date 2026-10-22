using System.Text.Json;
using Orders.Domain.Events;

namespace Orders.Infrastructure.Messaging;

public static class OrderMessageSerializer
{
    public const string ContentType = "application/json";
    public const string OrderCreatedSubject = "OrderCreated";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static BinaryData Serialize(OrderCreated message) => BinaryData.FromObjectAsJson(message, Options);

    /// <summary>Retorna null quando o corpo não é um OrderCreated válido (mensagem venenosa).</summary>
    public static OrderCreated? TryDeserialize(BinaryData body)
    {
        try
        {
            var message = body.ToObjectFromJson<OrderCreated>(Options);
            return string.IsNullOrWhiteSpace(message?.OrderId) ? null : message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
