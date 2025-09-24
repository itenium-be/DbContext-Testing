namespace Itenium.EfTesting.Context.DbSetContext;

public class RepositoryProductService(IProduct repository)
{
    public async Task WarehouseBurnedDown(int warehouse)
    {
        var products = await repository.Find(new ProductFilter(warehouse), default);
        foreach (var product in products)
        {
            product.Stock = 0;
        }
        await repository.SaveChangesAsync();
    }
}
