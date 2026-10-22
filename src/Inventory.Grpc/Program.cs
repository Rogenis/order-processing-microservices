using Inventory.Grpc.Domain;
using Inventory.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSingleton(sp =>
{
    var stock = sp.GetRequiredService<IConfiguration>().GetSection("Inventory:Stock").Get<Dictionary<string, int>>() ?? [];
    return new StockStore(stock);
});

var app = builder.Build();

app.MapGrpcService<InventoryService>();
app.MapGet("/", () => "Inventory gRPC service. Use a gRPC client (see protos/inventory.proto).");

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
