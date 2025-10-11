using Dapper.Contrib.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rok.Infrastructure.Repositories;

public partial class GenericRepository<T>(IDbConnection db, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, IDbTransaction? transaction, ILogger<GenericRepository<T>> logger)
                                : IRepository<T> where T : class
{
    protected readonly IDbConnection _connection = db ?? throw new ArgumentNullException(nameof(db));
    protected readonly IDbConnection _backgroundConnection = backgroundDb ?? throw new ArgumentNullException(nameof(backgroundDb));
    protected readonly IDbTransaction? _transaction = transaction;
    protected readonly ILogger<GenericRepository<T>> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<long> AddAsync(T entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.InsertAsync<T>(entity, _transaction);
    }

    public async Task<bool> UpdateAsync(T entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.UpdateAsync<T>(entity, _transaction);
    }

    public async Task<bool> DeleteAsync(T entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.DeleteAsync<T>(entity, _transaction);
    }

    public async Task<IEnumerable<T>> GetAllAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery();
        return await ExecuteQueryAsync(sql, kind);
    }

    public async Task<int> CountAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"SELECT COUNT(*) FROM {GetTableName()}";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteScalarAsync<int>(sql);
    }

    public async Task<T?> GetByIdAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery("id");
        return await QuerySingleOrDefaultAsync(sql, kind, new { id });
    }

    public async Task<T?> GetByNameAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery("name");
        return await QuerySingleOrDefaultAsync(sql, kind, new { name });
    }

    public virtual string GetSelectQuery(string? whereParam = null)
    {
        throw new NotImplementedException("GetSelectQuery method is not implemented in GenericRepository.");
    }


    public virtual string GetTableName()
    {
        throw new NotImplementedException("GetTableName method is not implemented in GenericRepository.");
    }


    protected async Task<IReadOnlyList<T>> ExecuteQueryAsync(string sql, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground, object? param = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IDbConnection localConnection = ResolveConnection(kind);
            return (await localConnection.QueryAsync<T>(new CommandDefinition(sql, param))).ToList();
        }
        finally
        {
            stopwatch.Stop();
            LogQuery(kind, sql, stopwatch, param);
        }
    }

    protected async Task<T> ExecuteScalarAsync(string sql, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground, object? param = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IDbConnection localConnection = ResolveConnection(kind);
            IEnumerable<T> result = await localConnection.QueryAsync<T>(sql, param: param);

            return result.FirstOrDefault()!;
        }
        finally
        {
            stopwatch.Stop();
            LogQuery(kind, sql, stopwatch, param);
        }
    }

    protected async Task<T?> QuerySingleOrDefaultAsync(string sql, RepositoryConnectionKind kind, object? param)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IDbConnection localConnection = ResolveConnection(kind);
            return await localConnection.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, param));
        }
        finally
        {
            stopwatch.Stop();
            LogQuery(kind, sql, stopwatch, param);
        }
    }

    protected IDbConnection ResolveConnection(RepositoryConnectionKind kind)
    {
        if (_transaction is not null)
        {
            if (kind == RepositoryConnectionKind.Background)
                _logger.LogWarning("Tentative d'utiliser la connexion background pendant une transaction : fall‑back sur la connexion principale.");
            return _connection;
        }

        return kind == RepositoryConnectionKind.Background ? _backgroundConnection : _connection;
    }

    private void LogQuery(RepositoryConnectionKind kind, string sql, Stopwatch stopwatch, object? param = null)
    {
        sql = sql.Replace("\n", " ").Replace("\r", " ").Replace("  ", " ").Replace('\t', ' ').Trim();
        sql = TrimWhiteSpace().Replace(sql, " ");

        string serializedParams;

        try
        {
            serializedParams = param is null ? "null" : JsonSerializer.Serialize(param, _jsonOptions);
        }
        catch (Exception ex)
        {
            // En cas d'échec de sérialisation, logguer l'exception mais continuer.
            _logger.LogWarning(ex, "Failed to serialize query parameters for logging.");
            serializedParams = "<unserializable>";
        }

        _logger.LogInformation("Executed SQL ({Kind}): {Sql} | Params: {Params} | Elapsed: {ElapsedMilliseconds}ms",
                               kind, sql, serializedParams, stopwatch.ElapsedMilliseconds);
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex TrimWhiteSpace();
}