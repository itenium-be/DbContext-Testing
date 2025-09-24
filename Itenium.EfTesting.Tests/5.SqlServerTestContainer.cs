using Itenium.EfTesting.Context;
using Itenium.EfTesting.Context.DbSetContext;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

/// <summary>
/// Option 5:
/// SQL Server Test Container
///
/// ✅ Full SQL Server compatibility
/// ✅ All database features supported
/// ✅ Real production-like environment
/// ✅ Custom functions, stored procedures work
/// 
/// ⚠️ Slower startup (container initialization)
/// ⚠️ Requires Docker
/// </summary>
/// <remarks>
/// Especially for a real SQLServer, you will want to
/// set up the database & run migrations only once.
///
/// Each test should start a transaction and never
/// commit it, so each test runs isolated.
/// </remarks>
public class SqlServerTestContainer
{
    [Test]
    public async Task TestService()
    {
        await using var container = new MsSqlBuilder()
            .WithPassword("YourStrong@Passw0rd")
            .Build();

        await container.StartAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlServer(container.GetConnectionString())
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Or, if using migrations:
        // await context.Database.MigrateAsync();

        await context.Products.AddAsync(new ProductEntity() { WarehouseId = 1, Stock = 5 });
        await context.Products.AddAsync(new ProductEntity() { WarehouseId = 2, Stock = 3 });
        await context.SaveChangesAsync();

        var service = new ProductService(context);
        await service.WarehouseBurnedDown(1);

        var warehouse1 = await context.Products.Where(x => x.WarehouseId == 1).ToArrayAsync();
        Assert.That(warehouse1, Has.All.Matches<ProductEntity>(x => x.Stock == 0));
        var warehouse2 = await context.Products.Where(x => x.WarehouseId != 1).ToArrayAsync();
        Assert.That(warehouse2, Has.All.Matches<ProductEntity>(x => x.Stock != 0));
    }
}
