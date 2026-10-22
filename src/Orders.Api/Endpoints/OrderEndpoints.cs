using Orders.Api.Contracts;
using Orders.Api.Security;
using Orders.Domain;
using Orders.Domain.Abstractions;
using Orders.Domain.Events;

namespace Orders.Api.Endpoints;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .WithTags("Orders");

        orders.MapPost("/", CreateOrderAsync)
            .WithName("CreateOrder")
            .WithSummary("Creates an order and publishes OrderCreated for asynchronous processing.");

        orders.MapGet("/{id}", GetOrderAsync)
            .WithName("GetOrder");

        orders.MapGet("/", ListOrdersAsync)
            .WithName("ListOrders")
            .WithSummary("Lists a customer's orders, newest first.");

        return app;
    }

    internal static async Task<IResult> CreateOrderAsync(
        CreateOrderRequest request,
        HttpRequest httpRequest,
        IOrderRepository repository,
        IOrderEventPublisher publisher,
        TimeProvider clock,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var items = request.Items?.Select(i => new OrderItem(i.Sku ?? string.Empty, i.Quantity, i.UnitPrice)).ToList();
        var errors = OrderValidator.Validate(request.CustomerId, items);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var customerId = request.CustomerId!;
        var idempotencyKey = httpRequest.Headers[IdempotencyKey.HeaderName].ToString();
        string orderId;
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            orderId = Guid.NewGuid().ToString("N");
        }
        else if (IdempotencyKey.IsValid(idempotencyKey))
        {
            orderId = IdempotencyKey.ToOrderId(customerId, idempotencyKey);
        }
        else
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [IdempotencyKey.HeaderName] = ["Use 8 to 100 characters: letters, digits, '-' or '_'."],
            });
        }

        var order = Order.Create(orderId, customerId, items!, clock.GetUtcNow());
        var created = await repository.TryAddAsync(order, cancellationToken).ConfigureAwait(false);

        if (!created)
        {
            var existing = await repository.GetAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (existing is null || !existing.HasSameContentAs(customerId, items!))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Idempotency-Key already used with a different payload.");
            }

            // Repetição da mesma requisição. Se o evento não chegou a sair da primeira vez, sai agora;
            // o worker ignora pedidos que já não estão Pending.
            if (existing.Status == OrderStatus.Pending)
            {
                var replayResult = await TryPublishAsync(existing, publisher, logger, cancellationToken).ConfigureAwait(false);
                if (replayResult is not null)
                {
                    return replayResult;
                }
            }
            return Results.Ok(OrderResponse.From(existing));
        }

        var publishFailure = await TryPublishAsync(order, publisher, logger, cancellationToken).ConfigureAwait(false);
        return publishFailure ?? Results.Created($"/orders/{order.Id}", OrderResponse.From(order));
    }

    internal static async Task<IResult> GetOrderAsync(string id, IOrderRepository repository, CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return order is null ? Results.NotFound() : Results.Ok(OrderResponse.From(order));
    }

    internal static async Task<IResult> ListOrdersAsync(string? customerId, IOrderRepository repository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["customerId"] = ["customerId is required."] });
        }

        var orders = await repository.ListByCustomerAsync(customerId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(orders.Select(OrderResponse.From));
    }

    private static async Task<IResult?> TryPublishAsync(
        Order order,
        IOrderEventPublisher publisher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await publisher.PublishAsync(OrderCreated.From(order), cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Order {OrderId} saved but OrderCreated was not published", order.Id);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Order saved, but it could not be queued for processing.",
                detail: $"Retry the request with the same {IdempotencyKey.HeaderName} header to resume it.");
        }
    }
}
