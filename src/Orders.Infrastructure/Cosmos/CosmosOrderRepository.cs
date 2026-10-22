using System.Net;
using Microsoft.Azure.Cosmos;
using Orders.Domain;
using Orders.Domain.Abstractions;

namespace Orders.Infrastructure.Cosmos;

internal sealed class CosmosOrderRepository(Container container) : IOrderRepository
{
    public async Task<bool> TryAddAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            await container.CreateItemAsync(
                OrderDocument.FromDomain(order),
                new PartitionKey(order.Id),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public async Task<Order?> GetAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<OrderDocument>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @customerId")
            .WithParameter("@customerId", customerId);

        var orders = new List<Order>();
        using var iterator = container.GetItemQueryIterator<OrderDocument>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var document in await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false))
            {
                orders.Add(document.ToDomain());
            }
        }

        return [.. orders.OrderByDescending(o => o.CreatedAt)];
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            await container.ReplaceItemAsync(
                OrderDocument.FromDomain(order),
                order.Id,
                new PartitionKey(order.Id),
                new ItemRequestOptions { IfMatchEtag = order.ConcurrencyToken },
                cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new ConcurrencyConflictException($"Order {order.Id} was modified concurrently.", ex);
        }
    }
}
