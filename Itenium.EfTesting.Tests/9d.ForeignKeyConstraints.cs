using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class ForeignKeyConstraints
{
    public class OrderEntity
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = "";
        public List<OrderItemEntity> Items { get; set; } = [];
    }

    public class OrderItemEntity
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public OrderEntity? Order { get; set; }
        public string ProductName { get; set; } = "";
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<OrderEntity> Orders { get; init; }
        public DbSet<OrderItemEntity> OrderItems { get; init; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItemEntity>()
                .HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId);
        }
    }

    [Test]
    public async Task InMemoryMockDbSet_AllowsOrphanedForeignKey()
    {
        var items = new[]
        {
            new OrderItemEntity { Id = 1, OrderId = 999, ProductName = "Widget" }
        }.BuildMockDbSet();

        var orphanedItem = await items.FirstAsync();
        Assert.That(orphanedItem.OrderId, Is.EqualTo(999));
    }

    [Test]
    public async Task InMemoryDatabase_AllowsOrphanedForeignKey()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.OrderItems.AddAsync(new OrderItemEntity { Id = 1, OrderId = 999, ProductName = "Widget" });
        await context.SaveChangesAsync();

        var orphanedItem = await context.OrderItems.FirstAsync();
        Assert.That(orphanedItem.OrderId, Is.EqualTo(999));
    }

    [Test]
    public async Task SqliteDatabase_ThrowsOnOrphanedForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.OrderItems.AddAsync(new OrderItemEntity { Id = 1, OrderId = 999, ProductName = "Widget" });

        var ex = Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
        Assert.That(ex.InnerException?.Message, Does.Contain("FOREIGN KEY"));
    }

    [Test]
    public async Task SqlServerTestContainer_ThrowsOnOrphanedForeignKey()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlServer(container.GetConnectionString())
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.OrderItems.AddAsync(new OrderItemEntity { OrderId = 999, ProductName = "Widget" });

        var ex = Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
        Assert.That(ex.InnerException?.Message, Does.Contain("FOREIGN KEY"));
    }
}
