using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;
using Rok.Shared;

namespace Rok.Infrastructure.Repositories;

public class ArtistRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<ArtistRepository> logger) : GenericRepository<ArtistEntity>(connection, backgroundConnection, null, logger), IArtistRepository
{
    private const string UpdateFavoriteSql = "UPDATE artists SET isFavorite = @isFavorite WHERE Id = @id";
    private const string UpdateLastListenSql = "UPDATE artists SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";
    private const string UpdateStatisticsSql = "UPDATE artists SET trackCount = @trackCount, totalDurationSeconds = @totalDurationSeconds, albumCount = @albumCount, bestOfCount = @bestOfCount, liveCount = @liveCount, compilationCount = @compilationCount, yearMini = @yearMini, yearMaxi = @yearMaxi WHERE id = @id";
    private const string UpdateMetadataAttemptSql = "UPDATE artists SET getMetaDataLastAttempt = @lastAttemptDate WHERE id = @id";
    private const string DeleteOrphansSql = "DELETE FROM artists WHERE id NOT IN (SELECT DISTINCT artistId FROM tracks WHERE artistId IS NOT NULL)";


    public async Task<IEnumerable<IArtistEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        if (string.IsNullOrWhiteSpace(name))
            return [];

        name = $"%{name}%";
        string sql = GetSelectQuery() + " WHERE artists.name LIKE @name";

        return await ExecuteQueryAsync(sql, kind, new { name });
    }

    public async Task<IEnumerable<IArtistEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(genreId);

        string sql = GetSelectQuery() +
                     "WHERE artists.genreId = @genreId";

        return await ExecuteQueryAsync(sql, kind, new { genreId });
    }


    public async Task<bool> PatchAsync(IUpdateArtistEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArtistEntity? artist = await GetByIdAsync(entity.Id, kind);
        if (artist == null) return false;

        ApplyPatch(entity.WikipediaUrl, value => artist.WikipediaUrl = value);
        ApplyPatch(entity.OfficialSiteUrl, value => artist.OfficialSiteUrl = value);
        ApplyPatch(entity.FacebookUrl, value => artist.FacebookUrl = value);
        ApplyPatch(entity.TwitterUrl, value => artist.TwitterUrl = value);
        ApplyPatch(entity.NovaUid, value => artist.NovaUid = value);
        ApplyPatch(entity.MusicBrainzID, value => artist.MusicBrainzID = value);
        ApplyPatch(entity.FormedYear, value => artist.FormedYear = value);
        ApplyPatch(entity.BornYear, value => artist.BornYear = value);
        ApplyPatch(entity.DiedYear, value => artist.DiedYear = value);
        ApplyPatch(entity.Disbanded, value => artist.Disbanded = value);
        ApplyPatch(entity.Style, value => artist.Style = value);
        ApplyPatch(entity.Gender, value => artist.Gender = value);
        ApplyPatch(entity.Mood, value => artist.Mood = value);
        ApplyPatch(entity.Biography, value => artist.Biography = value);

        return await UpdateAsync(artist, kind);
    }

    public async Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateFavoriteSql, new { isFavorite, id }, kind);
    }

    public async Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateLastListenSql, new { lastListen = DateTime.UtcNow, id }, kind);
    }

    public async Task<bool> UpdateStatisticsAsync(long id, int trackCount, long totalDurationSeconds, int albumCount, int bestOfCount, int liveCount, int compilationCount, int? yearMini, int? yearMaxi, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateStatisticsSql, new { trackCount, totalDurationSeconds, albumCount, bestOfCount, liveCount, compilationCount, yearMini, yearMaxi, id });
    }

    public async Task<bool> UpdateGetMetaDataLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return await ExecuteUpdateAsync(UpdateMetadataAttemptSql, new { lastAttemptDate = DateTime.UtcNow, id });
    }

    public async Task<int> DeleteOrphansAsync(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        return await ExecuteNonQueryAsync(DeleteOrphansSql);
    }



    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT artists.*,
                     genres.name AS genreName, genres.isFavorite AS isGenreFavorite, 
                     countries.code AS countryCode, countries.english AS countryName
                     FROM artists 
                     LEFT JOIN genres ON genres.Id = artists.genreId 
                     LEFT JOIN countries ON countries.Id = artists.countryId 
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE artists.{whereParam} = @{whereParam}";

        return query;
    }


    private static void ApplyPatch<T>(PatchField<T>? wrapper, Action<T?> setter)
    {
        if (wrapper?.IsSet == true)
        {
            setter(wrapper.Value);
        }
    }
}
