using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Infrastructure.Repositories;

public class EqualizerPresetRepository(IDbConnection db, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundDb, ILogger<EqualizerPresetRepository> logger, TimeProvider timeProvider)
    : GenericRepository<EqualizerPresetEntity>(db, backgroundDb, null, logger, timeProvider), IEqualizerPresetRepository
{
    private const string FindSql = """
        SELECT Id, Scope, ScopeId, Band0, Band1, Band2, Band3, Band4, Band5, Band6, Band7, Band8, Band9
        FROM EqualizerPreset
        WHERE Scope = @scope AND ((@scopeId IS NULL AND ScopeId IS NULL) OR ScopeId = @scopeId)
        LIMIT 1
        """;

    private const string FindDefaultSql = """
        SELECT Id, Scope, ScopeId, Band0, Band1, Band2, Band3, Band4, Band5, Band6, Band7, Band8, Band9
        FROM EqualizerPreset
        WHERE (Scope = 'Default' OR Scope LIKE 'Default_%') AND ScopeId IS NULL
        LIMIT 1
        """;

    private const string DeleteSql = """
        DELETE FROM EqualizerPreset
        WHERE Scope = @scope AND ((@scopeId IS NULL AND ScopeId IS NULL) OR ScopeId = @scopeId)
        """;

    private const string DeleteDefaultSql = """
        DELETE FROM EqualizerPreset
        WHERE (Scope = 'Default' OR Scope LIKE 'Default_%') AND ScopeId IS NULL
        """;

    private const string InsertSql = """
        INSERT INTO EqualizerPreset (Scope, ScopeId, Band0, Band1, Band2, Band3, Band4, Band5, Band6, Band7, Band8, Band9)
        VALUES (@Scope, @ScopeId, @Band0, @Band1, @Band2, @Band3, @Band4, @Band5, @Band6, @Band7, @Band8, @Band9)
        """;

    public async Task<EqualizerPresetEntity?> FindAsync(EqualizerScope scope, long? scopeId)
    {
        EqualizerPresetRow? row;

        if (scope == EqualizerScope.Default)
            row = await _connection.QueryFirstOrDefaultAsync<EqualizerPresetRow>(FindDefaultSql);
        else
            row = await _connection.QueryFirstOrDefaultAsync<EqualizerPresetRow>(FindSql, new { scope = scope.ToString(), scopeId });

        return row is null ? null : MapToEntity(row);
    }

    public async Task SaveAsync(EqualizerPresetEntity preset)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        using IDbTransaction transaction = _connection.BeginTransaction();

        if (preset.Scope == EqualizerScope.Default)
            await ExecuteNonQueryAsync(DeleteDefaultSql, transaction, new { });
        else
            await ExecuteNonQueryAsync(DeleteSql, transaction, new { scope = preset.Scope.ToString(), scopeId = preset.ScopeId });

        await ExecuteNonQueryAsync(InsertSql, transaction, BuildBandParams(preset));

        transaction.Commit();
    }

    public Task DeleteAsync(EqualizerScope scope, long? scopeId)
    {
        if (scope == EqualizerScope.Default)
            return _connection.ExecuteAsync(DeleteDefaultSql);
        else
            return _connection.ExecuteAsync(DeleteSql, new { scope = scope.ToString(), scopeId });
    }

    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
            SELECT Id, Scope, ScopeId, Band0, Band1, Band2, Band3, Band4, Band5, Band6, Band7, Band8, Band9
            FROM EqualizerPreset
            """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE {whereParam} = @{whereParam}";

        return query;
    }

    private static EqualizerPresetEntity MapToEntity(EqualizerPresetRow row)
    {
        string scopeStr = row.Scope;
        string? builtinKey = null;

        if (scopeStr.StartsWith("Default_", StringComparison.Ordinal))
        {
            builtinKey = scopeStr["Default_".Length..];
            scopeStr = "Default";
        }

        return new()
        {
            Id = row.Id,
            Scope = Enum.Parse<EqualizerScope>(scopeStr),
            BuiltinPresetKey = builtinKey,
            ScopeId = row.ScopeId,
            Bands = new float[] { row.Band0, row.Band1, row.Band2, row.Band3, row.Band4, row.Band5, row.Band6, row.Band7, row.Band8, row.Band9 }
        };
    }

    private static object BuildBandParams(EqualizerPresetEntity preset) => new
    {
        Scope = preset.Scope == EqualizerScope.Default && preset.BuiltinPresetKey is not null
            ? $"Default_{preset.BuiltinPresetKey}"
            : preset.Scope.ToString(),
        preset.ScopeId,
        Band0 = preset.Bands[0],
        Band1 = preset.Bands[1],
        Band2 = preset.Bands[2],
        Band3 = preset.Bands[3],
        Band4 = preset.Bands[4],
        Band5 = preset.Bands[5],
        Band6 = preset.Bands[6],
        Band7 = preset.Bands[7],
        Band8 = preset.Bands[8],
        Band9 = preset.Bands[9]
    };

    private sealed class EqualizerPresetRow
    {
        public int Id { get; set; } = 0;
        public string Scope { get; init; } = string.Empty;
        public long? ScopeId { get; init; }
        public float Band0 { get; init; } = 0;
        public float Band1 { get; init; } = 0;
        public float Band2 { get; init; } = 0;
        public float Band3 { get; init; } = 0;
        public float Band4 { get; init; } = 0;
        public float Band5 { get; init; } = 0;
        public float Band6 { get; init; } = 0;
        public float Band7 { get; init; } = 0;
        public float Band8 { get; init; } = 0;
        public float Band9 { get; init; } = 0;
    }
}