using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class ImportMessageThrottlerTests
{
    [Fact(DisplayName = "ShouldSendAlbumMessage should allow messages under the threshold")]
    public void ShouldSendAlbumMessage_ShouldAllowMessages_UnderThreshold()
    {
        // Arrange
        ImportMessageThrottler throttler = new();

        // Act
        bool first = throttler.ShouldSendAlbumMessage();
        bool second = throttler.ShouldSendAlbumMessage();

        // Assert
        Assert.True(first);
        Assert.True(second);
        Assert.False(throttler.IsAlbumThrottled);
    }

    [Fact(DisplayName = "ShouldSendAlbumMessage should throttle once threshold is exceeded")]
    public void ShouldSendAlbumMessage_ShouldThrottle_OnceThresholdIsExceeded()
    {
        // Arrange
        ImportMessageThrottler throttler = new();
        for (int i = 0; i < ImportMessageThrottler.MaxMessagesBeforeThrottle; i++)
            throttler.ShouldSendAlbumMessage();

        // Act
        bool allowedAtLimit = throttler.ShouldSendAlbumMessage();

        // Assert
        Assert.False(allowedAtLimit);
        Assert.True(throttler.IsAlbumThrottled);
        Assert.False(throttler.ShouldSendAlbumMessage());
    }

    [Fact(DisplayName = "Artist and album throttling should be independent")]
    public void ArtistAndAlbumThrottling_ShouldBeIndependent()
    {
        // Arrange
        ImportMessageThrottler throttler = new();
        for (int i = 0; i < ImportMessageThrottler.MaxMessagesBeforeThrottle; i++)
            throttler.ShouldSendAlbumMessage();
        throttler.ShouldSendAlbumMessage(); // trigger throttling

        // Act
        bool artistAllowed = throttler.ShouldSendArtistMessage();

        // Assert
        Assert.True(artistAllowed);
        Assert.True(throttler.IsAlbumThrottled);
        Assert.False(throttler.IsArtistThrottled);
    }

    [Fact(DisplayName = "Reset should clear throttled state and counters")]
    public void Reset_ShouldClearThrottledState_AndCounters()
    {
        // Arrange
        ImportMessageThrottler throttler = new();
        for (int i = 0; i < ImportMessageThrottler.MaxMessagesBeforeThrottle + 1; i++)
        {
            throttler.ShouldSendAlbumMessage();
            throttler.ShouldSendArtistMessage();
        }

        // Act
        throttler.Reset();

        // Assert
        Assert.False(throttler.IsAlbumThrottled);
        Assert.False(throttler.IsArtistThrottled);
        Assert.True(throttler.ShouldSendAlbumMessage());
        Assert.True(throttler.ShouldSendArtistMessage());
    }
}