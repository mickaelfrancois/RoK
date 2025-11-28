using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Infrastructure.Repositories;

public class AlbumRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<AlbumRepository> logger) : GenericRepository<AlbumEntity>(connection, backgroundConnection, null, logger), IAlbumRepository
{
    public async Task<IEnumerable<IAlbumEntity>> SearchAsync(string name, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        name = $"%{name}%";
        string sql = GetSelectQuery() + " WHERE albums.name LIKE @name";

        return await ExecuteQueryAsync(sql, kind, new { name });
    }


    public async Task<IEnumerable<IAlbumEntity>> GetByGenreIdAsync(long genreId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery("genreId");

        return await ExecuteQueryAsync(sql, kind, new { genreId });
    }

    public async Task<IEnumerable<IAlbumEntity>> GetByArtistIdAsync(long artistId, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = GetSelectQuery("artistId") + " ORDER BY albums.year DESC";

        return await ExecuteQueryAsync(sql, kind, new { artistId });
    }

    public async Task<bool> PatchAsync(IUpdateAlbumEntity entity, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        AlbumEntity? album = await GetByIdAsync(entity.Id, kind);
        if (album == null) return false;

        if (entity.Label?.IsSet == true) album.Label = entity.Label.Value;
        if (entity.Mood?.IsSet == true) album.Mood = entity.Mood.Value;
        if (entity.MusicBrainzID?.IsSet == true) album.MusicBrainzID = entity.MusicBrainzID.Value;
        if (entity.ReleaseDate?.IsSet == true) album.ReleaseDate = entity.ReleaseDate.Value;
        if (entity.ReleaseFormat?.IsSet == true) album.ReleaseFormat = entity.ReleaseFormat.Value;
        if (entity.Sales?.IsSet == true) album.Sales = entity.Sales.Value;
        if (entity.Speed?.IsSet == true) album.Speed = entity.Speed.Value;
        if (entity.Theme?.IsSet == true) album.Theme = entity.Theme.Value;
        if (entity.Wikipedia?.IsSet == true) album.Wikipedia = entity.Wikipedia.Value;
        if (entity.IsLive?.IsSet == true) album.IsLive = entity.IsLive.Value;
        if (entity.IsBestOf?.IsSet == true) album.IsBestOf = entity.IsBestOf.Value;
        if (entity.IsCompilation?.IsSet == true) album.IsCompilation = entity.IsCompilation.Value;

        return await UpdateAsync(album, kind);
    }

    public async Task<bool> UpdateFavoriteAsync(long id, bool isFavorite, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE albums SET isFavorite = @isFavorite WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { isFavorite, id });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateLastListenAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = $"UPDATE albums SET listenCount = listenCount + 1, lastListen = @lastListen WHERE Id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { lastListen = DateTime.UtcNow, id });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateStatisticsAsync(long id, int trackCount, long duration, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE albums SET trackCount = @trackCount, duration = @duration WHERE id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { trackCount, duration, id });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateGetMetaDataLastAttemptAsync(long id, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "UPDATE albums SET getMetaDataLastAttempt = @lastAttemptDate WHERE id = @id";

        IDbConnection localConnection = ResolveConnection(kind);
        int rowsAffected = await localConnection.ExecuteAsync(sql, new { lastAttemptDate = DateTime.UtcNow, id });

        return rowsAffected > 0;
    }


    public async Task<int> DeleteAlbumsWithoutTracks(RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground)
    {
        string sql = "DELETE FROM albums WHERE id NOT IN (SELECT DISTINCT albumId FROM tracks WHERE albumId IS NOT NULL)";

        IDbConnection localConnection = ResolveConnection(kind);
        return await localConnection.ExecuteAsync(sql);
    }


    public override string GetSelectQuery(string? whereParam = null)
    {
        string query = """
                SELECT albums.*,
                     genres.name AS genreName, genres.isFavorite AS isGenreFavorite,
                     artists.name AS artistName, artists.isFavorite AS isArtistFavorite,
                     countries.code AS countryCode, countries.english AS countryName 
                     FROM albums 
                     LEFT JOIN genres ON genres.Id = albums.genreId                      
                     LEFT JOIN artists ON artists.Id = albums.artistId 
                     LEFT JOIN countries ON countries.Id = artists.countryId
                """;

        if (!string.IsNullOrEmpty(whereParam))
            query += $" WHERE albums.{whereParam} = @{whereParam}";

        return query;
    }
}
