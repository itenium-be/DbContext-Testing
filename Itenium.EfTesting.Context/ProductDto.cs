namespace Itenium.EfTesting.Context;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int WarehouseId { get; set; }

    public override string ToString() => $"{Name} ({Category})";
}
