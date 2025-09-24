using Itenium.EfTesting.Context;
using Itenium.EfTesting.Context.DbSetContext;
using Microsoft.EntityFrameworkCore;

namespace Itenium.EfTesting.Tests;

/// <summary>
/// Option 3:
/// InMemoryDatabase Provider
///
/// ✅ Still very fast
/// ✅ Easy to have the test data completely under control
/// ✅ Uses your actual DbContext
/// </summary>
/// <remarks>
/// Not recommended by the EF Team:
/// ⚠️ They would like to remove this feature (but probably won't)
/// ⚠️ Which is why something like ExecuteDeleteAsync is not yet supported?
/// ⚠️ Does not work with custom db functions
/// ⚠️ Does not take into account other database specifics, like case-sensitivity
/// </remarks>
public class InMemoryDatabase
{
    [Test]
    public async Task TestService()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());

        var context = new StoreDbContext(options.Options);
        await context.Products.AddAsync(new ProductEntity() { Id = 100, WarehouseId = 1, Stock = 5 });
        await context.Products.AddAsync(new ProductEntity() { Id = 101, WarehouseId = 2, Stock = 3 });
        await context.SaveChangesAsync();

        var service = new ProductService(context);
        await service.WarehouseBurnedDown(1);

        var warehouse1 = await context.Products.Where(x => x.WarehouseId == 1).ToArrayAsync();
        Assert.That(warehouse1, Has.All.Matches<ProductEntity>(x => x.Stock == 0));

        var warehouse2 = await context.Products.Where(x => x.WarehouseId != 1).ToArrayAsync();
        Assert.That(warehouse2, Has.All.Matches<ProductEntity>(x => x.Stock != 0));
    }

    [Test]
    public async Task RemoveWorks()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());

        var context = new StoreDbContext(options.Options);
        var prod1 = new ProductEntity() { Id = 100, WarehouseId = 1, Stock = 5 };
        await context.Products.AddAsync(prod1);
        await context.Products.AddAsync(new ProductEntity() { Id = 101, WarehouseId = 2, Stock = 3 });
        await context.SaveChangesAsync();

        var allRecords = await context.Products.CountAsync();
        Assert.That(allRecords, Is.EqualTo(2));

        context.Products.Remove(prod1);
        await context.SaveChangesAsync();
        allRecords = await context.Products.CountAsync();
        Assert.That(allRecords, Is.EqualTo(1));
    }
}
