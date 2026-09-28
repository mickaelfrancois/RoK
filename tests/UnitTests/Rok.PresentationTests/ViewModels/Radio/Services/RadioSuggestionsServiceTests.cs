using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Services;
using Rok.ViewModels.Radio.Services;

namespace Rok.PresentationTests.ViewModels.Radio.Services;

public class RadioSuggestionsServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<ITelemetryClient> _telemetry = new();
    private readonly Mock<IRegionProvider> _region = new();

    private static readonly RadioSearchResultDto Station =
        new("France Inter", "https://s/inter.mp3", "https://inter.example", "uuid-1", null, "fr", "MP3", 128);

    private RadioSuggestionsService BuildService()
    {
        RadioPictureService pictureService = new(
            new Mock<IRadioPicture>().Object,
            new Mock<IHttpClientFactory>().Object,
            NullLogger<RadioPictureService>.Instance);

        return new RadioSuggestionsService(_mediator, _telemetry.Object, _region.Object, pictureService);
    }

    [Fact(DisplayName = "load_should_send_request_with_region_country_code")]
    public async Task LoadAsync_ShouldSendRequest_WithRegionCountryCode()
    {
        // Arrange
        _region.Setup(r => r.GetCountryCode()).Returns("FR");
        RadioSuggestionsDto expected = new(RadioSuggestionSource.Country, [Station]);
        _mediator.Setup<GetSuggestedRadioStationsRequest, Result<RadioSuggestionsDto>>().Returns(Result<RadioSuggestionsDto>.Ok(expected));

        // Act
        RadioSuggestionsDto suggestions = await BuildService().LoadAsync(CancellationToken.None);

        // Assert
        GetSuggestedRadioStationsRequest sent = Assert.Single(_mediator.Sent<GetSuggestedRadioStationsRequest>());
        Assert.Equal("FR", sent.CountryCode);
        Assert.Equal(expected, suggestions);
    }

    [Fact(DisplayName = "load_should_return_empty_none_when_request_fails")]
    public async Task LoadAsync_ShouldReturnEmptyNone_WhenRequestFails()
    {
        // Arrange
        _mediator.Setup<GetSuggestedRadioStationsRequest, Result<RadioSuggestionsDto>>()
                 .Returns(Result<RadioSuggestionsDto>.Fail(new OperationError("radio.suggestions_failed", "failed")));

        // Act
        RadioSuggestionsDto suggestions = await BuildService().LoadAsync(CancellationToken.None);

        // Assert
        Assert.Equal(RadioSuggestionSource.None, suggestions.Source);
        Assert.Empty(suggestions.Stations);
    }

    [Fact(DisplayName = "play_should_send_play_url_request_and_capture_suggestion_played")]
    public async Task PlayAsync_ShouldSendPlayUrlRequest_AndCaptureSuggestionPlayed()
    {
        // Arrange
        _mediator.Setup<PlayRadioUrlRequest, Result<bool>>().Returns(Result<bool>.Ok(true));

        // Act
        await BuildService().PlayAsync(Station, RadioSuggestionSource.Country);

        // Assert
        PlayRadioUrlRequest sent = Assert.Single(_mediator.Sent<PlayRadioUrlRequest>());
        Assert.Equal(Station.StreamUrl, sent.Url);
        _telemetry.Verify(t => t.CaptureEventAsync("Radio", "SuggestionPlayed",
            It.Is<Dictionary<string, object>>(p => p.Count == 1 && (string)p["source"] == "Country")), Times.Once);
    }

    [Fact(DisplayName = "add_should_send_add_request_with_all_station_fields")]
    public async Task AddAsync_ShouldSendAddRequest_WithAllStationFields()
    {
        // Arrange
        _mediator.Setup<AddRadioStationRequest, Result<long>>().Returns(Result<long>.Ok(7));

        // Act
        bool added = await BuildService().AddAsync(Station, RadioSuggestionSource.Worldwide);

        // Assert
        Assert.True(added);
        AddRadioStationRequest sent = Assert.Single(_mediator.Sent<AddRadioStationRequest>());
        Assert.Equal(Station.Name, sent.Name);
        Assert.Equal(Station.StreamUrl, sent.StreamUrl);
        Assert.Equal(Station.StationUuid, sent.StationUuid);
        _telemetry.Verify(t => t.CaptureEventAsync("Radio", "SuggestionAdded",
            It.Is<Dictionary<string, object>>(p => (string)p["source"] == "Worldwide")), Times.Once);
    }

    [Fact(DisplayName = "add_should_return_true_when_station_already_exists")]
    public async Task AddAsync_ShouldReturnTrue_WhenStationAlreadyExists()
    {
        // Arrange
        _mediator.Setup<AddRadioStationRequest, Result<long>>()
                 .Returns(Result<long>.Fail(new ConflictError("radio.duplicate", "exists")));

        // Act
        bool added = await BuildService().AddAsync(Station, RadioSuggestionSource.Country);

        // Assert
        Assert.True(added);
        _telemetry.Verify(t => t.CaptureEventAsync("Radio", "SuggestionAdded", It.IsAny<Dictionary<string, object>>()), Times.Never);
    }

    [Fact(DisplayName = "add_should_return_false_when_request_fails")]
    public async Task AddAsync_ShouldReturnFalse_WhenRequestFails()
    {
        // Arrange
        _mediator.Setup<AddRadioStationRequest, Result<long>>()
                 .Returns(Result<long>.Fail(new OperationError("radio.add_failed", "failed")));

        // Act
        bool added = await BuildService().AddAsync(Station, RadioSuggestionSource.Country);

        // Assert
        Assert.False(added);
    }
}