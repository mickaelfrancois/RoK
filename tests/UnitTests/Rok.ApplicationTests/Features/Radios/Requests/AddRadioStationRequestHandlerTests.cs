using Microsoft.Data.Sqlite;
using Moq;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class AddRadioStationRequestHandlerTests
{
    [Fact(DisplayName = "Handle should add station and return its id")]
    public async Task Handle_ShouldAddStation_AndReturnId()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42L);
        AddRadioStationRequestHandler handler = new(repo.Object, TimeProvider.System);
        AddRadioStationRequest request = new() { Name = "Nova", StreamUrl = "https://stream.nova.fr/nova.mp3" };

        // Act
        Result<long> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(42L, result.Value);
        repo.Verify(r => r.AddAsync(It.Is<RadioStationEntity>(e => e.Name == "Nova" && e.StreamUrl == "https://stream.nova.fr/nova.mp3"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return conflict error when URL already exists")]
    public async Task Handle_ShouldReturnConflict_WhenUrlAlreadyExists()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        SqliteException sqliteEx = SqliteExceptionStub.Create(19);
        repo.Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sqliteEx);
        AddRadioStationRequestHandler handler = new(repo.Object, TimeProvider.System);
        AddRadioStationRequest request = new() { Name = "Nova", StreamUrl = "https://stream.nova.fr/nova.mp3" };

        // Act
        Result<long> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<ConflictError>().And.HaveErrorWithCode("radio.duplicate");
    }
}

internal static class SqliteExceptionStub
{
    public static SqliteException Create(int errorCode) =>
        new("duplicate", errorCode);
}
