using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Infrastructure.Repositories;

public class ArtistRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<ArtistRepository> logger) : GenericRepository<ArtistEntity>(connection, backgroundConnection, null, logger), IArtistRepository
{
    public async Task<IEnumerable<IArtistEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        name = $"%{name}%";
        string sql = GetSelectQuery() + " WHERE artists.name LIKE @name";

        return await ExecuteQueryAsync(sql, kind, new { name });
    }

    public async Task<IEnumerable<IArtistEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery() +
                     "WHERE artists.genreId = @genreId";

        return await ExecuteQueryAsync(sql, kind, new { genreId });
    }


    public async Task<bool> PatchAsync(IUpdateArtistEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE artists SET " +
            "WikipediaUrl = @WikipediaUrl," +
            "OfficialSiteUrl = @OfficialSiteUrl, " +
            "FacebookUrl = @FacebookUrl, " +
            "TwitterUrl = @TwitterUrl, " +
            "NovaUid = @NovaUid, " +
            "MusicBrainzID = @MusicBrainzID, " +
            "FormedYear = @FormedYear, " +
            "BornYear = @BornYear, " +
            "DiedYear = @DiedYear, " +
            "Disbanded = @Disbanded, " +
            "Style = @Style, " +
            "Gender = @Gender, " +
            "Mood = @Mood, " +
            "Biography = @Biography " +
            "WHERE Id = @Id";

        IDbConnection connectiondb = ResolveConnection(kind);
        int rowsAffected = await connection.ExecuteAsync(sql, new
        {
            entity.WikipediaUrl,
            entity.OfficialSiteUrl,
            entity.FacebookUrl,
            entity.TwitterUrl,
            entity.NovaUid,
            entity.MusicBrainzID,
            entity.FormedYear,
            entity.BornYear,
            entity.DiedYear,
            entity.Disbanded,
            entity.Style,
            entity.Gender,
            entity.Mood,
            entity.Biography,
            entity.Id
        }, _transaction);

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE artists SET isFavorite = @isFavorite WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { isFavorite, id }, _transaction);

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE artists SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { lastListen = DateTime.UtcNow, id }, _transaction);

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateStatisticsAsync(long id, int trackCount, long totalDurationSeconds, int albumCount, int bestOfCount, int liveCount, int compilationCount, int? yearMini, int? yearMaxi, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE artists SET trackCount = @trackCount, totalDurationSeconds = @totalDurationSeconds, albumCount = @albumCount, bestOfCount = @bestOfCount, liveCount = @liveCount, compilationCount = @compilationCount, yearMini = @yearMini, yearMaxi = @yearMaxi WHERE id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { trackCount, totalDurationSeconds, albumCount, bestOfCount, liveCount, compilationCount, yearMini, yearMaxi, id }, _transaction);

        return rowsAffected > 0;
    }


    public async Task<int> DeleteArtistsWithoutTracks(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "DELETE FROM artists WHERE id NOT IN (SELECT DISTINCT artistId FROM tracks WHERE artistId IS NOT NULL)";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(sql);
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

        if (string.IsNullOrEmpty(whereParam) == false)
            query += $" WHERE artists.{whereParam} = @{whereParam}";

        return query;
    }
}
