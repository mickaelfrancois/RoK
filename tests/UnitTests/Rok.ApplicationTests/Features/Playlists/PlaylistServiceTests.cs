using Moq;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Requests;

namespace Rok.ApplicationTests.Features.Playlists;

public class PlaylistServiceTests
{
    private static PlaylistGroupDto BuildGroup(string name, int trackCount) => new() { Name = name, TrackCount = trackCount };

    private static PlaylistHeaderDto BuildPlaylist(int trackMaximum, params PlaylistGroupDto[] groups) =>
        new() { Id = 1, Name = "P", TrackMaximum = trackMaximum, Groups = groups.ToList() };

    private static List<TrackDto> BuildTracks(params long[] ids) => ids.Select(id => new TrackDto { Id = id, Title = $"t{id}" }).ToList();

    private static FakeMediator BuildMediator(Dictionary<PlaylistGroupDto, List<TrackDto>> groupTracks)
    {
        FakeMediator mediator = new();
        mediator.Setup<GeneratePlaylistTracksRequest, IEnumerable<TrackDto>>()
                .Returns(request =>
                {
                    foreach (KeyValuePair<PlaylistGroupDto, List<TrackDto>> entry in groupTracks)
                    {
                        if (ReferenceEquals(request.Group, entry.Key))
                            return entry.Value;
                    }
                    return new List<TrackDto>();
                });
        return mediator;
    }

    [Fact(DisplayName = "GenerateAsync should throw when playlist is null")]
    public Task GenerateAsync_ShouldThrow_WhenPlaylistIsNull()
    {
        // Arrange
        PlaylistService sut = new(Mock.Of<IMediator>());

        // Act & Assert
        return Assert.ThrowsAsync<ArgumentNullException>(() => sut.GenerateAsync(null!));
    }

    [Fact(DisplayName = "GenerateAsync should return empty result when playlist has no groups")]
    public async Task GenerateAsync_ShouldReturnEmpty_WhenNoGroups()
    {
        // Arrange
        PlaylistHeaderDto playlist = BuildPlaylist(trackMaximum: 10);
        PlaylistService sut = new(Mock.Of<IMediator>());

        // Act
        PlaylistTracksDto result = await sut.GenerateAsync(playlist);

        // Assert
        Assert.Empty(result.Tracks);
    }

    [Fact(DisplayName = "GenerateAsync should add up to group TrackCount per round")]
    public async Task GenerateAsync_ShouldRespectGroupTrackCountPerRound()
    {
        // Arrange
        PlaylistGroupDto group = BuildGroup("g1", trackCount: 2);
        PlaylistHeaderDto playlist = BuildPlaylist(trackMaximum: 10, group);
        Dictionary<PlaylistGroupDto, List<TrackDto>> groupTracks = new() { { group, BuildTracks(1, 2, 3, 4, 5) } };
        FakeMediator mediator = BuildMediator(groupTracks);
        PlaylistService sut = new(mediator);

        // Act
        PlaylistTracksDto result = await sut.GenerateAsync(playlist);

        // Assert
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, result.Tracks.Select(t => t.Id).ToArray());
    }

    [Fact(DisplayName = "GenerateAsync should stop when TrackMaximum is reached")]
    public async Task GenerateAsync_ShouldStop_WhenTrackMaximumReached()
    {
        // Arrange
        PlaylistGroupDto group = BuildGroup("g1", trackCount: 10);
        PlaylistHeaderDto playlist = BuildPlaylist(trackMaximum: 3, group);
        FakeMediator mediator = BuildMediator(new() { { group, BuildTracks(1, 2, 3, 4, 5) } });
        PlaylistService sut = new(mediator);

        // Act
        PlaylistTracksDto result = await sut.GenerateAsync(playlist);

        // Assert
        Assert.Equal(3, result.Tracks.Count);
    }

    [Fact(DisplayName = "GenerateAsync should round-robin across multiple groups")]
    public async Task GenerateAsync_ShouldRoundRobinAcrossGroups()
    {
        // Arrange
        PlaylistGroupDto g1 = BuildGroup("g1", trackCount: 1);
        PlaylistGroupDto g2 = BuildGroup("g2", trackCount: 1);
        PlaylistHeaderDto playlist = BuildPlaylist(trackMaximum: 4, g1, g2);
        FakeMediator mediator = BuildMediator(new()
        {
            { g1, BuildTracks(1, 2) },
            { g2, BuildTracks(10, 20) }
        });
        PlaylistService sut = new(mediator);

        // Act
        PlaylistTracksDto result = await sut.GenerateAsync(playlist);

        // Assert
        Assert.Equal(new long[] { 1, 10, 2, 20 }, result.Tracks.Select(t => t.Id).ToArray());
    }

    [Fact(DisplayName = "GenerateAsync should skip duplicates already added by another group")]
    public async Task GenerateAsync_ShouldSkipDuplicatesAcrossGroups()
    {
        // Arrange
        PlaylistGroupDto g1 = BuildGroup("g1", trackCount: 2);
        PlaylistGroupDto g2 = BuildGroup("g2", trackCount: 2);
        PlaylistHeaderDto playlist = BuildPlaylist(trackMaximum: 10, g1, g2);
        FakeMediator mediator = BuildMediator(new()
        {
            { g1, BuildTracks(1, 2) },
            { g2, BuildTracks(2, 3) }
        });
        PlaylistService sut = new(mediator);

        // Act
        PlaylistTracksDto result = await sut.GenerateAsync(playlist);

        // Assert
        Assert.Equal(new long[] { 1, 2, 3 }, result.Tracks.Select(t => t.Id).ToArray());
    }

    [Fact(DisplayName = "GenerateAsync should mark exhausted group empty and stop when all groups empty")]
    public async Task GenerateAsync_ShouldStop_WhenAllGroupsExhausted()
    {
        // Arrange
        PlaylistGroupDto g1 = BuildGroup("g1", trackCount: 5);
        PlaylistHeaderDto playlist = BuildPlaylist(trackMaximum: 100, g1);
        FakeMediator mediator = BuildMediator(new() { { g1, BuildTracks(1, 2) } });
        PlaylistService sut = new(mediator);

        // Act
        PlaylistTracksDto result = await sut.GenerateAsync(playlist);

        // Assert
        Assert.Equal(2, result.Tracks.Count);
    }

    [Fact(DisplayName = "GenerateAsync should assign incremental TrackNumber starting at one")]
    public async Task GenerateAsync_ShouldAssignIncrementalTrackNumber()
    {
        // Arrange
        PlaylistGroupDto group = BuildGroup("g1", trackCount: 3);
        PlaylistHeaderDto playlist = BuildPlaylist(trackMaximum: 5, group);
        FakeMediator mediator = BuildMediator(new() { { group, BuildTracks(7, 8, 9) } });
        PlaylistService sut = new(mediator);

        // Act
        PlaylistTracksDto result = await sut.GenerateAsync(playlist);

        // Assert
        Assert.Equal(new int?[] { 1, 2, 3 }, result.Tracks.Select(t => t.TrackNumber).ToArray());
    }
}
