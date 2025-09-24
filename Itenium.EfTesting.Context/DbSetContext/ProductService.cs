using System.Threading;
using Microsoft.EntityFrameworkCore;

namespace Itenium.EfTesting.Context.DbSetContext;

public class ProductService(StoreDbContext context)
{
    public async Task WarehouseBurnedDown(int warehouse)
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
}
