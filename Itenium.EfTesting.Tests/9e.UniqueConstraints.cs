using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class UniqueConstraints
{
    public class UserEntity
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<UserEntity> Users { get; init; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }

    [Test]
    public async Task InMemoryMockDbSet_AllowsDuplicateUniqueColumn()
    {
        var users = new[]
        {
            new UserEntity { Id = 1, Email = "test@example.com", Name = "Alice" },
            new UserEntity { Id = 2, Email = "test@example.com", Name = "Bob" }
        }.BuildMockDbSet();

        var count = await users.CountAsync(u => u.Email == "test@example.com");
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task InMemoryDatabase_AllowsDuplicateUniqueColumn()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.Users.AddAsync(new UserEntity { Id = 1, Email = "test@example.com", Name = "Alice" });
        await context.Users.AddAsync(new UserEntity { Id = 2, Email = "test@example.com", Name = "Bob" });
        await context.SaveChangesAsync();

        var count = await context.Users.CountAsync(u => u.Email == "test@example.com");
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task SqliteDatabase_ThrowsOnDuplicateUniqueColumn()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Users.AddAsync(new UserEntity { Id = 1, Email = "test@example.com", Name = "Alice" });
        await context.SaveChangesAsync();

        await context.Users.AddAsync(new UserEntity { Id = 2, Email = "test@example.com", Name = "Bob" });

        var ex = Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
        Assert.That(ex.InnerException?.Message, Does.Contain("UNIQUE"));
    }

    [Test]
    public async Task SqlServerTestContainer_ThrowsOnDuplicateUniqueColumn()
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

        await context.Users.AddAsync(new UserEntity { Email = "test@example.com", Name = "Alice" });
        await context.SaveChangesAsync();

        await context.Users.AddAsync(new UserEntity { Email = "test@example.com", Name = "Bob" });

        var ex = Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
        Assert.That(ex.InnerException?.Message, Does.Contain("unique").IgnoreCase.Or.Contain("duplicate").IgnoreCase.Or.Contain("IX_Users_Email"));
    }
}
