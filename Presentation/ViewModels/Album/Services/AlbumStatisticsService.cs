using Rok.Application.Features.Albums.Command;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Album.Services;

public class AlbumStatisticsService(IMediator mediator)
{
    private static bool NeedUpdate(AlbumDto album, IEnumerable<TrackViewModel> tracks)
    {
        bool mustUpdate = album.TrackCount != tracks.Count();
        mustUpdate |= album.Duration != tracks.Sum(c => c.Track.Duration);

        return mustUpdate;
    }

    public async Task<bool> UpdateIfNeededAsync(AlbumDto album, IEnumerable<TrackViewModel> tracks)
    {
        if (!NeedUpdate(album, tracks))
            return false;

        int trackCount = tracks.Count();
        long duration = tracks.Sum(c => c.Track.Duration);

        UpdateAlbumStatisticsCommand command = new(album.Id)
        {
            TrackCount = trackCount,
            Duration = duration,
        };

        await mediator.SendMessageAsync(command);

        album.TrackCount = trackCount;
        album.Duration = duration;

        return true;
    }
}