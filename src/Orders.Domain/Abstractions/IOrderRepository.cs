namespace Orders.Domain.Abstractions;

public interface IOrderRepository
{
    /// <summary>Grava um pedido novo. Retorna false se já existe um pedido com o mesmo Id.</summary>
    Task<bool> TryAddAsync(Order order, CancellationToken cancellationToken);

    Task<Order?> GetAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken);

    /// <summary>Atualiza o pedido. Lança <see cref="ConcurrencyConflictException"/> se ele mudou desde a leitura.</summary>
    Task UpdateAsync(Order order, CancellationToken cancellationToken);
}
