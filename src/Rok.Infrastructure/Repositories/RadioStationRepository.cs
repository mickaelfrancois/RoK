using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class RadioStationRepository(IDbConnection db, ILogger<RadioStationRepository> logger, TimeProvider timeProvider)
    : IRadioStationRepository
{
    private readonly IDbConnection _db = Guard.NotNull(db);
    private readonly ILogger<RadioStationRepository> _logger = Guard.NotNull(logger);
    private readonly TimeProvider _timeProvider = Guard.NotNull(timeProvider);

    private const string InsertSql = """
        INSERT INTO RadioStations (Name, StreamUrl, HomepageUrl, StationUuid, FaviconUrl, CountryCode, Codec, Bitrate, AddedAt, LastListen)
        VALUES (@Name, @StreamUrl, @HomepageUrl, @StationUuid, @FaviconUrl, @CountryCode, @Codec, @Bitrate, @AddedAt, @LastListen);
        SELECT last_insert_rowid();
        """;

    private const string SelectAllSql = """
        SELECT Id, Name, StreamUrl, HomepageUrl, StationUuid, FaviconUrl, CountryCode, Codec, Bitrate, AddedAt, LastListen
        FROM RadioStations
        ORDER BY LastListen DESC NULLS LAST, AddedAt DESC
        """;

    private const string SelectByIdSql = """
        SELECT Id, Name, StreamUrl, HomepageUrl, StationUuid, FaviconUrl, CountryCode, Codec, Bitrate, AddedAt, LastListen
        FROM RadioStations WHERE Id = @Id
        """;

    private const string SelectByUrlSql = """
        SELECT Id, Name, StreamUrl, HomepageUrl, StationUuid, FaviconUrl, CountryCode, Codec, Bitrate, AddedAt, LastListen
        FROM RadioStations WHERE StreamUrl = @StreamUrl
        """;

    private const string UpdateSql = """
        UPDATE RadioStations
        SET Name = @Name,
            StreamUrl = @StreamUrl,
            HomepageUrl = @HomepageUrl
        WHERE Id = @Id
        """;

    private const string DeleteSql = "DELETE FROM RadioStations WHERE Id = @Id";

    private const string TouchSql = "UPDATE RadioStations SET LastListen = @LastListen WHERE Id = @Id";

    public async Task<long> AddAsync(RadioStationEntity station, CancellationToken cancellationToken)
    {
        if (station.AddedAt == default)
            station.AddedAt = _timeProvider.GetUtcNow().UtcDateTime;

        long id = await _db.ExecuteScalarAsync<long>(new CommandDefinition(InsertSql, station, cancellationToken: cancellationToken));

        return id;
    }

    public Task<RadioStationEntity?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        _db.QueryFirstOrDefaultAsync<RadioStationEntity?>(new CommandDefinition(SelectByIdSql, new { Id = id }, cancellationToken: cancellationToken));

    public Task<RadioStationEntity?> GetByUrlAsync(string streamUrl, CancellationToken cancellationToken) =>
        _db.QueryFirstOrDefaultAsync<RadioStationEntity?>(new CommandDefinition(SelectByUrlSql, new { StreamUrl = streamUrl }, cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<RadioStationEntity>> ListAsync(CancellationToken cancellationToken)
    {
        IEnumerable<RadioStationEntity> rows = await _db.QueryAsync<RadioStationEntity>(new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public Task UpdateAsync(long id, string name, string streamUrl, string? homepageUrl, CancellationToken cancellationToken) =>
        _db.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new { Id = id, Name = name, StreamUrl = streamUrl, HomepageUrl = homepageUrl },
            cancellationToken: cancellationToken));

    public Task DeleteAsync(long id, CancellationToken cancellationToken) =>
        _db.ExecuteAsync(new CommandDefinition(DeleteSql, new { Id = id }, cancellationToken: cancellationToken));

    public Task TouchLastListenAsync(long id, DateTime utcNow, CancellationToken cancellationToken) =>
        _db.ExecuteAsync(new CommandDefinition(TouchSql, new { Id = id, LastListen = utcNow }, cancellationToken: cancellationToken));
}