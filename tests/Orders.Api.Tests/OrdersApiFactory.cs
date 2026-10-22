using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Domain.Abstractions;
using Orders.Domain.Events;
using Orders.Tests.Support;

namespace Orders.Api.Tests;

/// <summary>Sobe a API real com Cosmos e Service Bus trocados por dublês em memória.</summary>
public sealed class OrdersApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key-0123456789";

    public InMemoryOrderRepository Repository { get; } = new();

    public RecordingPublisher Publisher { get; } = new();

    public FixedTimeProvider Clock { get; } = new();

    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:ApiKey", ApiKey);
        // Formato válido para a validação de opções; nenhuma conexão é aberta, os serviços são substituídos.
        builder.UseSetting("Cosmos:ConnectionString", "AccountEndpoint=http://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;");
        builder.UseSetting("ServiceBus:ConnectionString", "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOrderRepository>();
            services.RemoveAll<IOrderEventPublisher>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IOrderRepository>(Repository);
            services.AddSingleton<IOrderEventPublisher>(Publisher);
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

public sealed class RecordingPublisher : IOrderEventPublisher
{
    private readonly List<OrderCreated> _published = [];

    public IReadOnlyList<OrderCreated> Published => _published;

    public bool Fail { get; set; }

    public Task PublishAsync(OrderCreated orderCreated, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Service Bus unavailable.");
        }
        lock (_published)
        {
            _published.Add(orderCreated);
        }
        return Task.CompletedTask;
    }
}
