using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly DbContext _db;
    protected readonly DbSet<T> _dbSet;

    public Repository(DbContext db)
    {
        _db = db;
        _dbSet = db.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);
    public virtual async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
    public virtual async Task<T?> GetByIdAsync(string id) => await _dbSet.FindAsync(id);

    public virtual async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.AsNoTracking().ToListAsync();

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Inserts an entity, returning false when a unique-constraint violation occurs instead of
    /// letting DbUpdateException bubble up as a 500. The failed entity is detached so the context
    /// isn't left poisoned for subsequent operations.
    /// </summary>
    public virtual async Task<bool> TryAddAsync(T entity)
    {
        try
        {
            await _dbSet.AddAsync(entity);
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            _db.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public virtual async Task UpdateAsync(T entity)
    {
        var entry = _db.Entry(entity);
        if (entry.State == EntityState.Detached)
            _dbSet.Attach(entity);
        if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
            entry.State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        => predicate != null ? await _dbSet.CountAsync(predicate) : await _dbSet.CountAsync();

    public virtual async Task<(List<T> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        params Expression<Func<T, object>>[] includes)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<T> query = _dbSet.AsNoTracking();

        if (filter != null)
            query = query.Where(filter);

        foreach (var include in includes)
            query = query.Include(include);

        int totalCount = await query.CountAsync();

        // Clamp the requested page to the last real page so a hostile/absurd page number
        // (e.g. ?page=500000) can't make Postgres scan-and-discard tens of millions of rows
        // via a huge OFFSET. Past the end → an empty page with no deep skip.
        if (totalCount == 0)
            return (new List<T>(), 0);
        var lastPage = (totalCount + pageSize - 1) / pageSize;
        if (page > lastPage)
            return (new List<T>(), totalCount);

        if (orderBy != null)
            query = orderBy(query);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public virtual async Task<List<T>> ListAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        if (filter != null)
            query = query.Where(filter);

        foreach (var include in includes)
            query = query.Include(include);

        if (orderBy != null)
            query = orderBy(query);

        return await query.ToListAsync();
    }
}
