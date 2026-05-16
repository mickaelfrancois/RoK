using Rok.Application.Features.Genres.Requests;

namespace Rok.ViewModels.Genre.Services;

public class GenreEditService(IMediator mediator)
{
    public async Task UpdateFavoriteAsync(GenreDto genre, bool isFavorite)
    {
        await mediator.Send(new UpdateGenreFavoriteRequest(genre.Id, isFavorite));
        genre.IsFavorite = isFavorite;
    }
}