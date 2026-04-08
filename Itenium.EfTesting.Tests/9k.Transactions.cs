using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class Transactions
{
    public class AccountEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Balance { get; set; }
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<AccountEntity> Accounts { get; init; }
    }

    [Test]
    public async Task InMemoryDatabase_NoRealTransactionSupport()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);

        await context.Accounts.AddAsync(new AccountEntity { Id = 1, Name = "Alice", Balance = 100 });
        await context.SaveChangesAsync();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await context.Database.BeginTransactionAsync());

        Assert.That(ex.Message, Does.Contain("Transactions are not supported"));
    }

    [Test]
    public async Task SqliteDatabase_TransactionRollbackWorks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await context.Accounts.AddAsync(new AccountEntity { Id = 1, Name = "Alice", Balance = 100 });
        await context.SaveChangesAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();

        var account = await context.Accounts.FindAsync(1);
        account!.Balance = 50;
        await context.SaveChangesAsync();

        await transaction.RollbackAsync();

        context.ChangeTracker.Clear();
        var reloadedAccount = await context.Accounts.FindAsync(1);
        Assert.That(reloadedAccount!.Balance, Is.EqualTo(100));
    }

    [Test]
    public async Task SqlServerTestContainer_TransactionIsolation()
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

        await context.Accounts.AddAsync(new AccountEntity { Name = "Alice", Balance = 100 });
        await context.SaveChangesAsync();

        var accountId = (await context.Accounts.FirstAsync()).Id;

        await using var transaction = await context.Database.BeginTransactionAsync();

        var account = await context.Accounts.FindAsync(accountId);
        account!.Balance = 50;
        await context.SaveChangesAsync();

        await transaction.RollbackAsync();

        context.ChangeTracker.Clear();
        var reloadedAccount = await context.Accounts.FindAsync(accountId);
        Assert.That(reloadedAccount!.Balance, Is.EqualTo(100));
    }
}
