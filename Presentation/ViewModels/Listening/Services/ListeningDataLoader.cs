using Rok.Application.Features.Tracks.Query;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists.Interfaces;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Listening.Services;

public class ListeningDataLoader(IMediator mediator, IArtistViewModelFactory artistViewModelFactory, ITrackViewModelFactory trackViewModelFactory, ILogger<ListeningDataLoader> logger)
{
    public List<TrackViewModel> CreateTracksViewModels(List<TrackDto> tracks)
    {
        List<TrackViewModel> list = new(tracks.Count);

        foreach (TrackDto track in tracks)
        {
            TrackViewModel trackViewModel = trackViewModelFactory.Create();
            trackViewModel.SetData(track);
            list.Add(trackViewModel);
        }

        return list;
    }

    public async Task<ArtistViewModel?> LoadArtistAsync(long artistId)
    {
        try
        {
            ArtistViewModel artist = artistViewModelFactory.Create();
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