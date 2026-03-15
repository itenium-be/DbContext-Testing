using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class DecimalPrecision
{
    public class PriceEntity
    {
        public int Id { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal Amount { get; set; }
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<PriceEntity> Prices { get; init; }
    }

    private const decimal PreciseDecimal = 123456.123456m;

    [Test]
    public async Task InMemoryMockDbSet_ExactDecimalPrecision()
    {
        var prices = new[]
        {
            new PriceEntity { Id = 1, Amount = PreciseDecimal }
        }.BuildMockDbSet();

        var price = await prices.FirstAsync();
        Assert.That(price.Amount, Is.EqualTo(PreciseDecimal));
    }

    [Test]
    public async Task InMemoryDatabase_ExactDecimalPrecision()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.Prices.AddAsync(new PriceEntity { Id = 1, Amount = PreciseDecimal });
        await context.SaveChangesAsync();

        var price = await context.Prices.FirstAsync();
        Assert.That(price.Amount, Is.EqualTo(PreciseDecimal));
    }

    [Test]
    public async Task SqliteDatabase_LosesDecimalPrecision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Prices.AddAsync(new PriceEntity { Id = 1, Amount = PreciseDecimal });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var price = await context.Prices.FirstAsync();

        Assert.That(price.Amount, Is.EqualTo(PreciseDecimal).Within(0.000001m));
    }

    [Test]
    public async Task SqlServerTestContainer_ConfigurablePrecision()
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

        await context.Prices.AddAsync(new PriceEntity { Amount = PreciseDecimal });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var price = await context.Prices.FirstAsync();

        Assert.That(price.Amount, Is.EqualTo(PreciseDecimal));
    }
}
