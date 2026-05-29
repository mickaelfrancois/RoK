using Moq;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Features.Radios.Services;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class SearchRadioStationsRequestHandlerTests
{
    private readonly Mock<IRadioBrowserClient> _client = new();

    [Fact(DisplayName = "search_should_return_results_when_client_responds")]
    public async Task Search_ShouldReturnResults_WhenClientResponds()
    {
        // Arrange
        IReadOnlyList<RadioSearchResultDto> expected =
        [
            new RadioSearchResultDto("Jazz FM", "https://s/jazz", null, null, null, null, null, null),
            new RadioSearchResultDto("TSF Jazz", "https://s/tsf", null, null, null, null, null, null),
        ];
        _client.Setup(c => c.SearchByNameAsync("jazz", 50, It.IsAny<CancellationToken>()))
               .ReturnsAsync(expected);

        SearchRadioStationsRequestHandler handler = new(_client.Object);

        // Act
        Result<IReadOnlyList<RadioSearchResultDto>> result = await handler.Handle(
            new SearchRadioStationsRequest { Query = "jazz", Limit = 50 },
            CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(2, result.Value.Count);
    }

    [Fact(DisplayName = "search_should_return_empty_when_no_match")]
    public async Task Search_ShouldReturnEmpty_WhenNoMatch()
    {
        // Arrange
        _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<RadioSearchResultDto>());

        SearchRadioStationsRequestHandler handler = new(_client.Object);

        // Act
        Result<IReadOnlyList<RadioSearchResultDto>> result = await handler.Handle(
            new SearchRadioStationsRequest { Query = "zz", Limit = 50 },
            CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Empty(result.Value);
    }

    [Fact(DisplayName = "search_should_fail_with_search_failed_on_http_exception")]
    public async Task Search_ShouldFailWithSearchFailed_OnHttpException()
    {
        // Arrange
        _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("no network"));

        SearchRadioStationsRequestHandler handler = new(_client.Object);

        // Act
        var result = await handler.Handle(
            new SearchRadioStationsRequest { Query = "x", Limit = 50 },
            CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("radio.search_failed");
    }

    [Fact(DisplayName = "search_should_fail_with_search_timeout_on_task_canceled")]
    public async Task Search_ShouldFailWithSearchTimeout_OnTaskCanceled()
    {
        // Arrange
        _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new TaskCanceledException());

        SearchRadioStationsRequestHandler handler = new(_client.Object);

        // Act
        var result = await handler.Handle(
            new SearchRadioStationsRequest { Query = "x", Limit = 50 },
            CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("radio.search_timeout");
    }

    [Fact(DisplayName = "search_should_forward_cancellation_token")]
    public async Task Search_ShouldForwardCancellationToken()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), cts.Token))
               .ReturnsAsync(Array.Empty<RadioSearchResultDto>());

        SearchRadioStationsRequestHandler handler = new(_client.Object);

        // Act
        _ = await handler.Handle(new SearchRadioStationsRequest { Query = "x", Limit = 50 }, cts.Token);

        // Assert
        _client.Verify(c => c.SearchByNameAsync("x", 50, cts.Token), Times.Once);
    }

    [Fact(DisplayName = "search_should_be_rejected_when_query_too_short")]
    public async Task Search_ShouldBeRejected_WhenQueryTooShort()
    {
        // Arrange
        SearchRadioStationsRequestValidator validator = new();

        // Act
        ValidationResult result = await validator.ValidateAsync(
            new SearchRadioStationsRequest { Query = "a", Limit = 50 }, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, e => e.PropertyName == nameof(SearchRadioStationsRequest.Query));
    }

    [Fact(DisplayName = "search_should_be_rejected_when_limit_out_of_range")]
    public async Task Search_ShouldBeRejected_WhenLimitOutOfRange()
    {
        // Arrange
        SearchRadioStationsRequestValidator validator = new();

        // Act
        var resultZero = await validator.ValidateAsync(new SearchRadioStationsRequest { Query = "jazz", Limit = 0 }, CancellationToken.None);
        var resultHigh = await validator.ValidateAsync(new SearchRadioStationsRequest { Query = "jazz", Limit = 300 }, CancellationToken.None);

        // Assert
        Assert.False(resultZero.IsValid);
        Assert.False(resultHigh.IsValid);
    }
}