using Itenium.EfTesting.Context;
using Itenium.EfTesting.Context.DbSetContext;
using NSubstitute;

namespace Itenium.EfTesting.Tests;

/// <summary>
/// Option 2:
/// Skip the whole DbContext debacle and mock one level higher.
///
/// ✅ Very, very fast
/// ✅ Easy to have the test data completely under control
///
/// ⚠️ You are no longer testing the database integration nor any code in the DbContext/Repository
/// ⚠️ You have to implement the Repository pattern on top of the existing DbContext Repository pattern
/// ⚠️ There is no problem with custom db functions, but you're also not testing that functionality
/// </summary>
public class RepositoryMocking
{
    [Test]
    public async Task TestService_WithMockedIProductRepository()
    {
        var repo = Substitute.For<IProduct>();
        var prod100 = new ProductDto()
        {
            Id = 100,
            WarehouseId = 1,
            Stock = 5
        };
        var prod101 = new ProductDto()
        {
            Id = 101,
            WarehouseId = 2,
            Stock = 3
        };
        var data = new[] { prod100, prod101 };
        repo
            .Find(Arg.Any<ProductFilter>(), Arg.Any<CancellationToken>())
            .Returns(data);
        var service = new RepositoryProductService(repo);

        await service.WarehouseBurnedDown(1);

        Assert.That(prod100.Stock, Is.EqualTo(0));

        // ATTN: if our repository returns something that doesn't make sense,
        //       like in this case a Product from another Warehouse
        //       We get "strange" test results...
        Assert.That(prod101.Stock, Is.EqualTo(0));
        await repo.Received(1).SaveChangesAsync();
    }
}
