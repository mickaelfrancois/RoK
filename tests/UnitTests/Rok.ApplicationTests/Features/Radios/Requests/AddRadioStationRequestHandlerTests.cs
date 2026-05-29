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

    [Fact(DisplayName = "add_should_persist_extended_metadata")]
    public async Task Add_ShouldPersistExtendedMetadata()
    {
        // Arrange
        RadioStationEntity? captured = null;
        Mock<IRadioStationRepository> repo = new();
        repo
            .Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<RadioStationEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(42L);

        AddRadioStationRequest request = new()
        {
            Name = "Jazz FM",
            StreamUrl = "https://stream.example/jazz",
            HomepageUrl = "https://jazz.example",
            StationUuid = "uuid-jazz-001",
            FaviconUrl = "https://jazz.example/logo.png",
            CountryCode = "fr",
            Codec = "MP3",
            Bitrate = 128
        };

        AddRadioStationRequestHandler handler = new(repo.Object, TimeProvider.System);

        // Act
        Result<long> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.NotNull(captured);
        Assert.Equal("uuid-jazz-001", captured!.StationUuid);
        Assert.Equal("https://jazz.example/logo.png", captured.FaviconUrl);
        Assert.Equal("fr", captured.CountryCode);
        Assert.Equal("MP3", captured.Codec);
        Assert.Equal(128, captured.Bitrate);
    }

    [Fact(DisplayName = "add_should_accept_nullable_extended_fields")]
    public async Task Add_ShouldAcceptNullableExtendedFields()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo
            .Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7L);

        AddRadioStationRequest request = new()
        {
            Name = "Manual entry",
            StreamUrl = "https://stream.example/manual"
        };

        AddRadioStationRequestHandler handler = new(repo.Object, TimeProvider.System);

        // Act
        Result<long> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "add_should_be_rejected_when_favicon_url_is_relative")]
    public async Task Add_ShouldBeRejected_WhenFaviconUrlIsRelative()
    {
        // Arrange
        AddRadioStationRequestValidator validator = new();
        AddRadioStationRequest request = new()
        {
            Name = "Test",
            StreamUrl = "https://stream.example/x",
            FaviconUrl = "favicon.ico"
        };

        // Act
        ValidationResult result = await validator.ValidateAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.PropertyName == nameof(AddRadioStationRequest.FaviconUrl));
    }
}

internal static class SqliteExceptionStub
{
    public static SqliteException Create(int errorCode) =>
        new("duplicate", errorCode);
}
