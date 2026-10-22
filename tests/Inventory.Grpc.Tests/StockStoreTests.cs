using Inventory.Grpc.Domain;

namespace Inventory.Grpc.Tests;

public class StockStoreTests
{
    private static StockStore NewStore() => new(new Dictionary<string, int> { ["A"] = 5, ["B"] = 1 });

    [Fact]
    public void Reserve_decrements_all_lines()
    {
        var store = NewStore();

        var result = store.Reserve("order-1", [("A", 2), ("B", 1)]);

        Assert.True(result.Reserved);
        Assert.Equal(3, store.GetAvailable("A"));
        Assert.Equal(0, store.GetAvailable("B"));
    }

    [Fact]
    public void Reserve_is_all_or_nothing()
    {
        var store = NewStore();

        var result = store.Reserve("order-1", [("A", 2), ("B", 2)]);

        Assert.False(result.Reserved);
        Assert.Contains("Insufficient stock for B", result.Reason, StringComparison.Ordinal);
        Assert.Equal(5, store.GetAvailable("A"));
        Assert.Equal(1, store.GetAvailable("B"));
    }

    [Fact]
    public void Reserve_is_idempotent_per_order()
    {
        var store = NewStore();

        var first = store.Reserve("order-1", [("A", 2)]);
        var retry = store.Reserve("order-1", [("A", 2)]);

        Assert.True(first.Reserved);
        Assert.Equal(first, retry);
        Assert.Equal(3, store.GetAvailable("A"));
    }

    [Fact]
    public void Unknown_sku_is_rejected()
    {
        var result = NewStore().Reserve("order-1", [("Z", 1)]);

        Assert.False(result.Reserved);
        Assert.Contains("Unknown SKU Z", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_orders_never_oversell()
    {
        var store = new StockStore(new Dictionary<string, int> { ["A"] = 10 });

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => store.Reserve($"order-{i}", [("A", 1)]))));

        Assert.Equal(10, results.Count(r => r.Reserved));
        Assert.Equal(0, store.GetAvailable("A"));
    }
}
