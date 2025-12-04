using Rok.Application.Features.Tracks.Query;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Listening.Services;

public class ListeningDataLoader(IMediator mediator, ILogger<ListeningDataLoader> logger)
{
    public List<TrackViewModel> CreateTracksViewModels(List<TrackDto> tracks)
    {
        List<TrackViewModel> list = new(tracks.Count);

        foreach (TrackDto track in tracks)
        {
            TrackViewModel trackViewModel = App.ServiceProvider.GetRequiredService<TrackViewModel>();
            trackViewModel.SetData(track);
            list.Add(trackViewModel);
        }

        return list;
    }

    public async Task<ArtistViewModel?> LoadArtistAsync(long artistId)
    {
        try
        {
            ArtistViewModel artist = App.ServiceProvider.GetRequiredService<ArtistViewModel>();
            await artist.LoadDataAsync(artistId, loadAlbums: false, loadTracks: false, fetchApi: false);
            return artist;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load artist {ArtistId} for listening view", artistId);
            return null;
        }
    }

    public async Task<List<TrackDto>> GetTracksByArtistAsync(long artistId, int maxTracks, IEnumerable<long> excludeTrackIds)
    {
        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByArtistIdQuery(artistId));

        List<TrackDto> shuffledTracks = tracks.ToList();
        if (shuffledTracks.Count == 0)
            return [];

        shuffledTracks.Shuffle();
        shuffledTracks.RemoveAll(c => excludeTrackIds.Contains(c.Id));

        return shuffledTracks.Take(maxTracks).ToList();
    }
}