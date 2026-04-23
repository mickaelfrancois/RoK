namespace Rok.Application.Interfaces;

public interface IRepository<T> where T : class
{
    Task<int> CountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<T?> GetByIdAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<T?> GetByNameAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<IEnumerable<T>> GetAllAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<long> AddAsync(T entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> UpdateAsync(T entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);

    Task<bool> DeleteAsync(T entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}