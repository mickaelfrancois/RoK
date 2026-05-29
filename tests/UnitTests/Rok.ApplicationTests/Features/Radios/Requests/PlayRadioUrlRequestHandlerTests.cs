using Moq;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Features.Radios.Services;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class PlayRadioUrlRequestHandlerTests
{
    [Fact(DisplayName = "Handle should resolve URL and play an ad-hoc station without persisting")]
    public async Task Handle_ShouldResolveUrl_AndPlayAdHocStation()
    {
        // Arrange
        Mock<IRadioStreamUrlResolver> resolver = new();
        resolver.Setup(r => r.ResolveAsync("http://radio/stream.pls", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Ok("http://stream/audio.mp3"));
        Mock<IPlayerService> player = new();
        PlayRadioUrlRequestHandler handler = new(resolver.Object, player.Object);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioUrlRequest { Url = "http://radio/stream.pls" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        player.Verify(p => p.PlayRadioStation(It.Is<RadioStationDto>(d => d.Id == 0 && d.StreamUrl == "http://stream/audio.mp3")), Times.Once);
    }

    [Fact(DisplayName = "Handle should surface HLS rejection from resolver")]
    public async Task Handle_ShouldSurfaceHlsRejection_FromResolver()
    {
        // Arrange
        Mock<IRadioStreamUrlResolver> resolver = new();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Fail(new OperationError("radio.hls_unsupported", "HLS not supported.")));
        Mock<IPlayerService> player = new();
        PlayRadioUrlRequestHandler handler = new(resolver.Object, player.Object);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioUrlRequest { Url = "http://radio/live.m3u8" }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("radio.hls_unsupported");
        player.Verify(p => p.PlayRadioStation(It.IsAny<RadioStationDto>()), Times.Never);
    }
}