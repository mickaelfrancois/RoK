using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Logic.ViewModels.Albums;
using Rok.Mapping;
using Rok.ViewModels.Albums.Interfaces;

namespace Rok.ViewModels.Genre.Services;

public class GenreDataLoader(IMediator mediator, IAlbumViewModelFactory albumViewModelFactory, ILogger<GenreDataLoader> logger)
{
    public async Task<GenreDto?> LoadGenreAsync(long genreId)
    {
        Result<GenreDto> result = await mediator.SendMessageAsync(new GetGenreByIdQuery(genreId));

        if (result.IsSuccess)
            return result.Value!;

        logger.LogError("Failed to load genre {GenreId}", genreId);
        return null;
    }

    public async Task<List<AlbumViewModel>> LoadAlbumsAsync(long genreId)
    {
        IEnumerable<AlbumDto> albums = await mediator.SendMessageAsync(new GetAlbumsByGenreIdQuery(genreId));
        return AlbumViewModelMap.CreateViewModels(albums.ToList(), albumViewModelFactory);
    }


    public async Task<IEnumerable<TrackDto>> LoadTracksAsync(long genreId)
    {
        return await mediator.SendMessageAsync(new GetTracksByGenreIdQuery(genreId));
    }
}