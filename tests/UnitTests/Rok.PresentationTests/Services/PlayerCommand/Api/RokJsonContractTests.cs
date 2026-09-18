using System.Text.Json;
using Rok.WebApi.Contracts;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

/// <summary>
/// Reads back payloads captured from a running instance. The API and the web companion are separate
/// programs sharing only these records, so nothing else proves the wire contract actually round-trips.
/// </summary>
public class RokJsonContractTests
{
    private const string StatusPayload =
        """
        {"state":"Paused","mode":"Music","volume":26,"isMuted":false,"position":16.5,"canNext":true,
        "canPrevious":false,"canSeek":true,"isLooping":false,"isBuffering":false,"queueLength":13,"queueSignature":8153726311,
        "current":{"trackId":6932,"title":"Shake it out","artistName":"Suicidal tendencies","albumName":"13",
        "genreName":"Punk","duration":231,"score":0,"listenCount":0,"isArtistFavorite":false,
        "isAlbumFavorite":false,"isGenreFavorite":false}}
        """;

    private const string PlaylistsPayload =
        """
        [{"id":1,"name":"Radio","trackCount":0,"duration":0,"isSmart":true,"shuffleOnPlay":false},
        {"id":3,"name":"Albums de l'année","trackCount":100,"duration":22087,"isSmart":true,"shuffleOnPlay":false}]
        """;

    [Fact(DisplayName = "The status payload of a running instance should read back into the contract")]
    public void StatusPayload_ShouldReadBack()
    {
        // Arrange & Act
        PlayerStatus? status = JsonSerializer.Deserialize<PlayerStatus>(StatusPayload, RokJson.Options);

        // Assert
        Assert.NotNull(status);
        Assert.Equal("Paused", status.State);
        Assert.Equal(26, status.Volume);
        Assert.Equal(16.5, status.Position);
        Assert.Equal(13, status.QueueLength);
        Assert.Equal(8153726311, status.QueueSignature);
        Assert.True(status.CanNext);
        Assert.NotNull(status.Current);
        Assert.Equal(6932, status.Current.TrackId);
        Assert.Equal("Shake it out", status.Current.Title);
        Assert.Equal("Suicidal tendencies", status.Current.ArtistName);
        Assert.Equal(231, status.Current.Duration);
    }

    [Fact(DisplayName = "A status without a current track should read back with a null member")]
    public void StatusPayload_ShouldReadBackWithoutCurrentTrack()
    {
        // Arrange
        const string payload =
            """
            {"state":"Stopped","mode":"None","volume":5,"isMuted":false,"position":0,"canNext":false,
            "canPrevious":false,"canSeek":true,"isLooping":false,"isBuffering":false,"queueLength":0,"queueSignature":0}
            """;

        // Act
        PlayerStatus? status = JsonSerializer.Deserialize<PlayerStatus>(payload, RokJson.Options);

        // Assert
        Assert.NotNull(status);
        Assert.Null(status.Current);
        Assert.Equal("Stopped", status.State);
    }

    [Fact(DisplayName = "The playlist payload of a running instance should read back into the contract")]
    public void PlaylistsPayload_ShouldReadBack()
    {
        // Arrange & Act
        List<PlaylistSummary>? playlists = JsonSerializer.Deserialize<List<PlaylistSummary>>(PlaylistsPayload, RokJson.Options);

        // Assert
        Assert.NotNull(playlists);
        Assert.Equal(2, playlists.Count);
        Assert.Equal("Albums de l'année", playlists[1].Name);
        Assert.Equal(100, playlists[1].TrackCount);
        Assert.True(playlists[1].IsSmart);
    }

    [Fact(DisplayName = "A queue payload should read back keeping the playing entry flagged")]
    public void QueuePayload_ShouldReadBack()
    {
        // Arrange
        const string payload =
            """
            [{"index":0,"trackId":682,"title":"L'étincelle","artistName":"Etienne daho",
            "albumName":"L'adorer","duration":262,"score":0,"isCurrent":true}]
            """;

        // Act
        List<QueueEntry>? queue = JsonSerializer.Deserialize<List<QueueEntry>>(payload, RokJson.Options);

        // Assert
        Assert.NotNull(queue);
        Assert.Single(queue);
        Assert.True(queue[0].IsCurrent);
        Assert.Equal("L'étincelle", queue[0].Title);
    }
}