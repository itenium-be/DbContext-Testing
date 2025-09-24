using Itenium.EfTesting.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class CaseSensitivity
{
    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<ProductEntity> Products { get; init; }

        public async Task<IEnumerable<ProductEntity>> Filter(string filter)
        {
            return await Products.Where(p => p.Name == filter).ToArrayAsync();
        }
    }

    [Test]
    public async Task InMemoryMockDbContext()
    {
        var products = new[]
        {
            new ProductEntity() { Id = 1, Name = "React" },
        }.BuildMockDbSet();
        var context = new StoreDbContext(new DbContextOptions<StoreDbContext>())
        {
            Products = products
        };

        var exactMatch = await context.Filter("react");
        Assert.That(exactMatch.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task InMemoryMockDbSet()
    {
        var products = new[]
        {
            new ProductEntity() { Id = 1, Name = "React" },
        }.BuildMockDbSet();

        var exactMatch = await products.Where(p => p.Name == "react").ToArrayAsync();

        Assert.That(exactMatch.Length, Is.EqualTo(0));
    }

    [Test]
    public async Task InMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.Products.AddAsync(new ProductEntity() { Id = 1, Name = "React" });
        await context.SaveChangesAsync();

        var exactMatch = await context.Filter("react");
        Assert.That(exactMatch.Count(), Is.EqualTo(0));
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

        await context.Products.AddAsync(new ProductEntity() { Id = 1, Name = "React" });
        await context.SaveChangesAsync();

        var exactMatch = await context.Filter("react");
        Assert.That(exactMatch.Count(), Is.EqualTo(0));
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

        await context.Products.AddAsync(new ProductEntity() { Name = "React" });
        await context.SaveChangesAsync();

        var exactMatch = await context.Filter("react");
        Assert.That(exactMatch.Count(), Is.EqualTo(1));
    }
}
