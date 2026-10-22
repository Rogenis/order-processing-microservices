namespace Orders.Worker.Processing;

/// <summary>
/// Espera crescente entre tentativas: 2 s, 4 s, 8 s... até 5 min. Devolver a mensagem na hora
/// gastaria todas as entregas da fila em segundos, e uma queda curta do estoque perderia o pedido.
/// </summary>
public static class RedeliveryPolicy
{
    public const string AttemptProperty = "attempt";
    public const int MaxAttempts = 8;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    /// <summary>Espera antes da tentativa seguinte, ou null quando as tentativas acabaram.</summary>
    public static TimeSpan? DelayFor(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempt);
        if (attempt >= MaxAttempts)
        {
            return null;
        }
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        return delay < MaxDelay ? delay : MaxDelay;
    }
}
