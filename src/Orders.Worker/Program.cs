using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Options;
using Orders.Infrastructure;
using Orders.Worker.Inventory;
using Orders.Worker.Processing;
using InventoryClient = Inventory.Contracts.Inventory.InventoryClient;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOrderStore(builder.Configuration);
builder.Services.AddServiceBus(builder.Configuration);

builder.Services.AddOptions<InventoryOptions>()
    .Bind(builder.Configuration.GetSection(InventoryOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddGrpcClient<InventoryClient>((sp, o) => o.Address = sp.GetRequiredService<IOptions<InventoryOptions>>().Value.Url)
    // Quedas de poucos segundos do estoque são absorvidas aqui. ReserveStock é idempotente, então repetir é seguro.
    .ConfigureChannel(o => o.ServiceConfig = new ServiceConfig
    {
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    InitialBackoff = TimeSpan.FromMilliseconds(500),
                    MaxBackoff = TimeSpan.FromSeconds(2),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes = { StatusCode.Unavailable },
                },
            },
        },
    });
builder.Services.AddScoped<IInventoryGateway, GrpcInventoryGateway>();

builder.Services.AddScoped<OrderCreatedHandler>();
builder.Services.AddHostedService<OrderQueueConsumer>();

await builder.Build().RunAsync().ConfigureAwait(false);
