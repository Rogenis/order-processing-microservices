using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Orders.Api.Security;

internal sealed class ApiKeyEndpointFilter(IOptions<ApiKeyOptions> options) : IEndpointFilter
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(options.Value.ApiKey);

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers[ApiKeyOptions.HeaderName].ToString();
        // Comparação em tempo constante: não vaza, pelo tempo de resposta, quantos caracteres batem.
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _expected))
        {
            return ValueTask.FromResult<object?>(Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Missing or invalid API key.",
                detail: $"Send a valid key in the {ApiKeyOptions.HeaderName} header."));
        }
        return next(context);
    }
}
