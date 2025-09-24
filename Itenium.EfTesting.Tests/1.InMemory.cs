using Itenium.EfTesting.Context;
using Itenium.EfTesting.Context.DbSetContext;
using Itenium.EfTesting.Context.QueryableContext;
using Microsoft.EntityFrameworkCore;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace Itenium.EfTesting.Tests;

/// <summary>
/// Option 1:
/// Use In-Memory objects
///
/// ✅ Very, very fast
/// ✅ Easy to have the test data completely under control
///
/// ⚠️ DbContext class is hard to mock
/// ⚠️ You are no longer testing the database integration
/// ⚠️ Some features don't work (for example .Include(WhereClause))
/// </summary>
/// <remarks>
/// Not recommended by the EF Team:
/// ⚠️ Does not work with custom db functions
/// ⚠️ Does not take into account other database specifics, like case-sensitivity
/// </remarks>
public class InMemory
{
    /// <summary>
    /// Synchronously with .AsQueryable()
    /// </summary>
    [Test]
    public void MockDbContext()
    {
        var context = Substitute.For<IQueryableStoreDbContext>();
        var prod100 = new ProductEntity()
        {
            Id = 100,
            WarehouseId = 1,
            Stock = 5
        };
        var prod101 = new ProductEntity()
        {
            Id = 101,
            WarehouseId = 2,
            Stock = 3
        };
        var data = new[] { prod100, prod101 };
        context.Products.Returns(data.AsQueryable());
        var service = new QueryableProductService(context);

        service.WarehouseBurnedDown(1);

        Assert.That(prod100.Stock, Is.EqualTo(0));
        Assert.That(prod101.Stock, Is.EqualTo(3));
        context.Received(1).SaveChanges();
    }

    /// <summary>
    /// Asynchronously does not work with .AsQueryable()
    /// </summary>
    [Test]
    public async Task MockDbContextAsync()
    {
        var context = Substitute.For<IQueryableStoreDbContext>();
        var prod100 = new ProductEntity()
        {
            Id = 100,
            WarehouseId = 1,
            Stock = 5
        };
        var prod101 = new ProductEntity()
        {
            Id = 101,
            WarehouseId = 2,
            Stock = 3
        };
        var data = new[] { prod100, prod101 };
        context.Products.Returns(data.AsQueryable());
        var service = new QueryableProductService(context);

        // FAILS: simple .AsQueryable() is not enough
        await service.WarehouseBurnedDownAsync(1);

        Assert.That(prod100.Stock, Is.EqualTo(0));
        Assert.That(prod101.Stock, Is.EqualTo(3));
        await context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }



    /// <summary>
    /// But we can use MockQueryable.BuildMock()
    /// </summary>
    [Test]
    public async Task MockDbContext_WithMockQueryable()
    {
        // https://github.com/romantitov/MockQueryable ⭐800
        var context = Substitute.For<IQueryableStoreDbContext>();
        var prod100 = new ProductEntity()
        {
            Id = 100,
            WarehouseId = 1,
            Stock = 5
        };
        var prod101 = new ProductEntity()
        {
            Id = 101,
            WarehouseId = 2,
            Stock = 3
        };
        var data = new[] { prod100, prod101 };
        context.Products.Returns(data.BuildMock());
        var service = new QueryableProductService(context);

        await service.WarehouseBurnedDownAsync(1);

        Assert.That(prod100.Stock, Is.EqualTo(0));
        Assert.That(prod101.Stock, Is.EqualTo(3));
        await context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MockQueryable_CanAlsoMockDbSet()
    {
        var data = new[] { new ProductEntity() };
        var productsDbSet = data.BuildMockDbSet()!;
        var repo = new TestStoreDbContext(productsDbSet);

        var products = await repo.Products.ToArrayAsync();

        Assert.That(products.Length, Is.EqualTo(1));
    }

    private class TestStoreDbContext : ActualStoreDbContext
    {
        public TestStoreDbContext(DbSet<ProductEntity> products)
        {
            Products = products;
        }
    }

    private class ActualStoreDbContext : DbContext
    {
        public DbSet<ProductEntity> Products { get; protected init; }
    }

    [Test]
    public async Task MockQueryable_InterfaceCanStillBeUseful()
    {
        var data = new[] { new ProductEntity() };
        var productsDbSet = data.BuildMockDbSet()!;
        var dbContext = Substitute.For<IStoreDbContext>();
        dbContext.Products.Returns(productsDbSet);
        var repo = new ProductInterfaceRepository(dbContext);

        var products = await repo.FindAll(default);

        Assert.That(products.Count(), Is.EqualTo(1));
    }
}
