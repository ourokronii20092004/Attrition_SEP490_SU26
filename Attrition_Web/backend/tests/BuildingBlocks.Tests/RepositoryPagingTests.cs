using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BuildingBlocks.Tests;

public class RepositoryPagingTests
{
    private class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions options) : base(options) { }
        public DbSet<Widget> Widgets => Set<Widget>();
    }

    private static TestDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"repo-test-{Guid.NewGuid()}")
            .Options;
        var ctx = new TestDbContext(options);
        ctx.Widgets.AddRange(Enumerable.Range(1, 250).Select(i => new Widget { Id = i, Name = $"w{i}" }));
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task GetPagedAsync_ClampsPageSizeTo100()
    {
        await using var ctx = NewContext();
        var repo = new Repository<Widget>(ctx);

        // Caller asks for "everything" the old way; clamp must cap the page at 100...
        var (items, total) = await repo.GetPagedAsync(1, int.MaxValue);

        Assert.Equal(100, items.Count);
        // ...but total still reflects the real row count.
        Assert.Equal(250, total);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllRows()
    {
        await using var ctx = NewContext();
        var repo = new Repository<Widget>(ctx);

        var all = await repo.ListAsync();

        Assert.Equal(250, all.Count);
    }

    [Fact]
    public async Task ListAsync_AppliesFilterAndOrder()
    {
        await using var ctx = NewContext();
        var repo = new Repository<Widget>(ctx);

        var filtered = await repo.ListAsync(w => w.Id <= 5, q => q.OrderByDescending(w => w.Id));

        Assert.Equal(5, filtered.Count);
        Assert.Equal(5, filtered[0].Id);
        Assert.Equal(1, filtered[^1].Id);
    }
}
