using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Statistics.Requests;

public class GetStatisticsRequest : IRequest<UserStatisticsDto>
{
}

public class GetStatisticsRequestHandler(ITrackRepository _trackRepository, IAlbumRepository _albumRepository, IArtistRepository _artistRepository, IGenreRepository _genreRepository) : IRequestHandler<GetStatisticsRequest, UserStatisticsDto>
{
    public async Task<UserStatisticsDto> Handle(GetStatisticsRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracksEnum = await _trackRepository.GetAllAsync();
        IEnumerable<AlbumEntity> albumsEnum = await _albumRepository.GetAllAsync();
        IEnumerable<ArtistEntity> artistsEnum = await _artistRepository.GetAllAsync();
        IEnumerable<GenreEntity> genresEnum = await _genreRepository.GetAllAsync();

        var tracks = tracksEnum.ToList();
        var albums = albumsEnum.ToList();
        var artists = artistsEnum.ToList();
        var genres = genresEnum.ToList();

        UserStatisticsDto dto = new()
        {
            TotalTracks = tracks.Count,
            TotalSizeBytes = tracks.Sum(t => t.Size),
            TotalDurationSeconds = tracks.Sum(t => t.Duration),
            TotalAlbums = albums.Count,
            TotalArtists = artists.Count,
            TotalGenres = genres.Count,
            TracksListenedCount = tracks.Count(t => t.ListenCount > 0)
        };
        dto.TracksNeverListenedCount = dto.TotalTracks - dto.TracksListenedCount;

        ComputeTrackTypes(dto, tracks);
        ComputeAlbumTypes(dto, albums);
        ComputeArtistsByGenre(dto, genres);

        ComputeTopAlbums(dto, albums);
        ComputeTopArtists(dto, artists);
        ComputeTopGenres(dto, genres);
        ComputeTopTracks(dto, tracks);

        return dto;
    }

    private static void ComputeTrackTypes(UserStatisticsDto dto, List<TrackEntity> tracks)
    {
        var fileTypeGroups = tracks
             .Where(t => !string.IsNullOrWhiteSpace(t.MusicFile))
             .Select(t =>
             {
                 string ext = Path.GetExtension(t.MusicFile ?? string.Empty).ToLowerInvariant().TrimStart('.');
                 return string.IsNullOrEmpty(ext) ? "unknown" : ext;
             })
             .GroupBy(ext => ext)
             .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
             .OrderByDescending(x => x.Count)
             .ToList();

        dto.TracksByFileType = fileTypeGroups;
    }

    private static void ComputeAlbumTypes(UserStatisticsDto dto, List<AlbumEntity> albums)
    {
        int liveCount = albums.Count(a => a.IsLive);
        int compilationCount = albums.Count(a => a.IsCompilation);
        int bestofCount = albums.Count(a => a.IsBestOf);
        int studioCount = albums.Count(a => !a.IsLive && !a.IsCompilation && !a.IsBestOf);

        dto.AlbumsByType.Add(new NamedCount { Name = "studio", Count = studioCount });
        dto.AlbumsByType.Add(new NamedCount { Name = "live", Count = liveCount });
        dto.AlbumsByType.Add(new NamedCount { Name = "compilation", Count = compilationCount });
        dto.AlbumsByType.Add(new NamedCount { Name = "bestof", Count = bestofCount });
    }

    private static void ComputeArtistsByGenre(UserStatisticsDto dto, List<GenreEntity> genres)
    {
        var artistsByGenre = genres
            .Select(g => new NamedCount { Name = string.IsNullOrWhiteSpace(g.Name) ? "unknown" : g.Name, Count = g.ArtistCount })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();
        dto.ArtistsByGenre = artistsByGenre;
    }

    private static void ComputeTopAlbums(UserStatisticsDto dto, List<AlbumEntity> albums)
    {
        dto.TopAlbums = albums
           .OrderByDescending(a => a.ListenCount)
           .Take(20)
           .Select(a => new TopItem { Id = a.Id, Name = a.Name, ListenCount = a.ListenCount })
           .ToList();
    }

    private static void ComputeTopArtists(UserStatisticsDto dto, List<ArtistEntity> artists)
    {
        dto.TopArtists = artists
            .OrderByDescending(ar => ar.ListenCount)
            .Take(20)
            .Select(ar => new TopItem { Id = ar.Id, Name = ar.Name, ListenCount = ar.ListenCount })
            .ToList();
    }

    private static void ComputeTopGenres(UserStatisticsDto dto, List<GenreEntity> genres)
    {
        dto.TopGenres = genres
            .OrderByDescending(ar => ar.ListenCount)
            .Take(20)
            .Select(ar => new TopItem { Id = ar.Id, Name = ar.Name, ListenCount = ar.ListenCount })
            .ToList();
    }

    private static void ComputeTopTracks(UserStatisticsDto dto, List<TrackEntity> tracks)
    {
        dto.TopTracks = tracks
             .OrderByDescending(t => t.ListenCount)
             .Take(20)
             .Select(t => new TopItem { Id = t.Id, Name = t.Title, ListenCount = t.ListenCount })
             .ToList();
    }
}
