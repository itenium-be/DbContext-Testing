using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MockQueryable.NSubstitute;
using Testcontainers.MsSql;

namespace Itenium.EfTesting.Tests;

public class FilteredIncludes
{
    public class BlogEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<PostEntity> Posts { get; set; } = [];
    }

    public class PostEntity
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public string Title { get; set; } = "";
        public bool IsPublished { get; set; }
    }

    private class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
    {
        public DbSet<BlogEntity> Blogs { get; init; }
        public DbSet<PostEntity> Posts { get; init; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PostEntity>()
                .HasOne<BlogEntity>()
                .WithMany(b => b.Posts)
                .HasForeignKey(p => p.BlogId);
        }

        public async Task<BlogEntity?> GetBlogWithPublishedPosts(int blogId)
        {
            return await Blogs
                .Include(b => b.Posts.Where(p => p.IsPublished))
                .FirstOrDefaultAsync(b => b.Id == blogId);
        }
    }

    [Test]
    public async Task InMemoryMockDbSet_FilteredIncludeNotSupported()
    {
        var blog = new BlogEntity
        {
            Id = 1,
            Name = "Tech Blog",
            Posts =
            [
                new PostEntity { Id = 1, BlogId = 1, Title = "Draft", IsPublished = false },
                new PostEntity { Id = 2, BlogId = 1, Title = "Published", IsPublished = true }
            ]
        };
        var blogs = new[] { blog }.BuildMockDbSet();
        var context = new StoreDbContext(new DbContextOptions<StoreDbContext>())
        {
            Blogs = blogs
        };

        var result = await context.GetBlogWithPublishedPosts(1);

        Assert.That(result!.Posts.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task InMemoryDatabase_FilteredIncludeWorks()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new StoreDbContext(options);
        var blog = new BlogEntity { Id = 1, Name = "Tech Blog" };
        await context.Blogs.AddAsync(blog);
        await context.Posts.AddAsync(new PostEntity { Id = 1, BlogId = 1, Title = "Draft", IsPublished = false });
        await context.Posts.AddAsync(new PostEntity { Id = 2, BlogId = 1, Title = "Published", IsPublished = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await context.GetBlogWithPublishedPosts(1);

        Assert.That(result!.Posts.Count, Is.EqualTo(1));
        Assert.That(result.Posts[0].Title, Is.EqualTo("Published"));
    }

    [Test]
    public async Task SqliteDatabase_FilteredIncludeWorks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new StoreDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var blog = new BlogEntity { Id = 1, Name = "Tech Blog" };
        await context.Blogs.AddAsync(blog);
        await context.Posts.AddAsync(new PostEntity { Id = 1, BlogId = 1, Title = "Draft", IsPublished = false });
        await context.Posts.AddAsync(new PostEntity { Id = 2, BlogId = 1, Title = "Published", IsPublished = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await context.GetBlogWithPublishedPosts(1);

        Assert.That(result!.Posts.Count, Is.EqualTo(1));
        Assert.That(result.Posts[0].Title, Is.EqualTo("Published"));
    }

    [Test]
    public async Task SqlServerTestContainer_FilteredIncludeWorks()
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

        var blog = new BlogEntity { Name = "Tech Blog" };
        await context.Blogs.AddAsync(blog);
        await context.SaveChangesAsync();

        await context.Posts.AddAsync(new PostEntity { BlogId = blog.Id, Title = "Draft", IsPublished = false });
        await context.Posts.AddAsync(new PostEntity { BlogId = blog.Id, Title = "Published", IsPublished = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await context.GetBlogWithPublishedPosts(blog.Id);

        Assert.That(result!.Posts.Count, Is.EqualTo(1));
        Assert.That(result.Posts[0].Title, Is.EqualTo("Published"));
    }
}
