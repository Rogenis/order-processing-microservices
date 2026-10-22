using Orders.Domain;

namespace Orders.Domain.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static Order NewOrder() => Order.Create(
        "order-1",
        "customer-1",
        [new OrderItem("KEYCHAIN-01", 2, 15.50m), new OrderItem("PLANTER-02", 1, 89.90m)],
        Now);

    [Fact]
    public void Create_starts_pending_and_computes_total()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(120.90m, order.Total);
        Assert.Equal(Now, order.CreatedAt);
        Assert.Equal(Now, order.UpdatedAt);
    }

    [Fact]
    public void Create_rejects_invalid_input()
    {
        Assert.Throws<ArgumentException>(() => Order.Create("order-1", "customer-1", [], Now));
        Assert.Throws<ArgumentException>(() => Order.Create("order-1", " ", [new OrderItem("A", 1, 1m)], Now));
    }

    [Fact]
    public void Confirm_moves_pending_to_confirmed()
    {
        var order = NewOrder();
        var later = Now.AddMinutes(1);

        order.Confirm(later);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(later, order.UpdatedAt);
        Assert.Null(order.RejectionReason);
    }

    [Fact]
    public void Reject_records_reason()
    {
        var order = NewOrder();

        order.Reject("Insufficient stock for LAMP-04.", Now);

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("Insufficient stock for LAMP-04.", order.RejectionReason);
    }

    [Fact]
    public void Final_states_cannot_change_again()
    {
        var order = NewOrder();
        order.Confirm(Now);

        Assert.Throws<InvalidOperationException>(() => order.Confirm(Now));
        Assert.Throws<InvalidOperationException>(() => order.Reject("late", Now));
    }

    [Fact]
    public void HasSameContentAs_ignores_item_order()
    {
        var order = NewOrder();

        Assert.True(order.HasSameContentAs("customer-1",
            [new OrderItem("PLANTER-02", 1, 89.90m), new OrderItem("KEYCHAIN-01", 2, 15.50m)]));
        Assert.False(order.HasSameContentAs("customer-1", [new OrderItem("KEYCHAIN-01", 3, 15.50m)]));
        Assert.False(order.HasSameContentAs("customer-2",
            [new OrderItem("KEYCHAIN-01", 2, 15.50m), new OrderItem("PLANTER-02", 1, 89.90m)]));
    }
}
