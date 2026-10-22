using System.ComponentModel.DataAnnotations;

namespace Orders.Api.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Auth";
    public const string HeaderName = "X-Api-Key";

    [Required]
    [MinLength(16)]
    public string ApiKey { get; set; } = string.Empty;
}
