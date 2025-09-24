using Microsoft.EntityFrameworkCore;

namespace Itenium.EfTesting.Context.QueryableContext;

public class QueryableStoreDbContext(DbContextOptions<QueryableStoreDbContext> options) : DbContext(options), IQueryableStoreDbContext
{
    public IQueryable<ProductEntity> Products => Set<ProductEntity>();
}

public interface IQueryableStoreDbContext
{
    IQueryable<ProductEntity> Products { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    int SaveChanges();
}
