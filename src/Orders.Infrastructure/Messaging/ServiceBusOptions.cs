using System.ComponentModel.DataAnnotations;

namespace Orders.Infrastructure.Messaging;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string QueueName { get; set; } = "orders";
}
