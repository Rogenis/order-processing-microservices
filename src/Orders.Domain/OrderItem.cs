namespace Orders.Domain;

public sealed record OrderItem(string Sku, int Quantity, decimal UnitPrice)
{
    public decimal Subtotal => Quantity * UnitPrice;
}
