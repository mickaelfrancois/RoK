using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using Rok.Infrastructure.MusicData;

namespace Rok.Infrastructure.UnitTests.MusicData;

public class MusicDataApiServiceTests
{
    private sealed class SequenceHandler(IEnumerable<(HttpStatusCode Status, string Body)> responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new(responses);

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            (HttpStatusCode status, string body) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.OK, "{}");

            HttpResponseMessage response = new(status)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body))
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            return Task.FromResult(response);
        }
    }

    private static MusicDataApiService CreateService(SequenceHandler handler)
    {
        HttpClient httpClient = new(handler);

        Mock<IAppOptions> appOptions = new();
        appOptions.SetupGet(o => o.NovaApiEnabled).Returns(true);

        IOptions<MusicDataApiOptions> options = Options.Create(new MusicDataApiOptions
        {
            BaseAddress = "https://musicdata.test/",
            ApiKey = "test-key"
        });

        Mock<IHttpClientFactory> factory = new();

        return new MusicDataApiService(httpClient, factory.Object, appOptions.Object, options, NullLogger<MusicDataApiService>.Instance);
    }

    [Fact(DisplayName = "transient_failure_is_not_cached_and_is_retried_on_next_call")]
    public async Task Transient_failure_is_not_cached_and_is_retried_on_next_call()
    {
        // Arrange
        SequenceHandler handler = new(
        [
            (HttpStatusCode.InternalServerError, "boom"),
            (HttpStatusCode.OK, """{ "name": "Radiohead" }""")
        ]);
        MusicDataApiService sut = CreateService(handler);

        // Act
        MusicDataArtistDto? first = await sut.GetArtistAsync("Radiohead", null);
        MusicDataArtistDto? second = await sut.GetArtistAsync("Radiohead", null);

        // Assert
        Assert.Null(first);
        Assert.NotNull(second);
        Assert.Equal("Radiohead", second!.Name);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact(DisplayName = "successful_response_is_cached_and_not_requeried")]
    public async Task Successful_response_is_cached_and_not_requeried()
    {
        // Arrange
        SequenceHandler handler = new(
        [
            (HttpStatusCode.OK, """{ "name": "Radiohead" }""")
        ]);
        MusicDataApiService sut = CreateService(handler);

        // Act
        MusicDataArtistDto? first = await sut.GetArtistAsync("Radiohead", null);
        MusicDataArtistDto? second = await sut.GetArtistAsync("Radiohead", null);

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact(DisplayName = "definitive_not_found_is_cached_as_negative")]
    public async Task Definitive_not_found_is_cached_as_negative()
    {
        // Arrange
        SequenceHandler handler = new(
        [
            (HttpStatusCode.NotFound, string.Empty)
        ]);
        MusicDataApiService sut = CreateService(handler);

        // Act
        MusicDataArtistDto? first = await sut.GetArtistAsync("Unknown Artist", null);
        MusicDataArtistDto? second = await sut.GetArtistAsync("Unknown Artist", null);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, handler.CallCount);
    }
}
