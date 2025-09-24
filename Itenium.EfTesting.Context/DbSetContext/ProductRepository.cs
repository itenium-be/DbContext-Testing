using Microsoft.EntityFrameworkCore;

namespace Itenium.EfTesting.Context.DbSetContext;

public class ProductRepository(StoreDbContext db) : IProduct
{
    public async Task Save(ProductDto product)
    {
        var entity = ToEntity(product);
        if (product.Id == 0)
        {
            await db.Products.AddAsync(entity);
        }
        else
        {
            db.Products.Update(entity);
        }
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProductDto>> Find(ProductFilter filter, CancellationToken cancellationToken)
    {
        var products = await db.Products
            .Where(x => x.WarehouseId == filter.WarehouseId)
            .ToArrayAsync(cancellationToken);
        return products.Select(ToDto);
    }

    public async Task<IEnumerable<ProductDto>> FindAll(CancellationToken cancellationToken)
    {
        var products = await db.Products.ToListAsync(cancellationToken: cancellationToken);
        var result = products.Select(ToDto);
        return result;
    }

    public async Task SaveChangesAsync()
    {
        await db.SaveChangesAsync();
    }

    private static ProductEntity ToEntity(ProductDto dto)
    {
        return new ProductEntity()
        {
            Id = dto.Id,
            Name = dto.Name,
            Category = dto.Category,
            Price = dto.Price,
            Stock = dto.Stock,
            WarehouseId = dto.WarehouseId,
        };
    }

    private static ProductDto ToDto(ProductEntity entity)
    {
        return new ProductDto()
        {
            Id = entity.Id,
            Name = entity.Name,
            Category = entity.Category,
            Price = entity.Price,
            Stock = entity.Stock,
            WarehouseId = entity.WarehouseId,
        };
    }
}
