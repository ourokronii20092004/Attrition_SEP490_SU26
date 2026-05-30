using System.Linq.Expressions;

namespace BuildingBlocks.Persistence;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Inserts an entity, returning false (instead of throwing) when the insert violates a unique
    /// constraint. Lets callers translate a TOCTOU duplicate race into a friendly result.
    /// </summary>
    Task<bool> TryAddAsync(T entity);

    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    Task<(List<T> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        params Expression<Func<T, object>>[] includes);

    // Unbounded read for "get all matching" / aggregate use. No paging clamp; not change-tracked.
    Task<List<T>> ListAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        params Expression<Func<T, object>>[] includes);
}
