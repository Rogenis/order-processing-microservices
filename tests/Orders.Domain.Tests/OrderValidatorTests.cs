using Orders.Domain;

namespace Orders.Domain.Tests;

public class OrderValidatorTests
{
    [Fact]
    public void Valid_order_has_no_errors()
    {
        var errors = OrderValidator.Validate("customer-1", [new OrderItem("A", 1, 0m)]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Reports_every_problem_at_once()
    {
        var errors = OrderValidator.Validate(null, [new OrderItem("", 0, -1m), new OrderItem("B", 1, 1m)]);

        Assert.Contains("customerId", errors.Keys);
        Assert.Equal(3, errors["items[0]"].Length);
        Assert.DoesNotContain("items[1]", errors.Keys);
    }

    [Fact]
    public void Missing_items_is_an_error()
    {
        Assert.Contains("items", OrderValidator.Validate("c", null).Keys);
        Assert.Contains("items", OrderValidator.Validate("c", []).Keys);
    }

    [Fact]
    public void Repeated_sku_is_an_error()
    {
        var errors = OrderValidator.Validate("c", [new OrderItem("A", 1, 1m), new OrderItem("a", 2, 1m)]);

        Assert.Contains("repeated", errors["items"][0], StringComparison.Ordinal);
    }

    [Fact]
    public void Quantity_has_an_upper_bound()
    {
        var errors = OrderValidator.Validate("c", [new OrderItem("A", OrderValidator.MaxQuantityPerItem + 1, 1m)]);

        Assert.Contains("items[0]", errors.Keys);
    }
}
