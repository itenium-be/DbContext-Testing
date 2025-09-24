using Microsoft.EntityFrameworkCore;

namespace Itenium.EfTesting.Context.QueryableContext;

public class QueryableProductService(IQueryableStoreDbContext context)
{
    public async Task WarehouseBurnedDownAsync(int warehouse)
    {
        var products = await context.Products
            .Where(x => x.WarehouseId == warehouse)
            .ToArrayAsync();
        foreach (var product in products)
        {
            product.Stock = 0;
        }

        await context.SaveChangesAsync();
    }

    public void WarehouseBurnedDown(int warehouse)
    {
        var products = context.Products
            .Where(x => x.WarehouseId == warehouse)
            .ToArray();
        foreach (var product in products)
        {
            product.Stock = 0;
        }

        context.SaveChanges();
    }
}
