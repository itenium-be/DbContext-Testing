using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class DateTimePrecision
{
    public class EventEntity
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<EventEntity> Events { get; init; }
    }

    private static readonly DateTime PreciseDateTime = new(2024, 6, 15, 14, 30, 45, 123, DateTimeKind.Utc);

    [Test]
    public async Task InMemoryMockDbSet_FullNetPrecision()
    {
        var events = new[]
        {
            new EventEntity { Id = 1, Timestamp = PreciseDateTime.AddTicks(4567) }
        }.BuildMockDbSet();

        var evt = await events.FirstAsync();
        Assert.That(evt.Timestamp.Ticks % 10000, Is.EqualTo(4567));
    }

    [Test]
    public async Task InMemoryDatabase_FullNetPrecision()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);
        var preciseTime = PreciseDateTime.AddTicks(4567);

        await context.Events.AddAsync(new EventEntity { Id = 1, Timestamp = preciseTime });
        await context.SaveChangesAsync();

        var evt = await context.Events.FirstAsync();
        Assert.That(evt.Timestamp, Is.EqualTo(preciseTime));
    }

    [Test]
    public async Task SqliteDatabase_TextStorageLimitedPrecision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var preciseTime = PreciseDateTime.AddTicks(4567);
        await context.Events.AddAsync(new EventEntity { Id = 1, Timestamp = preciseTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        Assert.That(evt.Timestamp, Is.EqualTo(preciseTime).Within(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public async Task SqlServerTestContainer_DateTime2HighPrecision()
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

        var preciseTime = PreciseDateTime.AddTicks(4567);
        await context.Events.AddAsync(new EventEntity { Timestamp = preciseTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        Assert.That(evt.Timestamp, Is.EqualTo(preciseTime).Within(TimeSpan.FromMicroseconds(1)));
    }
}
