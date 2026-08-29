using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;

namespace Rok.ViewModels.Genre.Services;

public class GenreDataLoader(IMediator mediator, IAlbumViewModelFactory albumViewModelFactory, ILogger<GenreDataLoader> logger)
{
    public async Task<GenreDto?> LoadGenreAsync(long genreId)
    {
        Result<GenreDto> result = await mediator.Send(new GetGenreByIdRequest(genreId));

        if (result.IsSuccess)
            return result.Value;

        logger.LogError("Failed to load genre {GenreId}", genreId);
        return null;
    }

    public async Task<List<AlbumViewModel>> LoadAlbumsAsync(long genreId)
    {
        IEnumerable<AlbumDto> albums = await mediator.Send(new GetAlbumsByGenreIdRequest(genreId));
        return AlbumViewModelMap.CreateViewModels(albums.ToList(), albumViewModelFactory);
    }


    public async Task<IEnumerable<TrackDto>> LoadTracksAsync(long genreId)
    {
        Result<IEnumerable<TrackDto>> result = await mediator.Send(new GetTracksByGenreIdRequest(genreId));

        if (result.IsFailure)
        {
            logger.LogError("Failed to load tracks for genre {GenreId}", genreId);
            return [];
        }

        return result.Value;
    }
}