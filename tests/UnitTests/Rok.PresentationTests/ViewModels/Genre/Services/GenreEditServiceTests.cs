using MiF.Mediator.Interfaces;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Genres.Command;
using Rok.ViewModels.Genre.Services;

namespace Rok.PresentationTests.ViewModels.Genre.Services;

public class GenreEditServiceTests
{
    [Fact(DisplayName = "UpdateFavoriteAsync should send the favorite command and update the genre state")]
    public async Task UpdateFavoriteAsync_ShouldSendCommandAndUpdateState()
    {
        // Arrange
        Mock<IMediator> mediator = new();
        GenreDto genre = new() { Id = 5, IsFavorite = false };
        GenreEditService sut = new(mediator.Object);

        // Act
        await sut.UpdateFavoriteAsync(genre, isFavorite: true);

        // Assert
        Assert.True(genre.IsFavorite);
        mediator.Verify(m => m.SendMessageAsync(
            It.Is<UpdateGenreFavoriteCommand>(c => c.Id == 5 && c.IsFavorite == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
