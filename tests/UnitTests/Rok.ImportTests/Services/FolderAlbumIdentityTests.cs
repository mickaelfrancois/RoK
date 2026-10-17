using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class FolderAlbumIdentityTests
{
    private static async Task<(TrackImport Tracks, AlbumImport Albums)> BuildCachesAsync(List<TrackEntity> tracks, List<AlbumEntity> albums)
    {
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IAlbumRepository> albumRepository = new();
        albumRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);

        TrackImport trackImport = new(trackRepository.Object);
        AlbumImport albumImport = new(albumRepository.Object, TimeProvider.System);
        await trackImport.LoadCacheAsync();
        await albumImport.LoadCacheAsync();

        return (trackImport, albumImport);
    }

    private static TrackFile File(string path, string album)
    {
        return new TrackFile { FullPath = path, Album = album };
    }

    private static TrackEntity Track(long id, string path, long? albumId, long? artistId)
    {
        return new TrackEntity { Id = id, MusicFile = path, AlbumId = albumId, ArtistId = artistId };
    }

    [Fact(DisplayName = "resolve_skips_a_group_without_known_tracks")]
    public async Task Resolve_ShouldSkipGroup_WithoutKnownTracks()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity>(),
            new List<AlbumEntity> { new() { Id = 1, Name = "Album" } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "Album") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "resolve_picks_the_album_of_most_known_tracks")]
    public async Task Resolve_ShouldPickAlbum_OfMostKnownTracks()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity>
            {
                Track(1, @"C:\m\a.flac", 5, 10),
                Track(2, @"C:\m\b.flac", 5, 10),
                Track(3, @"C:\m\c.flac", 4, 10)
            },
            new List<AlbumEntity> { new() { Id = 4, Name = "Album" }, new() { Id = 5, Name = "Album" } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "Album"), File(@"C:\m\b.flac", "Album"), File(@"C:\m\c.flac", "Album") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        KnownFolderAlbum known = Assert.Single(result).Value;
        Assert.Equal(5, known.AlbumId);
        Assert.Equal(new long[] { 1, 2 }, known.FolderTrackIds.Order());
    }

    [Fact(DisplayName = "resolve_breaks_ties_with_the_lowest_id")]
    public async Task Resolve_ShouldBreakTies_WithTheLowestId()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity> { Track(1, @"C:\m\a.flac", 9, 10), Track(2, @"C:\m\b.flac", 3, 10) },
            new List<AlbumEntity> { new() { Id = 3, Name = "Album" }, new() { Id = 9, Name = "Album" } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "Album"), File(@"C:\m\b.flac", "Album") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        Assert.Equal(3, Assert.Single(result).Value.AlbumId);
    }

    [Fact(DisplayName = "resolve_ignores_an_album_with_another_name")]
    public async Task Resolve_ShouldIgnoreAlbum_WithAnotherName()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity> { Track(1, @"C:\m\a.flac", 1, 10), Track(2, @"C:\m\b.flac", 2, 10), Track(3, @"C:\m\c.flac", 2, 10) },
            new List<AlbumEntity> { new() { Id = 1, Name = "Album" }, new() { Id = 2, Name = "Other" } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "Album"), File(@"C:\m\b.flac", "Album"), File(@"C:\m\c.flac", "Album") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        KnownFolderAlbum known = Assert.Single(result).Value;
        Assert.Equal(1, known.AlbumId);
        Assert.Equal(new long[] { 1 }, known.FolderTrackIds);
    }

    [Fact(DisplayName = "resolve_flags_a_group_whose_known_tracks_have_two_artists")]
    public async Task Resolve_ShouldFlagCompilation_WhenKnownTracksHaveTwoArtists()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity> { Track(1, @"C:\m\a.flac", 1, 10), Track(2, @"C:\m\b.flac", 1, 11) },
            new List<AlbumEntity> { new() { Id = 1, Name = "Album", IsCompilation = true } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "Album"), File(@"C:\m\b.flac", "Album") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        Assert.True(Assert.Single(result).Value.WasCompilation);
    }

    [Fact(DisplayName = "resolve_does_not_flag_a_group_whose_known_tracks_have_one_artist")]
    public async Task Resolve_ShouldNotFlagCompilation_WhenKnownTracksHaveOneArtist()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity> { Track(1, @"C:\m\a.flac", 1, 10), Track(2, @"C:\m\b.flac", 1, 10) },
            new List<AlbumEntity> { new() { Id = 1, Name = "Album" } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "Album"), File(@"C:\m\b.flac", "Album") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        Assert.False(Assert.Single(result).Value.WasCompilation);
    }

    [Fact(DisplayName = "folder_album_identity_matches_album_differing_by_case")]
    public async Task Resolve_ShouldMatchKnownAlbum_WhenTagDiffersOnlyByCase()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity> { Track(1, @"C:\m\a.flac", 1, 10) },
            new List<AlbumEntity> { new() { Id = 1, Name = "Greatest Hits" } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "greatest hits") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        Assert.Equal(1, Assert.Single(result).Value.AlbumId);
    }

    [Fact(DisplayName = "resolve_groups_files_case_insensitively")]
    public async Task Resolve_ShouldGroupFiles_CaseInsensitively()
    {
        // Arrange
        (TrackImport tracks, AlbumImport albums) = await BuildCachesAsync(
            new List<TrackEntity> { Track(1, @"C:\m\a.flac", 1, 10), Track(2, @"C:\m\b.flac", 1, 11) },
            new List<AlbumEntity> { new() { Id = 1, Name = "Album" } });
        List<TrackFile> files = new() { File(@"C:\m\a.flac", "Album"), File(@"C:\m\b.flac", "ALBUM") };

        // Act
        IReadOnlyDictionary<string, KnownFolderAlbum> result = FolderAlbumIdentity.Resolve(files, tracks, albums);

        // Assert
        KnownFolderAlbum known = Assert.Single(result).Value;
        Assert.Equal(new long[] { 1, 2 }, known.FolderTrackIds.Order());
        Assert.True(known.WasCompilation);
        Assert.True(result.ContainsKey("album"));
    }
}