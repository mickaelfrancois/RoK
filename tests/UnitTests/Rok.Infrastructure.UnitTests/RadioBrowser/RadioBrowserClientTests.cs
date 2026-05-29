using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Rok.Application.Dto;
using Rok.Infrastructure.RadioBrowser;

namespace Rok.Infrastructure.UnitTests.RadioBrowser;

public class RadioBrowserClientTests
{
    private static (RadioBrowserClient Client, List<HttpRequestMessage> Captured) CreateClient(
        string responseJson,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        List<HttpRequestMessage> captured = [];
        Mock<HttpMessageHandler> handlerMock = new(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                captured.Add(req);
                HttpResponseMessage resp = new(status)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(responseJson))
                };
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return resp;
            });

        HttpClient http = new(handlerMock.Object) { BaseAddress = new Uri("https://de1.api.radio-browser.info/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Rok/1.0");

        RadioBrowserClient client = new(http, NullLogger<RadioBrowserClient>.Instance);
        return (client, captured);
    }

    [Fact(DisplayName = "search_by_name_should_call_byname_endpoint_with_encoded_query")]
    public async Task SearchByName_ShouldCallByNameEndpoint_WithEncodedQuery()
    {
        // Arrange
        var (client, captured) = CreateClient("[]");

        // Act
        _ = await client.SearchByNameAsync("jazz fm", 50, CancellationToken.None);

        // Assert
        Assert.Single(captured);
        string url = captured[0].RequestUri!.AbsoluteUri;
        Assert.Contains("/json/stations/byname/jazz%20fm", url);
        Assert.Contains("limit=50", url);
        Assert.Contains("hidebroken=true", url);
        Assert.Contains("order=votes", url);
        Assert.Contains("reverse=true", url);
    }

    [Fact(DisplayName = "search_by_name_should_attach_user_agent_header")]
    public async Task SearchByName_ShouldAttachUserAgentHeader()
    {
        var (client, captured) = CreateClient("[]");

        _ = await client.SearchByNameAsync("jazz", 10, CancellationToken.None);

        Assert.Contains("Rok/1.0", captured[0].Headers.UserAgent.ToString());
    }

    [Fact(DisplayName = "search_by_name_should_map_url_resolved_when_present")]
    public async Task SearchByName_ShouldMapUrlResolved_WhenPresent()
    {
        string json = """
            [{
                "name": "Jazz FM",
                "url": "https://stream.example/orig",
                "url_resolved": "https://stream.example/resolved.mp3",
                "stationuuid": "uuid-1",
                "favicon": "https://jazz.example/logo.png",
                "homepage": "https://jazz.example",
                "countrycode": "FR",
                "codec": "MP3",
                "bitrate": 128
            }]
            """;
        var (client, _) = CreateClient(json);

        IReadOnlyList<RadioSearchResultDto> results = await client.SearchByNameAsync("jazz", 50, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("https://stream.example/resolved.mp3", results[0].StreamUrl);
        Assert.Equal("Jazz FM", results[0].Name);
        Assert.Equal("fr", results[0].CountryCode);
        Assert.Equal("MP3", results[0].Codec);
        Assert.Equal(128, results[0].Bitrate);
    }

    [Fact(DisplayName = "search_by_name_should_fallback_to_url_when_resolved_missing")]
    public async Task SearchByName_ShouldFallbackToUrl_WhenResolvedMissing()
    {
        string json = """
            [{ "name": "TSF", "url": "https://stream.example/tsf", "url_resolved": "" }]
            """;
        var (client, _) = CreateClient(json);

        var results = await client.SearchByNameAsync("tsf", 50, CancellationToken.None);

        Assert.Equal("https://stream.example/tsf", results[0].StreamUrl);
    }

    [Fact(DisplayName = "search_by_name_should_skip_stations_without_name_or_url")]
    public async Task SearchByName_ShouldSkipStations_WithoutNameOrUrl()
    {
        string json = """
            [
                { "name": "Valid", "url": "https://stream.example/valid", "url_resolved": "https://stream.example/valid" },
                { "name": "",      "url": "https://stream.example/noname", "url_resolved": "https://stream.example/noname" },
                { "name": "No URL", "url": "", "url_resolved": "" }
            ]
            """;
        var (client, _) = CreateClient(json);

        var results = await client.SearchByNameAsync("x", 50, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Valid", results[0].Name);
    }

    [Fact(DisplayName = "search_by_name_should_treat_bitrate_zero_as_unknown")]
    public async Task SearchByName_ShouldTreatBitrateZero_AsUnknown()
    {
        string json = """
            [{ "name": "A", "url": "https://stream.example/a", "url_resolved": "https://stream.example/a", "bitrate": 0 }]
            """;
        var (client, _) = CreateClient(json);

        var results = await client.SearchByNameAsync("a", 50, CancellationToken.None);

        Assert.Null(results[0].Bitrate);
    }

    [Fact(DisplayName = "search_by_name_should_return_empty_list_on_empty_response")]
    public async Task SearchByName_ShouldReturnEmptyList_OnEmptyResponse()
    {
        var (client, _) = CreateClient("[]");

        var results = await client.SearchByNameAsync("zzz", 50, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact(DisplayName = "search_by_name_should_throw_http_request_exception_on_500")]
    public Task SearchByName_ShouldThrowHttpRequestException_On500()
    {
        var (client, _) = CreateClient("internal error", HttpStatusCode.InternalServerError);

        return Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SearchByNameAsync("x", 50, CancellationToken.None));
    }

    [Fact(DisplayName = "search_by_name_should_apply_limit_parameter")]
    public async Task SearchByName_ShouldApplyLimitParameter()
    {
        var (client, captured) = CreateClient("[]");

        _ = await client.SearchByNameAsync("rock", 10, CancellationToken.None);

        Assert.Contains("limit=10", captured[0].RequestUri!.AbsoluteUri);
    }
}