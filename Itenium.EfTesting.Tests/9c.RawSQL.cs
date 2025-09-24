using Itenium.EfTesting.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class RawSql
{
    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<ProductEntity> Products { get; init; }

        public async Task<IEnumerable<ProductEntity>> GetOverstock()
        {
            return await Products
                .FromSqlRaw("SELECT * FROM Products WHERE Stock > 10")
                .ToArrayAsync();
        }
    }

    /// <summary>
    /// Executing even simple SQL is not going to work for InMemoryDatabase...
    /// </summary>
    [Test]
    public async Task InMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.Products.AddAsync(new ProductEntity() { Name = "React", Stock = 50 });
        await context.Products.AddAsync(new ProductEntity() { Name = "Vue", Stock = 3 });
        await context.SaveChangesAsync();

        await context.GetOverstock();
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

        await context.Products.AddAsync(new ProductEntity() { Name = "React", Stock = 50 });
        await context.Products.AddAsync(new ProductEntity() { Name = "Vue", Stock = 3 });
        await context.SaveChangesAsync();

        var products = await context.GetOverstock();

        Assert.That(products.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task SqlServerTestContainer()
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

        await context.Products.AddAsync(new ProductEntity() { Name = "React", Stock = 50 });
        await context.Products.AddAsync(new ProductEntity() { Name = "Vue", Stock = 3 });
        await context.SaveChangesAsync();

        var products = await context.GetOverstock();
        Assert.That(products.Count(), Is.EqualTo(1));
    }
}
