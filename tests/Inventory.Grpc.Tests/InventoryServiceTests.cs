using Grpc.Core;
using Grpc.Net.Client;
using Inventory.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Inventory.Grpc.Tests;

/// <summary>Chama o serviço por gRPC de verdade (HTTP/2 sobre o TestServer), como o worker faz.</summary>
public sealed class InventoryServiceTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _app;
    private readonly GrpcChannel _channel;
    private readonly Contracts.Inventory.InventoryClient _client;

    public InventoryServiceTests(WebApplicationFactory<Program> factory)
    {
        _app = factory.WithWebHostBuilder(b => b
            .UseSetting("Inventory:Stock:WIDGET", "3")
            .UseSetting("Inventory:Stock:GADGET", "1"));
        _channel = GrpcChannel.ForAddress(_app.Server.BaseAddress, new GrpcChannelOptions { HttpHandler = _app.Server.CreateHandler() });
        _client = new Contracts.Inventory.InventoryClient(_channel);
    }

    [Fact]
    public async Task ReserveStock_reserves_and_updates_availability()
    {
        var request = new ReserveStockRequest { OrderId = "order-1" };
        request.Lines.Add(new StockLine { Sku = "WIDGET", Quantity = 2 });

        var reply = await _client.ReserveStockAsync(request);
        var stock = await _client.GetStockAsync(new GetStockRequest { Sku = "WIDGET" });

        Assert.True(reply.Reserved);
        Assert.Equal(1, stock.Available);
    }

    [Fact]
    public async Task ReserveStock_reports_insufficient_stock()
    {
        var request = new ReserveStockRequest { OrderId = "order-2" };
        request.Lines.Add(new StockLine { Sku = "GADGET", Quantity = 5 });

        var reply = await _client.ReserveStockAsync(request);

        Assert.False(reply.Reserved);
        Assert.Contains("GADGET", reply.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReserveStock_requires_order_id()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(async () => await _client.ReserveStockAsync(new ReserveStockRequest()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _app.Dispose();
    }
}
