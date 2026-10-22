using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Orders.Api.Endpoints;

/// <summary>
/// Com o header Idempotency-Key, o Id do pedido é derivado de (cliente, chave): repetir a mesma
/// requisição cai no mesmo documento em vez de criar um pedido duplicado.
/// </summary>
internal static partial class IdempotencyKey
{
    public const string HeaderName = "Idempotency-Key";

    public static bool IsValid(string key) => KeyFormat().IsMatch(key);

    public static string ToOrderId(string customerId, string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{customerId}\n{key}"));
        return Convert.ToHexStringLower(hash)[..32];
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{8,100}$")]
    private static partial Regex KeyFormat();
}
