using Rok.Application.Dto;
using Rok.Application.Features.Genres.Requests;
using Rok.ViewModels.Genre.Services;

namespace Rok.PresentationTests.ViewModels.Genre.Services;

public class GenreEditServiceTests
{
    [Fact(DisplayName = "UpdateFavoriteAsync should send the favorite command and update the genre state")]
    public async Task UpdateFavoriteAsync_ShouldSendCommandAndUpdateState()
    {
        // Arrange
        FakeMediator mediator = new();
        GenreDto genre = new() { Id = 5, IsFavorite = false };
        GenreEditService sut = new(mediator);

        // Act
        await sut.UpdateFavoriteAsync(genre, isFavorite: true);

        // Assert
        Assert.True(genre.IsFavorite);
        UpdateGenreFavoriteRequest sent = Assert.Single(mediator.Sent<UpdateGenreFavoriteRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.IsFavorite);
    }
}