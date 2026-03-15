using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class StringContainsLike
{
    public class DocumentEntity
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<DocumentEntity> Documents { get; init; }
    }

    [Test]
    public async Task InMemoryMockDbSet_ContainsIsLiteral()
    {
        var docs = new[]
        {
            new DocumentEntity { Id = 1, Title = "100% Complete" },
            new DocumentEntity { Id = 2, Title = "100 Percent" }
        }.BuildMockDbSet();

        var result = await docs.Where(d => d.Title.Contains("%")).ToArrayAsync();
        Assert.That(result.Length, Is.EqualTo(1));
    }

    [Test]
    public async Task InMemoryDatabase_ContainsIsLiteral()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.Documents.AddAsync(new DocumentEntity { Id = 1, Title = "100% Complete" });
        await context.Documents.AddAsync(new DocumentEntity { Id = 2, Title = "100 Percent" });
        await context.SaveChangesAsync();

        var result = await context.Documents.Where(d => d.Title.Contains("%")).ToArrayAsync();
        Assert.That(result.Length, Is.EqualTo(1));
    }

    [Test]
    public async Task SqliteDatabase_LikeEscapesWildcards()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Documents.AddAsync(new DocumentEntity { Id = 1, Title = "100% Complete" });
        await context.Documents.AddAsync(new DocumentEntity { Id = 2, Title = "100 Percent" });
        await context.SaveChangesAsync();

        var containsResult = await context.Documents.Where(d => d.Title.Contains("%")).ToArrayAsync();
        Assert.That(containsResult.Length, Is.EqualTo(1));

        var likeResult = await context.Documents
            .Where(d => EF.Functions.Like(d.Title, "%100%"))
            .ToArrayAsync();
        Assert.That(likeResult.Length, Is.EqualTo(2));
    }

    [Test]
    public async Task SqlServerTestContainer_ContainsVsLikeSemantics()
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

        await context.Documents.AddAsync(new DocumentEntity { Title = "100% Complete" });
        await context.Documents.AddAsync(new DocumentEntity { Title = "100 Percent" });
        await context.SaveChangesAsync();

        var containsResult = await context.Documents.Where(d => d.Title.Contains("%")).ToArrayAsync();
        Assert.That(containsResult.Length, Is.EqualTo(1));

        var likeResult = await context.Documents
            .Where(d => EF.Functions.Like(d.Title, "%100%"))
            .ToArrayAsync();
        Assert.That(likeResult.Length, Is.EqualTo(2));
    }
}
