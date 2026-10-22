using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orders.Domain.Abstractions;
using Orders.Infrastructure.Cosmos;
using Orders.Infrastructure.Messaging;

namespace Orders.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CosmosOptions>()
            .Bind(configuration.GetSection(CosmosOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;
            var clientOptions = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            };
            if (settings.UseGatewayMode)
            {
                clientOptions.ConnectionMode = ConnectionMode.Gateway;
                clientOptions.LimitToEndpoint = true;
            }
            return new CosmosClient(settings.ConnectionString, clientOptions);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;
            return sp.GetRequiredService<CosmosClient>().GetContainer(settings.DatabaseName, settings.ContainerName);
        });

        services.AddSingleton<IOrderRepository, CosmosOrderRepository>();
        services.AddHostedService<CosmosInitializer>();
        return services;
    }

    public static IServiceCollection AddServiceBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceBusOptions>()
            .Bind(configuration.GetSection(ServiceBusOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp => new ServiceBusClient(sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value.ConnectionString));
        return services;
    }

    public static IServiceCollection AddOrderEventPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceBus(configuration);
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
            return sp.GetRequiredService<ServiceBusClient>().CreateSender(settings.QueueName);
        });
        services.AddSingleton<IOrderEventPublisher, ServiceBusOrderEventPublisher>();
        return services;
    }
}
