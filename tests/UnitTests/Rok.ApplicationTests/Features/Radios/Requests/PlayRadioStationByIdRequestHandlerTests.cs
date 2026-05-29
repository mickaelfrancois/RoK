using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class PlayRadioStationByIdRequestHandlerTests
{
    [Fact(DisplayName = "Handle should play the favourite station and touch LastListen")]
    public async Task Handle_ShouldPlayFavouriteStation_AndTouchLastListen()
    {
        // Arrange
        DateTime now = new(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider time = new(now);

        RadioStationEntity entity = new() { Id = 7, Name = "Nova", StreamUrl = "http://stream/nova.mp3", AddedAt = now };
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        Mock<IPlayerService> player = new();
        PlayRadioStationByIdRequestHandler handler = new(repo.Object, player.Object, time);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioStationByIdRequest { Id = 7 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        player.Verify(p => p.PlayRadioStation(It.Is<RadioStationDto>(d => d.Id == 7 && d.Name == "Nova")), Times.Once);
        repo.Verify(r => r.TouchLastListenAsync(7L, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return NotFound when station does not exist")]
    public async Task Handle_ShouldReturnNotFound_WhenStationDoesNotExist()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((RadioStationEntity?)null);
        Mock<IPlayerService> player = new();
        FakeTimeProvider time = new(DateTime.UtcNow);
        PlayRadioStationByIdRequestHandler handler = new(repo.Object, player.Object, time);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioStationByIdRequest { Id = 99 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("radio.not_found");
        player.Verify(p => p.PlayRadioStation(It.IsAny<RadioStationDto>()), Times.Never);
    }
}