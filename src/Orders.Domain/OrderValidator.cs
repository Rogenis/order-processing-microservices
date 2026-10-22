namespace Orders.Domain;

/// <summary>
/// Regras de entrada de um pedido. Usado pela API para responder 400 com todos os erros de uma vez,
/// e pelo próprio <see cref="Order"/> como guarda.
/// </summary>
public static class OrderValidator
{
    public const int MaxItems = 50;
    public const int MaxQuantityPerItem = 1_000;

    public static IReadOnlyDictionary<string, string[]> Validate(string? customerId, IReadOnlyList<OrderItem>? items)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(customerId))
        {
            errors["customerId"] = ["customerId is required."];
        }

        if (items is null || items.Count == 0)
        {
            errors["items"] = ["An order needs at least one item."];
            return errors;
        }

        if (items.Count > MaxItems)
        {
            errors["items"] = [$"An order can have at most {MaxItems} items."];
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var itemErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                itemErrors.Add("sku is required.");
            }
            if (item.Quantity is <= 0 or > MaxQuantityPerItem)
            {
                itemErrors.Add($"quantity must be between 1 and {MaxQuantityPerItem}.");
            }
            if (item.UnitPrice < 0)
            {
                itemErrors.Add("unitPrice cannot be negative.");
            }
            if (itemErrors.Count > 0)
            {
                errors[$"items[{i}]"] = [.. itemErrors];
            }
        }

        var duplicated = items.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicated.Count > 0)
        {
            errors["items"] = [$"Each SKU must appear once; repeated: {string.Join(", ", duplicated)}."];
        }

        return errors;
    }
}
