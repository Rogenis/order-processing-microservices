using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Orders.Infrastructure.Messaging;

namespace Orders.Worker.Processing;

/// <summary>
/// Liga a fila ao <see cref="OrderCreatedHandler"/>. Falha transitória: agenda uma cópia da mensagem
/// com espera crescente (<see cref="RedeliveryPolicy"/>) e conclui a original; esgotadas as tentativas,
/// dead-letter. Mensagem que nunca vai dar certo (corpo inválido, pedido inexistente) vai direto para a
/// dead-letter.
/// </summary>
internal sealed class OrderQueueConsumer(
    ServiceBusClient client,
    IOptions<ServiceBusOptions> options,
    IServiceScopeFactory scopes,
    ILogger<OrderQueueConsumer> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;
    private ServiceBusSender? _retrySender;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _retrySender = client.CreateSender(options.Value.QueueName);
        _processor = client.CreateProcessor(options.Value.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4,
        });
        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);
        logger.LogInformation("Listening on queue {Queue}", options.Value.QueueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Parada normal do host.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
            await _processor.DisposeAsync().ConfigureAwait(false);
        }
        if (_retrySender is not null)
        {
            await _retrySender.DisposeAsync().ConfigureAwait(false);
        }
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = OrderMessageSerializer.TryDeserialize(args.Message.Body);
        if (message is null)
        {
            logger.LogError("Message {MessageId} has an invalid body; dead-lettering", args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "InvalidBody", "Body is not a valid OrderCreated.", args.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        using var scope = scopes.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<OrderCreatedHandler>();
        try
        {
            var outcome = await handler.HandleAsync(message, args.CancellationToken).ConfigureAwait(false);
            if (outcome == HandleOutcome.OrderNotFound)
            {
                await args.DeadLetterMessageAsync(args.Message, "OrderNotFound", $"Order {message.OrderId} does not exist.", args.CancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ScheduleRetryAsync(args, message.OrderId, ex).ConfigureAwait(false);
        }
    }

    private async Task ScheduleRetryAsync(ProcessMessageEventArgs args, string orderId, Exception error)
    {
        var attempt = args.Message.ApplicationProperties.TryGetValue(RedeliveryPolicy.AttemptProperty, out var value)
            && value is int previous ? previous + 1 : 1;

        var delay = RedeliveryPolicy.DelayFor(attempt);
        if (delay is null)
        {
            logger.LogError(error, "Order {OrderId} failed {Attempts} times; dead-lettering", orderId, attempt);
            await args.DeadLetterMessageAsync(args.Message, "RetriesExhausted", error.Message, args.CancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var retry = new ServiceBusMessage(args.Message)
        {
            // Id novo por tentativa: o mesmo Id seria descartado pela detecção de duplicidade da fila.
            MessageId = $"{orderId}-retry-{attempt}",
        };
        retry.ApplicationProperties[RedeliveryPolicy.AttemptProperty] = attempt;

        logger.LogWarning("Order {OrderId} failed (attempt {Attempt}): {Error}; retrying in {Delay}",
            orderId, attempt, error.Message, delay);
        // Agenda antes de concluir: se a conclusão falhar, a mensagem duplicada é absorvida pela idempotência.
        await _retrySender!.ScheduleMessageAsync(retry, DateTimeOffset.UtcNow + delay.Value, args.CancellationToken)
            .ConfigureAwait(false);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error from {Source} on {Entity}", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }
}
