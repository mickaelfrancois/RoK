using Rok.Application.Interfaces;
using Rok.Domain.Entities;
using System.Collections.Concurrent;

namespace Rok.Import;

public class Statistics(ITrackRepository trackRepository, IAlbumRepository albumRepository, IArtistRepository artistRepository, IGenreRepository genreRepository)
{
    private IEnumerable<TrackEntity>? _tracks;
    private IEnumerable<AlbumEntity>? _albums;
    private IEnumerable<GenreEntity>? _genres;
    private IEnumerable<ArtistEntity>? _artists;


    private async Task<IEnumerable<TrackEntity>> GetTracksCacheAsync()
    {
        _tracks ??= await trackRepository.GetAllAsync(RepositoryConnectionKind.Background);

        return _tracks;
    }

    private async Task<IEnumerable<AlbumEntity>> GetAlbumsCacheAsync()
    {
        _albums ??= await albumRepository.GetAllAsync(RepositoryConnectionKind.Background);

        return _albums;
    }

    private async Task<IEnumerable<GenreEntity>> GetGenresCacheAsync()
    {
        _genres ??= await genreRepository.GetAllAsync(RepositoryConnectionKind.Background);

        return _genres;
    }

    private async Task<IEnumerable<ArtistEntity>> GetArtistsCacheAsync()
    {
        _artists ??= await artistRepository.GetAllAsync(RepositoryConnectionKind.Background);

        return _artists;
    }


    public async Task UpdateAlbumsAsync(ConcurrentBag<long> albumsId)
    {
        if (albumsId.IsEmpty)
            return;

        IEnumerable<TrackEntity> tracks = await GetTracksCacheAsync();
        IEnumerable<AlbumEntity> albums = await GetAlbumsCacheAsync();

        foreach (long albumId in albumsId)
        {
            AlbumEntity? album = albums.FirstOrDefault(a => a.Id == albumId);
            if (album == null)
                continue;

            List<TrackEntity> albumTracks = tracks.Where(t => t.AlbumId == albumId).ToList();

            if (album.TrackCount == albumTracks.Count && album.Duration == albumTracks.Sum(t => t.Duration))
                continue; // No changes needed

            await albumRepository.UpdateStatisticsAsync(albumId, albumTracks.Count, albumTracks.Sum(t => t.Duration), RepositoryConnectionKind.Background);
        }
    }

    public async Task UpdateArtistsAsync(ConcurrentBag<long> artistsId)
    {
        if (artistsId.IsEmpty)
            return;

        IEnumerable<TrackEntity> tracks = await GetTracksCacheAsync();
        IEnumerable<AlbumEntity> albums = await GetAlbumsCacheAsync();
        IEnumerable<ArtistEntity> artists = await GetArtistsCacheAsync();

        foreach (long artistId in artistsId)
        {
            try
            {
                ArtistEntity? artist = artists.FirstOrDefault(a => a.Id == artistId);
                if (artist == null)
                    continue;

                List<TrackEntity> albumTracks = tracks.Where(t => t.ArtistId == artistId).ToList();
                List<AlbumEntity> artistAlbums = albums.Where(a => a.ArtistId == artistId).ToList();

                int trackCount = albumTracks.Count;
                long totalDurationSeconds = albumTracks.Sum(t => t.Duration);
                int albumCount = artistAlbums.Count(a => !a.IsCompilation && !a.IsLive && !a.IsBestOf);
                int bestofCount = artistAlbums.Count(a => a.IsBestOf && !a.IsCompilation);
                int liveCount = artistAlbums.Count(a => a.IsLive && !a.IsCompilation);
                int compilationCount = artistAlbums.Count(a => a.IsCompilation);

                List<AlbumEntity> albumYears = artistAlbums.Where(a => a.Year.HasValue && !a.IsCompilation && !a.IsLive && !a.IsBestOf).ToList();
                int? yearMini = albumYears.Count > 0 ? albumYears.Min(a => a.Year!.Value) : null;
                int? yearMaxi = albumYears.Count > 0 ? albumYears.Max(a => a.Year!.Value) : null;

                if (yearMini == 0)
                    yearMini = null;
                if (yearMaxi == 0)
                    yearMaxi = null;

                if (artist.TrackCount == trackCount &&
                   artist.TotalDurationSeconds == totalDurationSeconds &&
                   artist.AlbumCount == albumCount &&
                   artist.BestofCount == bestofCount &&
                   artist.LiveCount == liveCount &&
                   artist.CompilationCount == compilationCount &&
                   artist.YearMini == yearMini &&
                   artist.YearMaxi == yearMaxi)
                    continue; // No changes needed

                await artistRepository.UpdateStatisticsAsync(
                    artistId,
                    trackCount,
                    totalDurationSeconds,
                    albumCount,
                    bestofCount,
                    liveCount,
                    compilationCount,
                    yearMini,
                    yearMaxi,
                    RepositoryConnectionKind.Background
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating artist {artistId}: {ex.Message}");
            }
        }
    }

    public async Task UpdateGenresAsync(ConcurrentBag<long> genresId)
    {
        if (genresId.IsEmpty)
            return;

        IEnumerable<TrackEntity> tracks = await GetTracksCacheAsync();
        IEnumerable<AlbumEntity> albums = await GetAlbumsCacheAsync();
        IEnumerable<ArtistEntity> artists = await GetArtistsCacheAsync();
        IEnumerable<GenreEntity> genres = await GetGenresCacheAsync();

        foreach (long genreId in genresId)
        {
            GenreEntity? genre = genres.FirstOrDefault(a => a.Id == genreId);
            if (genre == null)
                continue;

            List<TrackEntity> genreTracks = tracks.Where(t => t.GenreId == genreId).ToList();
            List<AlbumEntity> genreAlbums = albums.Where(a => a.GenreId == genreId).ToList();
            List<ArtistEntity> genreArtists = artists.Where(a => a.GenreId == genreId).ToList();

            int trackCount = genreTracks.Count;
            int artistCount = genreArtists.Count;
            long totalDurationSeconds = genreTracks.Sum(t => t.Duration);
            int albumCount = genreAlbums.Count(a => !a.IsCompilation && !a.IsLive && !a.IsBestOf);
            int bestofCount = genreAlbums.Count(a => a.IsBestOf && !a.IsCompilation);
            int liveCount = genreAlbums.Count(a => a.IsLive && !a.IsCompilation);
            int compilationCount = genreAlbums.Count(a => a.IsCompilation);

            if (genre.TrackCount == trackCount &&
               genre.ArtistCount == artistCount &&
               genre.AlbumCount == albumCount &&
               genre.BestofCount == bestofCount &&
               genre.LiveCount == liveCount &&
               genre.CompilationCount == compilationCount)
                continue; // No changes needed

            await genreRepository.UpdateStatisticsAsync(
                genreId,
                trackCount,
                artistCount,
                albumCount,
                bestofCount,
                liveCount,
                compilationCount,
                totalDurationSeconds,
                RepositoryConnectionKind.Background
            );
        }
    }
}
