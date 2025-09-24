using Itenium.EfTesting.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class CustomFunction
{
    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<ProductEntity> Products { get; init; }
    }

    [Test]
    public async Task SqliteDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Products.AddAsync(new ProductEntity() { Name = "React" });
        await context.Products.AddAsync(new ProductEntity() { Name = "Angular" });
        await context.SaveChangesAsync();

        var lotsOfBytes = await context.Products
            .Where(p => EF.Functions.DataLength(p.Name) > "React".Length * 2)
            .ToArrayAsync();

        Assert.That(lotsOfBytes.Length, Is.EqualTo(1));
    }

    [Test]
    public async Task SqlServerTestContainerWithBuiltInFunction()
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

        await context.Products.AddAsync(new ProductEntity() { Name = "React" });
        await context.Products.AddAsync(new ProductEntity() { Name = "Angular" });
        await context.SaveChangesAsync();

        var lotsOfBytes = await context.Products
            .Where(p => EF.Functions.DataLength(p.Name) > "React".Length * 2)
            .ToArrayAsync();

        Assert.That(lotsOfBytes.Length, Is.EqualTo(1));
    }
}
