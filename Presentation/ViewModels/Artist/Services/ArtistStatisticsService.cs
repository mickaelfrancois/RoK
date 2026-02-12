using Rok.Application.Features.Artists.Command;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.ViewModels.Artist.Services;

public class ArtistStatisticsService(IMediator mediator)
{
    public bool NeedUpdate(ArtistDto artist, IEnumerable<AlbumViewModel> albums, IEnumerable<TrackViewModel> tracks)
    {
        bool mustUpdate = artist.AlbumCount != albums.Count(c => !c.Album.IsLive && !c.Album.IsCompilation && !c.Album.IsBestOf);
        mustUpdate |= artist.LiveCount != albums.Count(c => c.Album.IsLive);
        mustUpdate |= artist.CompilationCount != albums.Count(c => c.Album.IsCompilation);
        mustUpdate |= artist.BestofCount != albums.Count(c => c.Album.IsBestOf);
        mustUpdate |= artist.TrackCount != tracks.Count();

        return mustUpdate;
    }

    public async Task<bool> UpdateIfNeededAsync(ArtistDto artist, IEnumerable<AlbumViewModel> albums, IEnumerable<TrackViewModel> tracks)
    {
        if (!NeedUpdate(artist, albums, tracks))
            return false;

        int trackCount = tracks.Count();
        int albumCount = albums.Count(c => !c.Album.IsLive && !c.Album.IsCompilation && !c.Album.IsBestOf);
        int bestofCount = albums.Count(c => c.Album.IsBestOf);
        int liveCount = albums.Count(c => c.Album.IsLive);
        int compilationCount = albums.Count(c => c.Album.IsCompilation);
        long totalDurationSeconds = tracks.Sum(c => c.Track.Duration);

        int yearMini = albums
            .Where(a => a.Album.Year.HasValue && !a.Album.IsCompilation && !a.Album.IsLive && !a.Album.IsBestOf)
            .Select(a => a.Album.Year!.Value)
            .DefaultIfEmpty(0)
            .Min();

        int yearMaxi = albums
            .Where(a => a.Album.Year.HasValue && !a.Album.IsCompilation && !a.Album.IsLive && !a.Album.IsBestOf)
            .Select(a => a.Album.Year!.Value)
            .DefaultIfEmpty(0)
            .Max();

        UpdateArtistStatisticsCommand command = new(artist.Id)
        {
            TrackCount = trackCount,
            AlbumCount = albumCount,
            BestOfCount = bestofCount,
            LiveCount = liveCount,
            CompilationCount = compilationCount,
            TotalDurationSeconds = totalDurationSeconds,
            YearMini = yearMini == 0 ? null : yearMini,
            YearMaxi = yearMaxi == 0 ? null : yearMaxi
        };

        await mediator.SendMessageAsync(command);

        artist.TrackCount = trackCount;
        artist.AlbumCount = albumCount;
        artist.BestofCount = bestofCount;
        artist.LiveCount = liveCount;
        artist.CompilationCount = compilationCount;
        artist.TotalDurationSeconds = totalDurationSeconds;
        artist.YearMini = yearMini == 0 ? null : yearMini;
        artist.YearMaxi = yearMaxi == 0 ? null : yearMaxi;

        return true;
    }
}