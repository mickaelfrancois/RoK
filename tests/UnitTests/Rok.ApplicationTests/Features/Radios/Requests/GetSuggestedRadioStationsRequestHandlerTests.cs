using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Features.Radios.Services;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class GetSuggestedRadioStationsRequestHandlerTests
{
    private readonly Mock<IRadioBrowserClient> _client = new();

    private static readonly IReadOnlyList<RadioSearchResultDto> CountryStations =
    [
        new RadioSearchResultDto("France Inter", "https://s/inter", null, null, null, "fr", null, null),
    ];

    private static readonly IReadOnlyList<RadioSearchResultDto> WorldStations =
    [
        new RadioSearchResultDto("BBC Radio 1", "https://s/bbc1", null, null, null, "gb", null, null),
        new RadioSearchResultDto("KEXP", "https://s/kexp", null, null, null, "us", null, null),
    ];

    private GetSuggestedRadioStationsRequestHandler CreateHandler() =>
        new(_client.Object, NullLogger<GetSuggestedRadioStationsRequestHandler>.Instance);

    [Fact(DisplayName = "suggestions_should_return_country_stations_when_country_has_results")]
    public async Task Suggestions_ShouldReturnCountryStations_WhenCountryHasResults()
    {
        // Arrange
        _client.Setup(c => c.GetTopByCountryAsync("FR", 24, It.IsAny<CancellationToken>())).ReturnsAsync(CountryStations);

        // Act
        Result<RadioSuggestionsDto> result = await CreateHandler().Handle(
            new GetSuggestedRadioStationsRequest { CountryCode = "FR" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(RadioSuggestionSource.Country, result.Value.Source);
        Assert.Equal(CountryStations, result.Value.Stations);
        _client.Verify(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "suggestions_should_fallback_to_worldwide_when_country_is_empty")]
    public async Task Suggestions_ShouldFallbackToWorldwide_WhenCountryIsEmpty()
    {
        // Arrange
        _client.Setup(c => c.GetTopByCountryAsync("FR", It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<RadioSearchResultDto>());
        _client.Setup(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(WorldStations);

        // Act
        Result<RadioSuggestionsDto> result = await CreateHandler().Handle(
            new GetSuggestedRadioStationsRequest { CountryCode = "FR" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(RadioSuggestionSource.Worldwide, result.Value.Source);
        Assert.Equal(2, result.Value.Stations.Count);
    }

    [Fact(DisplayName = "suggestions_should_fallback_to_worldwide_when_country_request_fails")]
    public async Task Suggestions_ShouldFallbackToWorldwide_WhenCountryRequestFails()
    {
        // Arrange
        _client.Setup(c => c.GetTopByCountryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("no network"));
        _client.Setup(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(WorldStations);

        // Act
        Result<RadioSuggestionsDto> result = await CreateHandler().Handle(
            new GetSuggestedRadioStationsRequest { CountryCode = "FR" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(RadioSuggestionSource.Worldwide, result.Value.Source);
    }

    [Fact(DisplayName = "suggestions_should_fallback_to_worldwide_when_country_request_times_out")]
    public async Task Suggestions_ShouldFallbackToWorldwide_WhenCountryRequestTimesOut()
    {
        // Arrange
        _client.Setup(c => c.GetTopByCountryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new TaskCanceledException());
        _client.Setup(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(WorldStations);

        // Act
        Result<RadioSuggestionsDto> result = await CreateHandler().Handle(
            new GetSuggestedRadioStationsRequest { CountryCode = "FR" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(RadioSuggestionSource.Worldwide, result.Value.Source);
    }

    [Fact(DisplayName = "suggestions_should_return_empty_success_when_all_requests_fail")]
    public async Task Suggestions_ShouldReturnEmptySuccess_WhenAllRequestsFail()
    {
        // Arrange
        _client.Setup(c => c.GetTopByCountryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("no network"));
        _client.Setup(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("no network"));

        // Act
        Result<RadioSuggestionsDto> result = await CreateHandler().Handle(
            new GetSuggestedRadioStationsRequest { CountryCode = "FR" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(RadioSuggestionSource.None, result.Value.Source);
        Assert.Empty(result.Value.Stations);
    }

    [Fact(DisplayName = "suggestions_should_return_none_when_worldwide_is_empty")]
    public async Task Suggestions_ShouldReturnNone_WhenWorldwideIsEmpty()
    {
        // Arrange
        _client.Setup(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<RadioSearchResultDto>());

        // Act
        Result<RadioSuggestionsDto> result = await CreateHandler().Handle(
            new GetSuggestedRadioStationsRequest { CountryCode = null }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(RadioSuggestionSource.None, result.Value.Source);
    }

    [Theory(DisplayName = "suggestions_should_skip_country_when_code_is_invalid")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ZZ")]
    [InlineData("FRA")]
    [InlineData("1A")]
    public async Task Suggestions_ShouldSkipCountry_WhenCodeIsInvalid(string? countryCode)
    {
        // Arrange
        _client.Setup(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(WorldStations);

        // Act
        Result<RadioSuggestionsDto> result = await CreateHandler().Handle(
            new GetSuggestedRadioStationsRequest { CountryCode = countryCode }, CancellationToken.None);

        // Assert
        Assert.Equal(RadioSuggestionSource.Worldwide, result.Value.Source);
        _client.Verify(c => c.GetTopByCountryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "suggestions_should_normalize_country_code_before_request")]
    public async Task Suggestions_ShouldNormalizeCountryCode_BeforeRequest()
    {
        // Arrange
        _client.Setup(c => c.GetTopByCountryAsync("FR", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(CountryStations);

        // Act
        _ = await CreateHandler().Handle(new GetSuggestedRadioStationsRequest { CountryCode = " fr " }, CancellationToken.None);

        // Assert
        _client.Verify(c => c.GetTopByCountryAsync("FR", 24, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "suggestions_should_clamp_limit_between_1_and_100")]
    public async Task Suggestions_ShouldClampLimit_Between1And100()
    {
        // Arrange
        _client.Setup(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(WorldStations);

        // Act
        _ = await CreateHandler().Handle(new GetSuggestedRadioStationsRequest { Limit = 500 }, CancellationToken.None);

        // Assert
        _client.Verify(c => c.GetTopWorldwideAsync(100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "suggestions_should_propagate_cancellation_when_caller_cancels")]
    public async Task Suggestions_ShouldPropagateCancellation_WhenCallerCancels()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();
        _client.Setup(c => c.GetTopByCountryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new TaskCanceledException());

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateHandler().Handle(new GetSuggestedRadioStationsRequest { CountryCode = "FR" }, cts.Token));
        _client.Verify(c => c.GetTopWorldwideAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}