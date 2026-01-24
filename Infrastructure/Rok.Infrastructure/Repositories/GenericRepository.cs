using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Repositories;

public partial class GenericRepository<T>(IDbConnection db, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, IDbTransaction? transaction, ILogger<GenericRepository<T>> logger)
                                : IRepository<T> where T : class
{
    private const int SlowQueryThresholdMilliseconds = 1000;
    private const int StackAllocThreshold = 256;

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
        string sql = GetSelectQuery() + GetDefaultGroupBy();

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
        string sql = GetSelectQuery("id") + GetDefaultGroupBy();
        return await QuerySingleOrDefaultAsync(sql, kind, new { id });
    }

    public async Task<T?> GetByNameAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery("name") + GetDefaultGroupBy();
        return await QuerySingleOrDefaultAsync(sql, kind, new { name });
    }

    public virtual string GetSelectQuery(string? whereParam = null)
    {
        throw new NotImplementedException("GetSelectQuery method is not implemented in GenericRepository.");
    }

    public virtual string GetDefaultGroupBy()
    {
        return string.Empty;
    }

    public virtual string GetTableName()
    {
        throw new NotImplementedException("GetTableName method is not implemented in GenericRepository.");
    }

    protected async Task<bool> ExecuteUpdateAsync(string sql, object param, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IDbConnection localConnection = ResolveConnection(kind);
            int rowsAffected = await localConnection.ExecuteAsync(sql, param, _transaction);
            return rowsAffected > 0;
        }
        finally
        {
            stopwatch.Stop();
            LogQuery(kind, sql, stopwatch, param);
        }
    }

    protected async Task<int> ExecuteNonQueryAsync(string sql, object? param = null, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IDbConnection localConnection = ResolveConnection(kind);
            return await localConnection.ExecuteAsync(sql, param, _transaction);
        }
        finally
        {
            stopwatch.Stop();
            LogQuery(kind, sql, stopwatch, param);
        }
    }

    protected async Task<int> ExecuteNonQueryAsync(string sql, IDbTransaction transaction, object? param = null, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IDbConnection localConnection = ResolveConnection(kind);
            return await localConnection.ExecuteAsync(sql, param, transaction);
        }
        finally
        {
            stopwatch.Stop();
            LogQuery(kind, sql, stopwatch, param);
        }
    }


    protected async Task<IReadOnlyList<T>> ExecuteQueryAsync(string sql, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground, object? param = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IDbConnection localConnection = ResolveConnection(kind);
            IEnumerable<T> result = await localConnection.QueryAsync<T>(new CommandDefinition(sql, param));
            return result.ToList();
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
            {
                _logger.LogCritical("Attempt to use background connection while a transaction is active. This is not allowed.");
                throw new InvalidOperationException("Cannot use background connection when a transaction is active.");
            }

            return _connection;
        }

        return kind == RepositoryConnectionKind.Background ? _backgroundConnection : _connection;
    }

    private void LogQuery(RepositoryConnectionKind kind, string sql, Stopwatch stopwatch, object? param = null)
    {
        string normalizedSql = NormalizeSql(sql);
        string serializedParams = SerializeParams(param);

        if (stopwatch.ElapsedMilliseconds > SlowQueryThresholdMilliseconds)
        {
            _logger.LogWarning("Slow SQL execution detected ({Kind}): {Sql} | Params: {Params} | Elapsed: {ElapsedMilliseconds}ms",
                               kind, normalizedSql, serializedParams, stopwatch.ElapsedMilliseconds);
            return;
        }

        _logger.LogInformation("Executed SQL ({Kind}): {Sql} | Params: {Params} | Elapsed: {ElapsedMilliseconds}ms",
                               kind, normalizedSql, serializedParams, stopwatch.ElapsedMilliseconds);
    }

    private static string NormalizeSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return string.Empty;
        }

        char[]? rentedArray = null;

        try
        {
            Span<char> buffer = sql.Length <= StackAllocThreshold
                ? stackalloc char[sql.Length]
                : rentedArray = ArrayPool<char>.Shared.Rent(sql.Length);

            int writeIndex = 0;
            bool previousWasWhitespace = false;

            for (int i = 0; i < sql.Length; i++)
            {
                char c = sql[i];

                if (char.IsWhiteSpace(c))
                {
                    if (!previousWasWhitespace && writeIndex > 0)
                    {
                        buffer[writeIndex++] = ' ';
                        previousWasWhitespace = true;
                    }
                }
                else
                {
                    buffer[writeIndex++] = c;
                    previousWasWhitespace = false;
                }
            }

            if (writeIndex > 0 && buffer[writeIndex - 1] == ' ')
            {
                writeIndex--;
            }

            return new string(buffer.Slice(0, writeIndex));
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
        }
    }

    private string SerializeParams(object? param)
    {
        if (param is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(param, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize query parameters for logging.");
            return "<unserializable>";
        }
    }
}