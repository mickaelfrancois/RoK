using Microsoft.Data.Sqlite;
using Moq;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class UpdateRadioStationRequestHandlerTests
{
    private static RadioStationEntity ExistingStation(long id = 1) =>
        new()
        {
            Id = id,
            Name = "Old name",
            StreamUrl = "https://old.example/stream.mp3",
            HomepageUrl = "https://old.example",
            AddedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    [Fact(DisplayName = "update_should_succeed_when_station_exists")]
    public async Task Update_ShouldSucceed_WhenStationExists()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingStation());
        UpdateRadioStationRequestHandler handler = new(repo.Object);
        UpdateRadioStationRequest request = new()
        {
            Id = 1,
            Name = "Renamed",
            StreamUrl = "https://new.example/stream.mp3",
            HomepageUrl = "https://new.example"
        };

        // Act
        Result<bool> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repo.Verify(r => r.UpdateAsync(1, "Renamed", "https://new.example/stream.mp3", "https://new.example", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "update_should_return_not_found_when_station_missing")]
    public async Task Update_ShouldReturnNotFound_WhenStationMissing()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((RadioStationEntity?)null);
        UpdateRadioStationRequestHandler handler = new(repo.Object);
        UpdateRadioStationRequest request = new()
        {
            Id = 99,
            Name = "X",
            StreamUrl = "https://x.example/stream.mp3"
        };

        // Act
        Result<bool> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("radio.not_found");
        repo.Verify(r => r.UpdateAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "update_should_return_conflict_when_url_already_exists")]
    public async Task Update_ShouldReturnConflict_WhenUrlAlreadyExists()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingStation());
        SqliteException sqliteEx = SqliteExceptionStub.Create(19);
        repo.Setup(r => r.UpdateAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sqliteEx);
        UpdateRadioStationRequestHandler handler = new(repo.Object);
        UpdateRadioStationRequest request = new()
        {
            Id = 1,
            Name = "Renamed",
            StreamUrl = "https://other.example/stream.mp3"
        };

        // Act
        Result<bool> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<ConflictError>().And.HaveErrorWithCode("radio.duplicate");
    }

    [Fact(DisplayName = "update_should_trim_input_strings")]
    public async Task Update_ShouldTrimInputStrings()
    {
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingStation());
        UpdateRadioStationRequestHandler handler = new(repo.Object);
        UpdateRadioStationRequest request = new()
        {
            Id = 1,
            Name = "  Padded  ",
            StreamUrl = "  https://new.example/stream.mp3  ",
            HomepageUrl = "  https://new.example  "
        };

        await handler.Handle(request, CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(1, "Padded", "https://new.example/stream.mp3", "https://new.example", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "update_should_normalize_empty_homepage_to_null")]
    public async Task Update_ShouldNormalizeEmptyHomepage_ToNull()
    {
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingStation());
        UpdateRadioStationRequestHandler handler = new(repo.Object);
        UpdateRadioStationRequest request = new()
        {
            Id = 1,
            Name = "X",
            StreamUrl = "https://x.example/stream.mp3",
            HomepageUrl = "   "
        };

        await handler.Handle(request, CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(1, "X", "https://x.example/stream.mp3", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "update_should_be_rejected_when_id_is_zero")]
    public async Task Update_ShouldBeRejected_WhenIdIsZero()
    {
        UpdateRadioStationRequestValidator validator = new();
        var result = await validator.ValidateAsync(new UpdateRadioStationRequest { Id = 0, Name = "X", StreamUrl = "https://x.example/stream.mp3" }, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.PropertyName == nameof(UpdateRadioStationRequest.Id));
    }

    [Fact(DisplayName = "update_should_be_rejected_when_url_is_relative")]
    public async Task Update_ShouldBeRejected_WhenUrlIsRelative()
    {
        UpdateRadioStationRequestValidator validator = new();
        var result = await validator.ValidateAsync(new UpdateRadioStationRequest { Id = 1, Name = "X", StreamUrl = "/stream.mp3" }, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.PropertyName == nameof(UpdateRadioStationRequest.StreamUrl));
    }
}
