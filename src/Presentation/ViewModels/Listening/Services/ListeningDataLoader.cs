using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists.Interfaces;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Listening.Services;

public class ListeningDataLoader(IMediator mediator, IArtistViewModelFactory artistViewModelFactory, IAlbumViewModelFactory albumViewModelFactory, ITrackViewModelFactory trackViewModelFactory, ILogger<ListeningDataLoader> logger)
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

    public async Task<AlbumViewModel?> LoadAlbumAsync(long albumId)
    {
        Result<AlbumDto> result = await mediator.Send(new GetAlbumByIdRequest(albumId));

        if (result.IsFailure)
        {
            logger.LogError("Failed to load album {AlbumId} for listening view: {ErrorMessage}", albumId, result.Errors[0]);
            return null;
        }

        AlbumViewModel album = albumViewModelFactory.Create();
        album.SetData(result.Value);

        return album;
    }

    public async Task<List<TrackDto>> GetTracksByArtistAsync(long artistId, int maxTracks, IEnumerable<long> excludeTrackIds)
    {
        Result<IEnumerable<TrackDto>> result = await mediator.Send(new GetTracksByArtistIdRequest(artistId));

        if (result.IsFailure)
        {
            logger.LogError("Failed to load tracks for artist {ArtistId} for listening view", artistId);
            return [];
        }

        List<TrackDto> shuffledTracks = result.Value.ToList();
        if (shuffledTracks.Count == 0)
            return [];

        shuffledTracks.Shuffle();
        shuffledTracks.RemoveAll(c => excludeTrackIds.Contains(c.Id));

        return shuffledTracks.Take(maxTracks).ToList();
    }
}