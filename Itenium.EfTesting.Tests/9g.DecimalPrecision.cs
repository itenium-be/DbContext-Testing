using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

/// <summary>
/// Demonstrates decimal precision differences between providers.
/// In-memory/SQLite: Preserve full .NET decimal precision
/// SQL Server: Enforces configured column precision (decimal(18,6) truncates to 6 decimals)
/// </summary>
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

    // 8 decimal places, but column is decimal(18,6) - SQL Server truncates to 6
    private const decimal PreciseDecimal = 123456.12345678m;
    private const decimal TruncatedDecimal = 123456.123457m; // Rounded to 6 decimal places

    [Test]
    public async Task InMemoryMockDbSet_PreservesFullPrecision()
    {
        var prices = new[]
        {
            new PriceEntity { Id = 1, Amount = PreciseDecimal }
        }.BuildMockDbSet();

        var price = await prices.FirstAsync();

        Assert.That(price.Amount, Is.EqualTo(PreciseDecimal));
    }

    [Test]
    public async Task InMemoryDatabase_PreservesFullPrecision()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Prices.AddAsync(new PriceEntity { Id = 1, Amount = PreciseDecimal });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var price = await context.Prices.FirstAsync();

        Assert.That(price.Amount, Is.EqualTo(PreciseDecimal));
    }

    [Test]
    public async Task SqliteDatabase_PreservesFullPrecision()
    {
        // SQLite stores decimals as TEXT, preserving full precision
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

        // SQLite preserves precision - same as in-memory!
        Assert.That(price.Amount, Is.EqualTo(PreciseDecimal));
    }

    [Test]
    public async Task SqlServerTestContainer_TruncatesToColumnPrecision()
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

        // SQL Server enforces decimal(18,6) - truncates/rounds to 6 decimal places
        Assert.That(price.Amount, Is.Not.EqualTo(PreciseDecimal));
        Assert.That(price.Amount, Is.EqualTo(TruncatedDecimal));
    }
}
