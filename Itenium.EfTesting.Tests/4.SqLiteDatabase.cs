using Itenium.EfTesting.Context;
using Itenium.EfTesting.Context.DbSetContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Itenium.EfTesting.Tests;

/// <summary>
/// Option 4:
/// Actual database: SQLite
///
/// ✅ Still pretty fast
/// ✅ Better relational database behavior than InMemoryDatabase (ex: FKs)
/// ✅ Supports more EF features (ExecuteDeleteAsync, etc.)
/// ✅ Better constraint validation
///
/// ⚠️ Does not work with custom db functions
/// ⚠️ Does not take into account other database specifics, like case-sensitivity
/// </summary>
public class SqLiteDatabase
{
    [Test]
    public async Task TestService()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Or, if using migrations:
        // await context.Database.MigrateAsync();

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
}
