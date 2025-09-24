using Microsoft.EntityFrameworkCore;

namespace Itenium.EfTesting.Context.DbSetContext;

public class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options), IStoreDbContext
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
}

public interface IStoreDbContext
{
    DbSet<ProductEntity> Products { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
