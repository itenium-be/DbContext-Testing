using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

/// <summary>
/// Demonstrates DateTimeKind loss when using real databases.
/// In-memory: DateTimeKind is preserved (Utc stays Utc)
/// Database: DateTimeKind is lost (Utc becomes Unspecified)
/// </summary>
public class DateTimeKindLoss
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

    private static readonly DateTime UtcDateTime = new(2024, 6, 15, 14, 30, 45, DateTimeKind.Utc);

    [Test]
    public async Task InMemoryMockDbSet_PreservesDateTimeKind()
    {
        var events = new[]
        {
            new EventEntity { Id = 1, Timestamp = UtcDateTime }
        }.BuildMockDbSet();

        var evt = await events.FirstAsync();

        Assert.That(evt.Timestamp.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(evt.Timestamp, Is.EqualTo(UtcDateTime));
    }

    [Test]
    public async Task InMemoryDatabase_PreservesDateTimeKind()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Events.AddAsync(new EventEntity { Id = 1, Timestamp = UtcDateTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        Assert.That(evt.Timestamp.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(evt.Timestamp, Is.EqualTo(UtcDateTime));
    }

    [Test]
    public async Task SqliteDatabase_LosesDateTimeKind()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Events.AddAsync(new EventEntity { Id = 1, Timestamp = UtcDateTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        // DateTimeKind is lost - Utc becomes Unspecified
        Assert.That(evt.Timestamp.Kind, Is.Not.EqualTo(DateTimeKind.Utc));
        Assert.That(evt.Timestamp.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        // Dangerous: DateTime.Equals ignores Kind, so this passes even though Kind differs!
        Assert.That(evt.Timestamp, Is.EqualTo(UtcDateTime));
    }

    [Test]
    public async Task SqlServerTestContainer_LosesDateTimeKind()
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

        await context.Events.AddAsync(new EventEntity { Timestamp = UtcDateTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        // DateTimeKind is lost - Utc becomes Unspecified
        Assert.That(evt.Timestamp.Kind, Is.Not.EqualTo(DateTimeKind.Utc));
        Assert.That(evt.Timestamp.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        // Dangerous: DateTime.Equals ignores Kind, so this passes even though Kind differs!
        Assert.That(evt.Timestamp, Is.EqualTo(UtcDateTime));
    }
}

/// <summary>
/// Demonstrates the dangerous consequence of DateTimeKind loss:
/// Converting Unspecified to UTC using a specific timezone double-converts!
///
/// Scenario: Store UTC time → retrieve (Kind=Unspecified) → convert to UTC assuming local
/// In-memory: Kind preserved, conversion is no-op, correct result
/// Database: Kind=Unspecified, code assumes local timezone, WRONG conversion applied
/// </summary>
public class DateTimeLocalTimezonePitfall
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

    // Belgium timezone (UTC+1 in winter, UTC+2 in summer)
    private static readonly TimeZoneInfo BelgiumTz = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

    // 14:30 UTC on June 15 - in Belgium (UTC+2 summer time) this is 16:30 local
    private static readonly DateTime UtcTime = new(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

    private static DateTime ConvertToUtcAssumingBelgiumLocal(DateTime dt)
    {
        // Simulates what code might do: assume the datetime is in local (Belgium) time
        // and convert to UTC. This is correct for Local times, WRONG for already-UTC times.
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), BelgiumTz);
    }

    [Test]
    public async Task InMemoryDatabase_KindPreserved_ConversionSkipped()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Events.AddAsync(new EventEntity { Id = 1, Timestamp = UtcTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        // Kind is preserved - code can check and skip conversion
        Assert.That(evt.Timestamp.Kind, Is.EqualTo(DateTimeKind.Utc));

        // Correct behavior: check Kind before converting
        var result = evt.Timestamp.Kind == DateTimeKind.Utc
            ? evt.Timestamp
            : ConvertToUtcAssumingBelgiumLocal(evt.Timestamp);

        Assert.That(result, Is.EqualTo(UtcTime));
        Assert.That(result.Hour, Is.EqualTo(14));
    }

    [Test]
    public async Task SqliteDatabase_KindLost_WrongConversion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Events.AddAsync(new EventEntity { Id = 1, Timestamp = UtcTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        // Kind is Unspecified - code cannot tell it's already UTC!
        Assert.That(evt.Timestamp.Kind, Is.EqualTo(DateTimeKind.Unspecified));

        // Bug: code assumes Belgium local time and converts to UTC
        // 14:30 "Belgium local" → 12:30 UTC (summer time, UTC+2)
        var wrongResult = ConvertToUtcAssumingBelgiumLocal(evt.Timestamp);

        Assert.That(wrongResult, Is.Not.EqualTo(UtcTime));
        Assert.That(wrongResult.Hour, Is.EqualTo(12)); // 2 hours earlier - WRONG!
    }

    [Test]
    public async Task SqlServerTestContainer_KindLost_WrongConversion()
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

        await context.Events.AddAsync(new EventEntity { Timestamp = UtcTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        // Kind is Unspecified - code cannot tell it's already UTC!
        Assert.That(evt.Timestamp.Kind, Is.EqualTo(DateTimeKind.Unspecified));

        // Bug: code assumes Belgium local time and converts to UTC
        // 14:30 "Belgium local" → 12:30 UTC (summer time, UTC+2)
        var wrongResult = ConvertToUtcAssumingBelgiumLocal(evt.Timestamp);

        Assert.That(wrongResult, Is.Not.EqualTo(UtcTime));
        Assert.That(wrongResult.Hour, Is.EqualTo(12)); // 2 hours earlier - WRONG!
    }
}

/// <summary>
/// Demonstrates precision loss with SQL Server's legacy 'datetime' type.
/// datetime2: 100-nanosecond precision (same as .NET)
/// datetime: 3.33ms precision (rounds to .000, .003, .007)
/// </summary>
public class DateTimePrecisionLoss
{
    public class LegacyEventEntity
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class LegacyDbContext(DbContextOptions<LegacyDbContext> options) : DbContext(options)
    {
        public DbSet<LegacyEventEntity> Events { get; init; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LegacyEventEntity>()
                .Property(e => e.Timestamp)
                .HasColumnType("datetime"); // Legacy type with 3.33ms precision
        }
    }

    private static readonly DateTime PreciseTime = new(2024, 6, 15, 14, 30, 45, 125, DateTimeKind.Utc);

    [Test]
    public async Task InMemoryDatabase_PreservesMilliseconds()
    {
        var options = new DbContextOptionsBuilder<LegacyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new LegacyDbContext(options);
        await context.Events.AddAsync(new LegacyEventEntity { Id = 1, Timestamp = PreciseTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        Assert.That(evt.Timestamp.Millisecond, Is.EqualTo(125));
    }

    [Test]
    public async Task SqlServerTestContainer_LegacyDateTimeRoundsMilliseconds()
    {
        await using var container = new MsSqlBuilder()
            .WithPassword("YourStrong@Passw0rd")
            .Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<LegacyDbContext>()
            .UseSqlServer(container.GetConnectionString())
            .Options;

        await using var context = new LegacyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Events.AddAsync(new LegacyEventEntity { Timestamp = PreciseTime });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var evt = await context.Events.FirstAsync();

        // datetime rounds to .000, .003, .007, .010, .013, etc.
        // 125ms rounds to 127ms (.127)
        Assert.That(evt.Timestamp.Millisecond, Is.Not.EqualTo(125));
        Assert.That(evt.Timestamp.Millisecond, Is.EqualTo(127));
    }
}
