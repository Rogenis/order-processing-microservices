using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orders.Infrastructure.Cosmos;

/// <summary>Cria banco e container no ambiente local. Tenta de novo enquanto o emulador sobe.</summary>
internal sealed class CosmosInitializer(
    CosmosClient client,
    IOptions<CosmosOptions> options,
    ILogger<CosmosInitializer> logger) : IHostedService
{
    private const int MaxAttempts = 30;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.CreateIfNotExists)
        {
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var database = await client.CreateDatabaseIfNotExistsAsync(settings.DatabaseName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await database.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties(settings.ContainerName, "/id"),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Cosmos container {Database}/{Container} ready", settings.DatabaseName, settings.ContainerName);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is CosmosException or HttpRequestException)
            {
                logger.LogWarning("Cosmos not ready (attempt {Attempt}/{Max}): {Message}", attempt, MaxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
