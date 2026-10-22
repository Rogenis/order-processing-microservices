using System.ComponentModel.DataAnnotations;

namespace Orders.Infrastructure.Cosmos;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string DatabaseName { get; set; } = "orders-db";

    [Required]
    public string ContainerName { get; set; } = "orders";

    /// <summary>
    /// O emulador local só expõe o gateway HTTP do endpoint configurado. Em produção fica false
    /// (modo Direct, que descobre as réplicas).
    /// </summary>
    public bool UseGatewayMode { get; set; }

    /// <summary>Cria banco e container na inicialização. Ligado no ambiente local; em produção a infra provisiona.</summary>
    public bool CreateIfNotExists { get; set; }
}
