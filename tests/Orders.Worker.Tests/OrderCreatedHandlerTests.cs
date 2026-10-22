using Microsoft.Extensions.Logging.Abstractions;
using Orders.Domain;
using Orders.Domain.Events;
using Orders.Tests.Support;
using Orders.Worker.Inventory;
using Orders.Worker.Processing;

namespace Orders.Worker.Tests;

public class OrderCreatedHandlerTests
{
    private readonly InMemoryOrderRepository _repository = new();
    private readonly FakeInventory _inventory = new();
    private readonly FixedTimeProvider _clock = new();

    private OrderCreatedHandler NewHandler() =>
        new(_repository, _inventory, _clock, NullLogger<OrderCreatedHandler>.Instance);

    private async Task<Order> SeedOrderAsync()
    {
        var order = Order.Create("order-1", "customer-1", [new OrderItem("LAMP-04", 1, 199m)], FixedTimeProvider.Default);
        await _repository.TryAddAsync(order, CancellationToken.None);
        return order;
    }

    [Fact]
    public async Task Confirms_order_when_stock_is_reserved()
    {
        var order = await SeedOrderAsync();
        _clock.Now = FixedTimeProvider.Default.AddSeconds(3);

        var outcome = await NewHandler().HandleAsync(OrderCreated.From(order), CancellationToken.None);

        Assert.Equal(HandleOutcome.Confirmed, outcome);
        var stored = await _repository.GetAsync(order.Id, CancellationToken.None);
        Assert.Equal(OrderStatus.Confirmed, stored!.Status);
        Assert.Equal(_clock.Now, stored.UpdatedAt);
    }

    [Fact]
    public async Task Rejects_order_with_the_inventory_reason()
    {
        var order = await SeedOrderAsync();
        _inventory.Next = new StockReservation(false, "Insufficient stock for LAMP-04.");

        var outcome = await NewHandler().HandleAsync(OrderCreated.From(order), CancellationToken.None);

        Assert.Equal(HandleOutcome.Rejected, outcome);
        var stored = await _repository.GetAsync(order.Id, CancellationToken.None);
        Assert.Equal(OrderStatus.Rejected, stored!.Status);
        Assert.Equal("Insufficient stock for LAMP-04.", stored.RejectionReason);
    }

    [Fact]
    public async Task Redelivery_of_a_processed_order_is_skipped()
    {
        var order = await SeedOrderAsync();
        var handler = NewHandler();
        await handler.HandleAsync(OrderCreated.From(order), CancellationToken.None);

        var outcome = await handler.HandleAsync(OrderCreated.From(order), CancellationToken.None);

        Assert.Equal(HandleOutcome.AlreadyProcessed, outcome);
        Assert.Equal(1, _inventory.Calls);
    }

    [Fact]
    public async Task Missing_order_is_reported()
    {
        var message = new OrderCreated("ghost", "customer-1", 10m, FixedTimeProvider.Default);

        var outcome = await NewHandler().HandleAsync(message, CancellationToken.None);

        Assert.Equal(HandleOutcome.OrderNotFound, outcome);
        Assert.Equal(0, _inventory.Calls);
    }

    [Fact]
    public async Task Concurrent_processing_by_another_instance_is_treated_as_done()
    {
        var order = await SeedOrderAsync();
        // Outra instância confirma o pedido enquanto esta consulta o estoque.
        _inventory.OnReserve = () =>
        {
            var other = _repository.GetAsync(order.Id, CancellationToken.None).Result!;
            other.Confirm(FixedTimeProvider.Default);
            _repository.ForceUpdate(other);
        };

        var outcome = await NewHandler().HandleAsync(OrderCreated.From(order), CancellationToken.None);

        Assert.Equal(HandleOutcome.AlreadyProcessed, outcome);
    }

    [Fact]
    public async Task Inventory_failure_propagates_so_the_message_is_retried()
    {
        var order = await SeedOrderAsync();
        _inventory.OnReserve = () => throw new TimeoutException("Inventory down.");

        await Assert.ThrowsAsync<TimeoutException>(() => NewHandler().HandleAsync(OrderCreated.From(order), CancellationToken.None));

        var stored = await _repository.GetAsync(order.Id, CancellationToken.None);
        Assert.Equal(OrderStatus.Pending, stored!.Status);
    }

    private sealed class FakeInventory : IInventoryGateway
    {
        public StockReservation Next { get; set; } = new(true, null);

        public Action? OnReserve { get; set; }

        public int Calls { get; private set; }

        public Task<StockReservation> ReserveAsync(Order order, CancellationToken cancellationToken)
        {
            Calls++;
            OnReserve?.Invoke();
            return Task.FromResult(Next);
        }
    }
}
