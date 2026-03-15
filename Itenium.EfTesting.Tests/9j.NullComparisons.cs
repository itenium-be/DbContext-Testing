using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class NullComparisons
{
    public class PersonEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? MiddleName { get; set; }
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<PersonEntity> People { get; init; }
    }

    [Test]
    public async Task InMemoryMockDbSet_CSharpNullSemantics()
    {
        var people = new[]
        {
            new PersonEntity { Id = 1, Name = "Alice", MiddleName = null },
            new PersonEntity { Id = 2, Name = "Bob", MiddleName = "James" },
            new PersonEntity { Id = 3, Name = "Carol", MiddleName = "Anne" }
        }.BuildMockDbSet();

        var notJames = await people.Where(p => p.MiddleName != "James").ToArrayAsync();
        Assert.That(notJames.Length, Is.EqualTo(2));
    }

    [Test]
    public async Task InMemoryDatabase_CSharpNullSemantics()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.People.AddAsync(new PersonEntity { Id = 1, Name = "Alice", MiddleName = null });
        await context.People.AddAsync(new PersonEntity { Id = 2, Name = "Bob", MiddleName = "James" });
        await context.People.AddAsync(new PersonEntity { Id = 3, Name = "Carol", MiddleName = "Anne" });
        await context.SaveChangesAsync();

        var notJames = await context.People.Where(p => p.MiddleName != "James").ToArrayAsync();
        Assert.That(notJames.Length, Is.EqualTo(2));
    }

    [Test]
    public async Task SqliteDatabase_SqlNullSemantics()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.People.AddAsync(new PersonEntity { Id = 1, Name = "Alice", MiddleName = null });
        await context.People.AddAsync(new PersonEntity { Id = 2, Name = "Bob", MiddleName = "James" });
        await context.People.AddAsync(new PersonEntity { Id = 3, Name = "Carol", MiddleName = "Anne" });
        await context.SaveChangesAsync();

        var notJames = await context.People.Where(p => p.MiddleName != "James").ToArrayAsync();
        Assert.That(notJames.Length, Is.EqualTo(2));
    }

    [Test]
    public async Task SqlServerTestContainer_SqlNullSemantics()
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

        await context.People.AddAsync(new PersonEntity { Name = "Alice", MiddleName = null });
        await context.People.AddAsync(new PersonEntity { Name = "Bob", MiddleName = "James" });
        await context.People.AddAsync(new PersonEntity { Name = "Carol", MiddleName = "Anne" });
        await context.SaveChangesAsync();

        var notJames = await context.People.Where(p => p.MiddleName != "James").ToArrayAsync();
        Assert.That(notJames.Length, Is.EqualTo(2));
    }
}
