namespace Itenium.EfTesting.Context;

public interface IProduct
{
    Task Save(ProductDto product);
    Task<IEnumerable<ProductDto>> Find(ProductFilter filter, CancellationToken cancellationToken);
    Task<IEnumerable<ProductDto>> FindAll(CancellationToken cancellationToken);
    Task SaveChangesAsync();
}

public record ProductFilter(int WarehouseId);
