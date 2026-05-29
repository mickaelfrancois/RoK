using System.Net;
using System.Text;
using Moq;
using Moq.Protected;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Services;

namespace Rok.ApplicationTests.Features.Radios.Services;

public class RadioStreamUrlResolverTests
{
    private static RadioStreamUrlResolver CreateResolver(Dictionary<string, (string Body, string ContentType)> responses)
    {
        Mock<HttpMessageHandler> handlerMock = new(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                string url = req.RequestUri!.ToString();
                if (!responses.TryGetValue(url, out (string Body, string ContentType) entry))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                HttpResponseMessage resp = new(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(entry.Body))
                };
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(entry.ContentType);
                return resp;
            });

        HttpClient httpClient = new(handlerMock.Object);
        return new RadioStreamUrlResolver(httpClient);
    }

    [Fact(DisplayName = "Resolve should pass through a direct audio URL")]
    public async Task Resolve_ShouldPassThrough_DirectAudioUrl()
    {
        // Arrange
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://stream/audio.mp3"] = ("[binary]", "audio/mpeg")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://stream/audio.mp3", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("http://stream/audio.mp3", result.Value);
    }

    [Fact(DisplayName = "Resolve should extract first File entry from a .pls playlist")]
    public async Task Resolve_ShouldExtractFirstFileEntry_FromPlsPlaylist()
    {
        // Arrange
        string pls = """
            [playlist]
            NumberOfEntries=2
            File1=http://stream/one.mp3
            File2=http://stream/two.mp3
            Version=2
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/stream.pls"] = (pls, "audio/x-scpls")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/stream.pls", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("http://stream/one.mp3", result.Value);
    }

    [Fact(DisplayName = "Resolve should extract first URL from a simple m3u playlist")]
    public async Task Resolve_ShouldExtractFirstUrl_FromSimpleM3uPlaylist()
    {
        // Arrange
        string m3u = """
            #EXTM3U
            #EXTINF:-1,Radio
            http://stream/one.mp3
            http://stream/two.mp3
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/stream.m3u"] = (m3u, "audio/x-mpegurl")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/stream.m3u", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("http://stream/one.mp3", result.Value);
    }

    [Fact(DisplayName = "Resolve should reject an HLS segment manifest")]
    public async Task Resolve_ShouldReject_HlsSegmentManifest()
    {
        // Arrange
        string hls = """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-TARGETDURATION:6
            #EXTINF:6.000,
            segment0.ts
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/live.m3u8"] = (hls, "application/vnd.apple.mpegurl")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/live.m3u8", CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("radio.hls_unsupported");
    }

    [Fact(DisplayName = "Resolve should return error when playlist contains no usable URL")]
    public async Task Resolve_ShouldReturnError_WhenPlaylistContainsNoUsableUrl()
    {
        // Arrange
        string pls = """
            [playlist]
            NumberOfEntries=0
            Version=2
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/empty.pls"] = (pls, "audio/x-scpls")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/empty.pls", CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("radio.no_stream_in_playlist");
    }
}