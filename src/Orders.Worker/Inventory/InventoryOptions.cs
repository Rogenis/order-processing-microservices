using System.ComponentModel.DataAnnotations;

namespace Orders.Worker.Inventory;

public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    [Required]
    public Uri? Url { get; set; }
}
