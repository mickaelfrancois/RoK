using Rok.Application.Features.Genres.Command;

namespace Rok.ViewModels.Genre.Services;

public class GenreEditService(IMediator mediator, ILogger<GenreEditService> logger)
{
    public async Task UpdateFavoriteAsync(GenreDto genre, bool isFavorite)
    {
        await mediator.SendMessageAsync(new UpdateGenreFavoriteCommand(genre.Id, isFavorite));
        genre.IsFavorite = isFavorite;
    }
}