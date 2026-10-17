using System.Data;
using System.Globalization;
using System.Text.Json;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Messaging;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Options;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Import;
using Rok.Import.Services;
using Rok.Infrastructure.Files;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;
using Rok.Shared.Enums;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Merges an artist into another one through <see cref="ArtistMergeService"/>, then runs the real library import again and checks that the rescan leaves the database as the merge left it.
/// </summary>
public sealed class ArtistMergeRescanTests
{
    // The target name contains the source name: replaying a criteria rewrite would give "The The Rescan Beatles".
    private const string SourceName = "Rescan Beatles";
    private const string TargetName = "The Rescan Beatles";
    private const string DoubledTargetName = "The The Rescan Beatles";
    private const string SourceAlbumArtistFile = "track-s1";
    private const string SourcePerformerFile = "track-s2";
    private const string TargetHomonymFile = "track-t1";
    private const string TargetOwnFile = "track-t2";
    private const string NeighbourFile = "track-z-neighbour";
    private const string TargetNeighbourFile = "track-t-neighbour";
    private const string NeighbourRetitled = "Neighbour retitled by another reader";
    private const string TargetNeighbourRetitled = "Target neighbour retitled by another reader";
    private const string HomonymAlbum = "Homonym Album";
    private const string HomonymAlbumOtherCase = "homonym album";
    private const string SourceOwnAlbum = "Source Own Album";
    private const string TargetOwnAlbum = "Target Own Album";
    private const string NeighbourAlbum = "Neighbour album";
    private const string TargetNeighbourAlbum = "Target neighbour album";
    private const string SameFolderAlbum = "Same Folder Album";
    private const string SameFolderName = "same";

    private static readonly string[] SnapshotTables =
    [
        "Tracks", "Albums", "Artists", "Genres", "Playlists", "PlaylistTracks", "ListeningEvents", "ArtistTags", "AlbumTags", "Tags", "EqualizerPreset"
    ];

    [Fact(DisplayName = "rescan_after_merge_keeps_one_artist_with_the_target_id_flac")]
    public async Task Rescan_AfterMergeFlac_ShouldKeepOneArtistWithTheTargetId()
    {
        using Harness harness = new(CreateFlac);

        await AssertRescanAfterMergeAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rescan_after_merge_keeps_one_artist_with_the_target_id_mp3")]
    public async Task Rescan_AfterMergeMp3_ShouldKeepOneArtistWithTheTargetId()
    {
        using Harness harness = new(CreateMp3);

        await AssertRescanAfterMergeAsync(harness);
    }

    [Fact(DisplayName = "rescan_after_a_partial_merge_never_recreates_the_source")]
    public async Task Rescan_AfterPartialMerge_ShouldNeverRecreateTheSource()
    {
        // Arrange
        using Harness harness = new(CreateFlac);
        await harness.ImportTwiceAsync();
        long sourceId = harness.ArtistId(SourceName);
        long targetId = harness.ArtistId(TargetName);
        long survivorId = harness.AlbumIdOf(TargetHomonymFile);
        harness.SeedData(sourceId, targetId);
        harness.SeedSmartPlaylists();
        int artistCount = harness.TakeSnapshot().Rows("Artists").Count;
        harness.FailWritesTo(SourceAlbumArtistFile);

        // Act
        ArtistMergeOutcome partial = await harness.MergeAsync(sourceId, targetId);
        Snapshot afterIncident = harness.TakeSnapshot();
        harness.Touch(SourceAlbumArtistFile, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        Snapshot afterRescan = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, partial.State);
        Assert.Equal(SourceAlbumArtistFile, Path.GetFileNameWithoutExtension(Assert.Single(partial.Failures).FilePath));
        Assert.Equal(SourceName, afterIncident.Value("Artists", targetId, "renamePendingFrom"));
        Assert.Equal(artistCount - 1, afterIncident.Rows("Artists").Count);
        Assert.Equal(SourceName, harness.ReadArtistFromFile(SourceAlbumArtistFile));
        Assert.Equal(TargetName, harness.ReadArtistFromFile(SourcePerformerFile));

        Assert.Equal(1, rescan.TracksUpdated);
        Assert.Equal(0, rescan.TracksImported);
        Assert.Equal(0, rescan.TracksDeleted);
        Assert.Equal(0, rescan.ArtistsImported);
        Assert.Equal(0, rescan.ArtistsDeleted);
        Assert.Equal(0, rescan.AlbumsImported);
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal(artistCount - 1, afterRescan.Rows("Artists").Count);
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Artists WHERE name = @name COLLATE NOCASE", new { name = SourceName }));
        Assert.Equal(targetId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", harness.TrackId(SourceAlbumArtistFile), "artistId"));
        Assert.Equal(survivorId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", harness.TrackId(SourceAlbumArtistFile), "albumId"));
        Assert.Equal(survivorId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", harness.TrackId(TargetHomonymFile), "albumId"));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = HomonymAlbum }));
        Assert.Equal(SourceName, afterRescan.Value("Artists", targetId, "renamePendingFrom"));
        AssertSameExceptTouched(afterIncident, afterRescan, harness.TrackId(SourceAlbumArtistFile));
    }

    [Fact(DisplayName = "resume_after_a_partial_merge_completes_without_rewriting_criteria_twice")]
    public async Task Resume_AfterPartialMerge_ShouldCompleteWithoutRewritingCriteriaTwice()
    {
        // Arrange
        using Harness harness = new(CreateFlac);
        await harness.ImportTwiceAsync();
        long sourceId = harness.ArtistId(SourceName);
        long targetId = harness.ArtistId(TargetName);
        harness.SeedData(sourceId, targetId);
        harness.SeedSmartPlaylists();
        harness.FailWritesTo(SourceAlbumArtistFile);

        ArtistMergeOutcome partial = await harness.MergeAsync(sourceId, targetId);
        Snapshot afterIncident = harness.TakeSnapshot();
        harness.Touch(SourceAlbumArtistFile, 2);
        await harness.ImportAsync();
        harness.StopFailingWrites();

        // Act
        ArtistRenameOutcome resumed = await harness.ResumeAsync(targetId);
        Snapshot afterResume = harness.TakeSnapshot();
        harness.Retitle(SourceAlbumArtistFile, "Retitled by another reader", 4);
        ImportStatisticsDto finalRescan = await harness.ImportAsync();
        Snapshot final = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, partial.State);
        Assert.Equal(EArtistRenameState.Completed, resumed.State);
        Assert.Equal(string.Empty, afterResume.Value("Artists", targetId, "renamePendingFrom"));
        Assert.Equal(TargetName, harness.ReadArtistFromFile(SourceAlbumArtistFile));
        Assert.Equal(afterIncident.Rows("Playlists"), afterResume.Rows("Playlists"));

        foreach (string name in new[] { "Smart equals", "Smart not equals", "Smart contains" })
        {
            string json = harness.CriteriaJson(name);

            Assert.Equal(1, CountOccurrences(json, TargetName));
            Assert.DoesNotContain(DoubledTargetName, json, StringComparison.Ordinal);
        }

        // Positive control: the file written by the resume was changed by another reader and taken over.
        Assert.Equal(1, finalRescan.TracksUpdated);
        Assert.Equal("Retitled by another reader", final.Value("Tracks", harness.TrackId(SourceAlbumArtistFile), "title"));
        Assert.Equal(0, finalRescan.TracksImported);
        Assert.Equal(0, finalRescan.TracksDeleted);
        Assert.Equal(0, finalRescan.ArtistsImported);
        Assert.Equal(0, finalRescan.ArtistsDeleted);
        Assert.Equal(0, finalRescan.AlbumsImported);
        Assert.Equal(0, finalRescan.AlbumsDeleted);
        AssertSameExceptTouched(afterResume, final, harness.TrackId(SourceAlbumArtistFile));
    }

    [Fact(DisplayName = "smart_playlists_follow_the_target_after_merge")]
    public async Task SmartPlaylists_AfterMergeAndRescan_ShouldFollowTheTarget()
    {
        // Arrange
        using Harness harness = new(CreateFlac);
        await harness.ImportTwiceAsync();
        long sourceId = harness.ArtistId(SourceName);
        long targetId = harness.ArtistId(TargetName);
        harness.SeedSmartPlaylists();
        Dictionary<string, long[]> before = await harness.GenerateSmartPlaylistsAsync();

        // Act
        ArtistMergeOutcome outcome = await harness.MergeAsync(sourceId, targetId);
        harness.RetitleNeighbourAsAnotherReader();
        ImportStatisticsDto rescan = await harness.ImportAsync();
        Dictionary<string, long[]> after = await harness.GenerateSmartPlaylistsAsync();

        // Assert
        long[] merged =
        [
            harness.TrackId(SourceAlbumArtistFile),
            harness.TrackId(SourcePerformerFile),
            harness.TrackId(TargetHomonymFile),
            harness.TrackId(TargetOwnFile)
        ];

        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(1, rescan.TracksUpdated);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        Assert.Equal([harness.TrackId(SourceAlbumArtistFile), harness.TrackId(SourcePerformerFile)], before["Smart equals"]);
        Assert.Equal(merged.Order(), after["Smart equals"]);
        Assert.Equal(merged.Order(), after["Smart contains"]);
        Assert.Equal(merged.Order(), after["Smart target equals"]);
        Assert.Equal(new[] { harness.TrackId(NeighbourFile), harness.TrackId(TargetNeighbourFile) }.Order(), after["Smart not equals"]);
        Assert.Equal(3, outcome.ChangedPlaylistIds.Count);

        foreach (string name in new[] { "Smart equals", "Smart not equals", "Smart contains", "Smart target equals" })
        {
            string json = harness.CriteriaJson(name);

            Assert.Contains(TargetName, json, StringComparison.Ordinal);
            Assert.DoesNotContain(DoubledTargetName, json, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(json, TargetName));
        }
    }

    [Fact(DisplayName = "artist_picture_and_playlist_picture_follow_the_target")]
    public async Task Merge_ShouldKeepTheTargetPictureAndMoveThePlaylistPicture()
    {
        // Arrange
        using Harness harness = new(CreateFlac);
        await harness.ImportTwiceAsync();
        long sourceId = harness.ArtistId(SourceName);
        long targetId = harness.ArtistId(TargetName);
        harness.SeedImages(SourceName, [7, 7, 7], "backdrop2.jpg", [8, 8, 8]);
        harness.SeedImages(TargetName, [1, 2, 3], "backdrop1.jpg", [4, 5, 6]);
        long playlistId = harness.SeedClassicPlaylist(SourceName);

        // Act
        ArtistMergeOutcome outcome = await harness.MergeAsync(sourceId, targetId);
        harness.RetitleNeighbourAsAnotherReader();
        ImportStatisticsDto rescan = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        // Assert
        string targetFolder = harness.ArtistFolder(TargetName);

        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal([playlistId], outcome.ChangedPlaylistIds);
        Assert.Equal(TargetName, after.Value("Playlists", playlistId, "picture"));
        Assert.Equal([1, 2, 3], ReadBytes(harness.PictureFile(TargetName)));
        Assert.Equal([4, 5, 6], ReadBytes(Path.Combine(targetFolder, "backdrop1.jpg")));
        Assert.Equal([8, 8, 8], ReadBytes(Path.Combine(targetFolder, "backdrop2.jpg")));
        Assert.False(Directory.Exists(harness.ArtistFolder(SourceName)));
        Assert.Equal(1, rescan.TracksUpdated);
        Assert.Equal(0, rescan.ArtistsImported);
        Assert.Equal(0, rescan.ArtistsDeleted);
    }

    [Fact(DisplayName = "artist_picture_falls_back_to_the_source_when_the_target_has_none")]
    public async Task Merge_ShouldKeepTheSourcePictureWhenTheTargetHasNone()
    {
        // Arrange
        using Harness harness = new(CreateFlac);
        await harness.ImportTwiceAsync();
        long sourceId = harness.ArtistId(SourceName);
        long targetId = harness.ArtistId(TargetName);
        harness.SeedImages(SourceName, [7, 7, 7], "backdrop2.jpg", [8, 8, 8]);

        // Act
        ArtistMergeOutcome outcome = await harness.MergeAsync(sourceId, targetId);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal([7, 7, 7], ReadBytes(harness.PictureFile(TargetName)));
        Assert.Equal([8, 8, 8], ReadBytes(Path.Combine(harness.ArtistFolder(TargetName), "backdrop2.jpg")));
        Assert.False(Directory.Exists(harness.ArtistFolder(SourceName)));
    }

    [Fact(DisplayName = "rescan_after_merge_keeps_album_data_when_homonyms_share_a_folder")]
    public async Task Rescan_AfterMergeWithHomonymsInOneFolder_ShouldKeepAlbumData()
    {
        using Harness harness = new(CreateFlac);

        await AssertSameFolderRescanAsync(harness, touchAll: false);
    }

    [Fact(DisplayName = "rescan_of_every_file_after_merge_keeps_album_data_when_homonyms_share_a_folder")]
    public async Task Rescan_OfEveryFileAfterMergeWithHomonymsInOneFolder_ShouldKeepAlbumData()
    {
        using Harness harness = new(CreateFlac);

        await AssertSameFolderRescanAsync(harness, touchAll: true);
    }

    private static async Task AssertSameFolderRescanAsync(Harness harness, bool touchAll)
    {
        // Arrange
        string[] files = ["same-s1", "same-s2", "same-t1", "same-t2"];

        harness.AddFile(SameFolderName, "same-s1", SourceName, SameFolderAlbum);
        harness.AddFile(SameFolderName, "same-s2", SourceName, SameFolderAlbum);
        harness.AddFile(SameFolderName, "same-t1", TargetName, SameFolderAlbum);
        harness.AddFile(SameFolderName, "same-t2", TargetName, SameFolderAlbum);

        await harness.ImportTwiceAsync();

        long sourceId = harness.ArtistId(SourceName);
        long targetId = harness.ArtistId(TargetName);
        int trackCount = harness.Scalar<int>("SELECT COUNT(*) FROM Tracks");
        List<long> initialAlbumIds = harness.AlbumIdsNamed(SameFolderAlbum);

        harness.SeedSameFolderAlbumData(files);

        ArtistMergeOutcome outcome = await harness.MergeAsync(sourceId, targetId);
        Snapshot afterMerge = harness.TakeSnapshot();
        int albumsAfterMerge = harness.AlbumIdsNamed(SameFolderAlbum).Count;
        int listenSum = harness.Scalar<int>("SELECT COALESCE(SUM(listenCount), 0) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = SameFolderAlbum });

        // Act
        foreach (string file in touchAll ? files : [files[2]])
        {
            harness.Retitle(file, "Retitled " + file, 3);
        }

        ImportStatisticsDto rescan = await harness.ImportAsync();
        List<long> finalAlbumIds = harness.AlbumIdsNamed(SameFolderAlbum);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the folder must be processed again");
        Assert.Equal(0, rescan.TracksDeleted);
        Assert.Equal(trackCount, harness.Scalar<int>("SELECT COUNT(*) FROM Tracks"));
        Assert.Equal(0, rescan.AlbumsDeleted);
        Assert.Equal(0, rescan.AlbumsImported);
        Assert.Equal(albumsAfterMerge, finalAlbumIds.Count);
        Assert.Single(finalAlbumIds);
        Assert.Contains(finalAlbumIds[0], initialAlbumIds);
        Assert.Equal("0", harness.Scalar<string>("SELECT CAST(isCompilation AS TEXT) FROM Albums WHERE id = @id", new { id = finalAlbumIds[0] }));

        long survivorId = finalAlbumIds[0];

        Assert.Equal(1, harness.Scalar<int>("SELECT isFavorite FROM Albums WHERE id = @survivorId", new { survivorId }));
        Assert.Equal(listenSum, harness.Scalar<int>("SELECT listenCount FROM Albums WHERE id = @survivorId", new { survivorId }));
        Assert.Equal(initialAlbumIds.Count, harness.Scalar<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @survivorId", new { survivorId }));
        Assert.Equal(afterMerge.Rows("ListeningEvents").Count, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @survivorId", new { survivorId }));

        foreach (string file in files)
        {
            Assert.Equal(survivorId, harness.AlbumIdOf(file));
        }
    }

    private static async Task AssertRescanAfterMergeAsync(Harness harness)
    {
        // Arrange
        await harness.ImportTwiceAsync();

        long sourceId = harness.ArtistId(SourceName);
        long targetId = harness.ArtistId(TargetName);
        long neighbourId = harness.TrackId(NeighbourFile);
        long sourceHomonymAlbumId = harness.AlbumIdOf(SourceAlbumArtistFile);
        long survivorId = harness.AlbumIdOf(TargetHomonymFile);

        harness.SeedData(sourceId, targetId);
        harness.SeedSmartPlaylists();
        harness.SeedImages(SourceName, [7, 7, 7], "backdrop2.jpg", [8, 8, 8]);
        harness.SeedImages(TargetName, [1, 2, 3], "backdrop1.jpg", [4, 5, 6]);

        Snapshot seeded = harness.TakeSnapshot();
        int artistCount = seeded.Rows("Artists").Count;
        int albumCount = seeded.Rows("Albums").Count;

        ArtistMergeOutcome outcome = await harness.MergeAsync(sourceId, targetId);
        Snapshot before = harness.TakeSnapshot();

        // Act
        harness.RetitleNeighbourAsAnotherReader();
        ImportStatisticsDto statistics = await harness.ImportAsync();
        Snapshot after = harness.TakeSnapshot();

        harness.RetitleTargetNeighbourAsAnotherReader();
        ImportStatisticsDto targetFolderStatistics = await harness.ImportAsync();
        Snapshot afterTargetFolder = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(sourceId, outcome.DeletedArtistId);
        Assert.Equal(string.Empty, before.Value("Artists", targetId, "renamePendingFrom"));
        Assert.Equal(TargetName, harness.ReadArtistFromFile(SourceAlbumArtistFile));
        Assert.Equal(TargetName, harness.ReadArtistFromFile(SourcePerformerFile));
        Assert.Equal(TargetName, harness.ReadArtistFromFile(TargetHomonymFile));

        // Positive control: the folder was processed again, so the neighbour changed by another reader was taken over.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, statistics.TracksUpdated);

        Assert.Equal(0, statistics.TracksImported);
        Assert.Equal(0, statistics.AlbumsImported);
        Assert.Equal(0, statistics.ArtistsImported);
        Assert.Equal(0, statistics.GenresImported);
        Assert.Equal(0, statistics.TracksDeleted);
        Assert.Equal(0, statistics.AlbumsDeleted);
        Assert.Equal(0, statistics.ArtistsDeleted);
        Assert.Equal(0, statistics.GenresDeleted);

        // One artist, with the target id.
        Assert.Equal(artistCount - 1, after.Rows("Artists").Count);
        Assert.False(after.Rows("Artists").ContainsKey(sourceId));
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Artists WHERE name = @name COLLATE NOCASE", new { name = SourceName }));
        Assert.Equal(targetId, harness.ArtistId(TargetName));

        foreach (string file in new[] { SourceAlbumArtistFile, SourcePerformerFile, TargetHomonymFile, TargetOwnFile })
        {
            Assert.Equal(targetId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(file), "artistId"));
        }

        Assert.Equal("1", after.Value("Artists", targetId, "isFavorite"));
        Assert.Equal("8", after.Value("Artists", targetId, "listenCount"));
        Assert.Equal(seeded.Value("Artists", sourceId, "lastListen"), after.Value("Artists", targetId, "lastListen"));
        Assert.Equal("4", after.Value("Artists", targetId, "trackCount"));
        Assert.Equal("3", after.Value("Artists", targetId, "albumCount"));

        // Listening history: complete, nothing detached.
        Assert.Equal(3, after.Rows("ListeningEvents").Count);
        Assert.Equal(3, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE artistId = @targetId AND albumId = @survivorId", new { targetId, survivorId }));

        // User tags, union.
        Assert.Equal(2, harness.Scalar<int>("SELECT COUNT(*) FROM ArtistTags WHERE artistId = @targetId", new { targetId }));
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM ArtistTags WHERE artistId = @sourceId", new { sourceId }));

        // Equalizer presets: the target one is kept, the source album one moves, nothing is orphaned.
        Assert.Equal(2, after.Rows("EqualizerPreset").Count);
        Assert.Equal(1.5, harness.Scalar<double>("SELECT Band0 FROM EqualizerPreset WHERE Scope = 'Artist' AND ScopeId = @targetId", new { targetId }));
        Assert.Equal(6.5, harness.Scalar<double>("SELECT Band0 FROM EqualizerPreset WHERE Scope = 'Album' AND ScopeId = @survivorId", new { survivorId }));

        // Homonym albums: one album, every track, favorite, tags and listens summed.
        Assert.Equal(albumCount - 1, after.Rows("Albums").Count);
        Assert.False(after.Rows("Albums").ContainsKey(sourceHomonymAlbumId));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = HomonymAlbum }));
        Assert.Equal(survivorId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(SourceAlbumArtistFile), "albumId"));
        Assert.Equal(survivorId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(TargetHomonymFile), "albumId"));
        Assert.Equal("1", after.Value("Albums", survivorId, "isFavorite"));
        Assert.Equal("6", after.Value("Albums", survivorId, "listenCount"));
        Assert.Equal("2", after.Value("Albums", survivorId, "trackCount"));
        Assert.Equal(2, harness.Scalar<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @survivorId", new { survivorId }));
        Assert.Equal(targetId.ToString(CultureInfo.InvariantCulture), after.Value("Albums", survivorId, "artistId"));

        // Images and playlists.
        Assert.Equal([1, 2, 3], ReadBytes(harness.PictureFile(TargetName)));
        Assert.False(Directory.Exists(harness.ArtistFolder(SourceName)));
        Assert.Equal(TargetName, after.Value("Playlists", harness.PlaylistId("Rescan classic"), "picture"));

        AssertSameExceptTouched(before, after, neighbourId);

        // The target folder processed again changes nothing either (second positive control).
        Assert.Equal(TargetNeighbourRetitled, afterTargetFolder.Value("Tracks", harness.TrackId(TargetNeighbourFile), "title"));
        Assert.Equal(1, targetFolderStatistics.TracksUpdated);
        Assert.Equal(0, targetFolderStatistics.TracksImported);
        Assert.Equal(0, targetFolderStatistics.AlbumsImported);
        Assert.Equal(0, targetFolderStatistics.ArtistsImported);
        Assert.Equal(0, targetFolderStatistics.TracksDeleted);
        Assert.Equal(0, targetFolderStatistics.AlbumsDeleted);
        Assert.Equal(0, targetFolderStatistics.ArtistsDeleted);
        AssertSameExceptTouched(after, afterTargetFolder, harness.TrackId(TargetNeighbourFile));
    }

    private static byte[] ReadBytes(string path)
    {
        return File.ReadAllBytes(path);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + 1, StringComparison.Ordinal);
        }

        return count;
    }

    private static void AssertSameExceptTouched(Snapshot expectedSnapshot, Snapshot actualSnapshot, long touchedId)
    {
        foreach (string table in SnapshotTables)
        {
            Dictionary<long, string> expected = expectedSnapshot.Rows(table);
            Dictionary<long, string> actual = actualSnapshot.Rows(table);

            if (table == "Tracks")
            {
                expected.Remove(touchedId);
                actual.Remove(touchedId);
            }

            Assert.Equal(expected, actual);
        }
    }

    private static string CreateMp3(string folder, string name, string[] albumArtists, string[] performers, string album)
    {
        string path = Mp3TagFixture.CreateWithArtists(folder, 3, albumArtists, performers, fileName: name + ".mp3");

        PrepareTags(path, name, album);

        return path;
    }

    private static string CreateFlac(string folder, string name, string[] albumArtists, string[] performers, string album)
    {
        string path = FlacTagFixture.CreateWithArtists(folder, albumArtists, performers, name + ".flac");

        PrepareTags(path, name, album);

        return path;
    }

    private static void PrepareTags(string path, string name, string album)
    {
        using TagLib.File file = TagLib.File.Create(path);

        file.Tag.Title = "Title " + name;
        file.Tag.Album = album;
        file.Tag.Genres = ["Rock"];

        file.Save();
    }

    private sealed class Snapshot(Dictionary<string, Dictionary<long, Dictionary<string, string>>> tables)
    {
        public Dictionary<long, string> Rows(string table)
        {
            return tables[table].ToDictionary(
                row => row.Key,
                row => string.Join("|", row.Value.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}")));
        }

        public string Value(string table, long id, string column)
        {
            return tables[table][id].Single(c => string.Equals(c.Key, column, StringComparison.OrdinalIgnoreCase)).Value;
        }
    }

    private sealed class SmartPlaylistRow
    {
        public string Name { get; set; } = string.Empty;

        public string GroupsJson { get; set; } = string.Empty;
    }

    private sealed class FailingTagWriter(ITagWriter inner) : ITagWriter
    {
        public string? FailingFileName { get; set; }

        public Task<IReadOnlyList<TagWriteCheck>> CheckAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
        {
            return inner.CheckAsync(paths, cancellationToken);
        }

        public async Task<IReadOnlyList<TagWriteResult>> WriteAsync(IReadOnlyList<TagWriteRequest> requests, CancellationToken cancellationToken)
        {
            List<TagWriteResult> results = [];

            foreach (TagWriteRequest request in requests)
            {
                if (FailingFileName is not null && Path.GetFileNameWithoutExtension(request.FilePath) == FailingFileName)
                {
                    results.Add(new TagWriteResult(request.FilePath, ETagWriteOutcome.Failed, ETagWriteReason.IoError, null, null));
                    continue;
                }

                results.AddRange(await inner.WriteAsync([request], cancellationToken));
            }

            return results;
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteDatabaseFixture _fixture = new();
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ArtistMergeRescanTests_");
        private readonly TrackRepository _trackRepository;
        private readonly AlbumRepository _albumRepository;
        private readonly ArtistRepository _artistRepository;
        private readonly GenreRepository _genreRepository;
        private readonly PlaylistHeaderRepository _playlistHeaderRepository;
        private readonly TagService _tagService = new(NullLogger<TagService>.Instance);
        private readonly FailingTagWriter _tagWriter;
        private readonly ArtistPicture _artistPicture;
        private readonly LibraryWriteGate _writeGate = new();
        private readonly AppOptions _options = new() { LibraryTokens = ["lib"], WriteTagsEnabled = true };
        private readonly Dictionary<string, string> _paths = [];
        private readonly Func<string, string, string[], string[], string, string> _createFile;

        public Harness(Func<string, string, string[], string[], string, string> createFile)
        {
            _createFile = createFile;

            _fixture.Connection.Execute("DELETE FROM Tracks; DELETE FROM Albums; DELETE FROM Artists; DELETE FROM Genres;");

            _options.SetCachePath(Path.Combine(_root.FullName, "cache"));

            _trackRepository = new TrackRepository(_fixture.Connection, _fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);
            _albumRepository = new AlbumRepository(_fixture.Connection, _fixture.Connection, NullLogger<AlbumRepository>.Instance, TimeProvider.System);
            _artistRepository = new ArtistRepository(_fixture.Connection, _fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);
            _genreRepository = new GenreRepository(_fixture.Connection, _fixture.Connection, NullLogger<GenreRepository>.Instance, TimeProvider.System);
            _playlistHeaderRepository = new PlaylistHeaderRepository(_fixture.Connection, _fixture.Connection, NullLogger<PlaylistHeaderRepository>.Instance, TimeProvider.System);
            _tagWriter = new FailingTagWriter(new TagLibTagWriter(Path.Combine(_root.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance));
            _artistPicture = new ArtistPicture(new DefaultFileSystem(), _options);

            // Two albums with the same name by two artists live in two folders: in one folder the import would take them for a compilation.
            _paths[SourceAlbumArtistFile] = createFile(SourceFolder, SourceAlbumArtistFile, [SourceName], ["Guest performer"], HomonymAlbum);
            _paths[SourcePerformerFile] = createFile(SourceFolder, SourcePerformerFile, [], [SourceName], SourceOwnAlbum);
            _paths[NeighbourFile] = createFile(SourceFolder, NeighbourFile, [], ["Neighbour artist"], NeighbourAlbum);
            _paths[TargetHomonymFile] = createFile(TargetFolder, TargetHomonymFile, [], [TargetName], HomonymAlbumOtherCase);
            _paths[TargetOwnFile] = createFile(TargetFolder, TargetOwnFile, [TargetName], ["Guest performer"], TargetOwnAlbum);
            _paths[TargetNeighbourFile] = createFile(TargetFolder, TargetNeighbourFile, [], ["Target neighbour artist"], TargetNeighbourAlbum);
        }

        private string LibraryFolder => Path.Combine(_root.FullName, "library");

        private string SourceFolder => Path.Combine(LibraryFolder, "source");

        private string TargetFolder => Path.Combine(LibraryFolder, "target");

        public T Scalar<T>(string sql, object? args = null)
        {
            return _fixture.Connection.ExecuteScalar<T>(sql, args)!;
        }

        public void AddFile(string folderName, string name, string artist, string album)
        {
            _paths[name] = _createFile(Path.Combine(LibraryFolder, folderName), name, [], [artist], album);
        }

        public List<long> AlbumIdsNamed(string album)
        {
            return _fixture.Connection.Query<long>("SELECT id FROM Albums WHERE name = @album COLLATE NOCASE ORDER BY id", new { album }).ToList();
        }

        public void SeedSameFolderAlbumData(string[] files)
        {
            DateTime now = DateTime.UtcNow;

            foreach (long albumId in AlbumIdsNamed(SameFolderAlbum))
            {
                long tagId = InsertTag("Album tag " + albumId);

                _fixture.Connection.Execute("UPDATE Albums SET isFavorite = 1, listenCount = 2, lastListen = @now WHERE id = @albumId", new { albumId, now });
                _fixture.Connection.Execute("INSERT INTO AlbumTags(albumId, tagId) VALUES (@albumId, @tagId)", new { albumId, tagId });
            }

            foreach (string file in files)
            {
                long trackId = TrackId(file);
                long artistId = Scalar<long>("SELECT artistId FROM Tracks WHERE id = @trackId", new { trackId });

                InsertEvent(trackId, artistId, AlbumIdOf(file), now);
            }
        }

        public long TrackId(string name)
        {
            return Scalar<long>("SELECT id FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}%" });
        }

        public long AlbumIdOf(string name)
        {
            return Scalar<long>("SELECT albumId FROM Tracks WHERE id = @id", new { id = TrackId(name) });
        }

        public long ArtistId(string name)
        {
            return Scalar<long>("SELECT id FROM Artists WHERE name = @name", new { name });
        }

        public long PlaylistId(string name)
        {
            return Scalar<long>("SELECT id FROM Playlists WHERE name = @name", new { name });
        }

        public string ReadArtistFromFile(string name)
        {
            using TagLib.File file = TagLib.File.Create(_paths[name]);

            return TagFieldReader.ReadArtist(file.Tag);
        }

        public string ArtistFolder(string artistName)
        {
            return _artistPicture.GetArtistFolder(artistName);
        }

        public string PictureFile(string artistName)
        {
            return _artistPicture.GetPictureFile(artistName);
        }

        public void SeedImages(string artistName, byte[] picture, string backdropName, byte[] backdrop)
        {
            Directory.CreateDirectory(_artistPicture.GetArtistFolder(artistName));

            File.WriteAllBytes(_artistPicture.GetPictureFile(artistName), picture);
            File.WriteAllBytes(Path.Combine(_artistPicture.GetArtistFolder(artistName), backdropName), backdrop);
        }

        public void SeedData(long sourceId, long targetId)
        {
            long sourceTrackId = TrackId(SourceAlbumArtistFile);
            long sourceAlbumId = AlbumIdOf(SourceAlbumArtistFile);
            long targetTrackId = TrackId(TargetHomonymFile);
            long targetAlbumId = AlbumIdOf(TargetHomonymFile);
            DateTime now = DateTime.UtcNow;

            _fixture.Connection.Execute("UPDATE Artists SET isFavorite = 1, listenCount = 3, lastListen = @lastListen WHERE id = @sourceId", new { sourceId, lastListen = now.AddDays(-1) });
            _fixture.Connection.Execute("UPDATE Artists SET isFavorite = 0, listenCount = 5, lastListen = @lastListen WHERE id = @targetId", new { targetId, lastListen = now.AddDays(-3) });
            _fixture.Connection.Execute("UPDATE Albums SET isFavorite = 1, listenCount = 2, lastListen = @lastListen WHERE id = @sourceAlbumId", new { sourceAlbumId, lastListen = now.AddDays(-1) });
            _fixture.Connection.Execute("UPDATE Albums SET isFavorite = 0, listenCount = 4, lastListen = @lastListen WHERE id = @targetAlbumId", new { targetAlbumId, lastListen = now.AddDays(-3) });

            for (int i = 0; i < 2; i++)
            {
                InsertEvent(sourceTrackId, sourceId, sourceAlbumId, now.AddMinutes(-i));
            }

            InsertEvent(targetTrackId, targetId, targetAlbumId, now.AddMinutes(-5));

            long sourceTag = InsertTag("Source tag");
            long targetTag = InsertTag("Target tag");
            long sourceAlbumTag = InsertTag("Source album tag");
            long targetAlbumTag = InsertTag("Target album tag");

            _fixture.Connection.Execute("INSERT INTO ArtistTags(artistId, tagId) VALUES (@sourceId, @sourceTag), (@targetId, @targetTag)", new { sourceId, sourceTag, targetId, targetTag });
            _fixture.Connection.Execute("INSERT INTO AlbumTags(albumId, tagId) VALUES (@sourceAlbumId, @sourceAlbumTag), (@targetAlbumId, @targetAlbumTag)", new { sourceAlbumId, sourceAlbumTag, targetAlbumId, targetAlbumTag });

            _fixture.Connection.Execute("INSERT INTO EqualizerPreset(Scope, ScopeId, Band0, Band5) VALUES ('Artist', @sourceId, 3.5, -2.5)", new { sourceId });
            _fixture.Connection.Execute("INSERT INTO EqualizerPreset(Scope, ScopeId, Band0, Band5) VALUES ('Artist', @targetId, 1.5, -0.5)", new { targetId });
            _fixture.Connection.Execute("INSERT INTO EqualizerPreset(Scope, ScopeId, Band0, Band5) VALUES ('Album', @sourceAlbumId, 6.5, 0)", new { sourceAlbumId });

            SeedClassicPlaylist(SourceName);
        }

        public long SeedClassicPlaylist(string picture)
        {
            return InsertPlaylist("Rescan classic", picture, (int)PlaylistType.Classic, string.Empty);
        }

        public void SeedSmartPlaylists()
        {
            foreach ((string name, SmartPlaylistOperator op, string value) in new[]
            {
                ("Smart equals", SmartPlaylistOperator.Equals, SourceName),
                ("Smart not equals", SmartPlaylistOperator.NotEquals, SourceName),
                ("Smart contains", SmartPlaylistOperator.Contains, SourceName),
                ("Smart target equals", SmartPlaylistOperator.Equals, TargetName)
            })
            {
                List<PlaylistGroupDto> groups =
                [
                    new()
                    {
                        Name = "group",
                        Filters =
                        [
                            new()
                            {
                                Entity = SmartPlaylistEntity.Artists,
                                Field = SmartPlaylistField.Name,
                                FieldType = SmartPlaylistFieldType.String,
                                Operator = op,
                                Value = value
                            }
                        ]
                    }
                ];

                InsertPlaylist(name, string.Empty, (int)PlaylistType.Smart, JsonSerializer.Serialize(groups));
            }
        }

        public string CriteriaJson(string playlistName)
        {
            return _fixture.Connection.ExecuteScalar<string>("SELECT groupsJson FROM Playlists WHERE name = @playlistName", new { playlistName }) ?? string.Empty;
        }

        public async Task<Dictionary<string, long[]>> GenerateSmartPlaylistsAsync()
        {
            PlaylistTrackGenerateRepository repository = new(_fixture.Connection, _fixture.Connection, NullLogger<PlaylistTrackGenerateRepository>.Instance, TimeProvider.System);
            Dictionary<string, long[]> result = [];

            foreach (SmartPlaylistRow row in _fixture.Connection.Query<SmartPlaylistRow>("SELECT name, groupsJson FROM Playlists WHERE type = 0"))
            {
                List<PlaylistGroupDto> groups = JsonSerializer.Deserialize<List<PlaylistGroupDto>>(row.GroupsJson)!;
                List<long> ids = [];

                foreach (PlaylistGroupDto group in groups)
                {
                    ids.AddRange((await repository.GenerateAsync(new GeneratePlaylistTracksRequest { PlaylistTrackCount = 1000, Group = group })).Select(t => t.Id));
                }

                result[row.Name] = ids.Order().ToArray();
            }

            return result;
        }

        public async Task ImportTwiceAsync()
        {
            await ImportAsync();
            await ImportAsync();
        }

        public async Task<ImportStatisticsDto> ImportAsync()
        {
            Mock<IFolderResolver> folderResolver = new();
            folderResolver.Setup(r => r.GetPathFromTokenAsync("lib")).ReturnsAsync([LibraryFolder]);

            ImportService import = CreateImportService(folderResolver.Object);

            await import.ImportAsync(CancellationToken.None);

            return import.Statistics;
        }

        public async Task<ArtistMergeOutcome> MergeAsync(long sourceId, long targetId)
        {
            Mock<IPlayerService> player = new();
            Mock<IImport> import = new();
            import.SetupGet(i => i.UpdateInProgress).Returns(false);

            ArtistMergeService service = new(
                _artistRepository,
                _trackRepository,
                _albumRepository,
                _playlistHeaderRepository,
                new ArtistMergeRepository(_fixture.Connection, NullLogger<ArtistMergeRepository>.Instance),
                new ArtistRenameRepository(_fixture.Connection, NullLogger<ArtistRenameRepository>.Instance),
                new ArtistFileRewriter(_tagWriter, _trackRepository, player.Object, NullLogger<ArtistFileRewriter>.Instance),
                _artistPicture,
                import.Object,
                _writeGate,
                _options,
                NullLogger<ArtistMergeService>.Instance);

            Result<ArtistMergeOutcome> result = await service.MergeAsync(sourceId, targetId, null, CancellationToken.None);

            Assert.True(result.IsSuccess);

            return result.Value;
        }

        public async Task<ArtistRenameOutcome> ResumeAsync(long targetId)
        {
            Mock<IPlayerService> player = new();
            Mock<IImport> import = new();
            import.SetupGet(i => i.UpdateInProgress).Returns(false);

            ArtistRenameService service = new(
                _artistRepository,
                _trackRepository,
                _playlistHeaderRepository,
                new ArtistRenameRepository(_fixture.Connection, NullLogger<ArtistRenameRepository>.Instance),
                new ArtistFileRewriter(_tagWriter, _trackRepository, player.Object, NullLogger<ArtistFileRewriter>.Instance),
                _artistPicture,
                import.Object,
                _writeGate,
                _options,
                NullLogger<ArtistRenameService>.Instance);

            Result<ArtistRenameOutcome> result = await service.RenameAsync(targetId, TargetName, null, CancellationToken.None);

            Assert.True(result.IsSuccess);

            return result.Value;
        }

        public void FailWritesTo(string name)
        {
            _tagWriter.FailingFileName = name;
        }

        public void StopFailingWrites()
        {
            _tagWriter.FailingFileName = null;
        }

        public void Touch(string name, int days)
        {
            File.SetLastWriteTimeUtc(_paths[name], DateTime.UtcNow.AddDays(days));
        }

        public void RetitleNeighbourAsAnotherReader()
        {
            Retitle(NeighbourFile, NeighbourRetitled, 2);
        }

        public void RetitleTargetNeighbourAsAnotherReader()
        {
            Retitle(TargetNeighbourFile, TargetNeighbourRetitled, 2);
        }

        public void Retitle(string name, string title, int days)
        {
            using (TagLib.File file = TagLib.File.Create(_paths[name]))
            {
                file.Tag.Title = title;
                file.Save();
            }

            Touch(name, days);
        }

        public Snapshot TakeSnapshot()
        {
            Dictionary<string, Dictionary<long, Dictionary<string, string>>> tables = [];

            foreach (string table in SnapshotTables)
            {
                Dictionary<long, Dictionary<string, string>> rows = [];
                long index = 0;

                foreach (IDictionary<string, object?> row in _fixture.Connection.Query($"SELECT * FROM {table} ORDER BY 1, 2").Cast<IDictionary<string, object?>>())
                {
                    Dictionary<string, string> values = row.ToDictionary(
                        c => c.Key,
                        c => Convert.ToString(c.Value, CultureInfo.InvariantCulture) ?? "<null>");

                    string? idKey = row.Keys.FirstOrDefault(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase));

                    rows[idKey is null ? index++ : Convert.ToInt64(row[idKey], CultureInfo.InvariantCulture)] = values;
                }

                tables[table] = rows;
            }

            return new Snapshot(tables);
        }

        public void Dispose()
        {
            _fixture.Dispose();

            try
            {
                _root.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private void InsertEvent(long trackId, long artistId, long albumId, DateTime playedAt)
        {
            _fixture.Connection.Execute(
                "INSERT INTO ListeningEvents(trackId, artistId, albumId, playedAt, wasSkipped, durationPlayed, durationTotal) VALUES (@trackId, @artistId, @albumId, @playedAt, 0, 120, 180)",
                new { trackId, artistId, albumId, playedAt });
        }

        private long InsertTag(string name)
        {
            return _fixture.Connection.ExecuteScalar<long>("INSERT INTO Tags(name) VALUES (@name); SELECT last_insert_rowid();", new { name });
        }

        private long InsertPlaylist(string name, string picture, int type, string groupsJson)
        {
            return _fixture.Connection.ExecuteScalar<long>(
                "INSERT INTO Playlists(name, picture, trackMaximum, durationMaximum, trackCount, duration, groupsJson, type, creatDate) VALUES (@name, @picture, 0, 0, 0, 0, @groupsJson, @type, @now); SELECT last_insert_rowid();",
                new { name, picture, groupsJson, type, now = DateTime.UtcNow });
        }

        private ImportService CreateImportService(IFolderResolver folderResolver)
        {
            TimeProvider time = TimeProvider.System;
            Mock<IMessenger> messenger = new();
            Mock<IMediator> mediator = new();
            Mock<ILyricsService> lyrics = new();
            Mock<ITelemetryClient> telemetry = new();

            TrackImport trackImport = new(_trackRepository);
            AlbumImport albumImport = new(_albumRepository, time);
            ArtistImport artistImport = new(_artistRepository, time);
            GenreImport genreImport = new(_genreRepository, time);
            ImportTrackingService tracking = new();
            ImportMessageThrottler throttler = new();
            FileSystemService fileSystem = new(NullLogger<FileSystemService>.Instance);

            FolderImportProcessor folderProcessor = new(
                _options,
                trackImport,
                artistImport,
                albumImport,
                genreImport,
                fileSystem,
                new TrackFileProcessor(_tagService, NullLogger<TrackFileProcessor>.Instance),
                new TrackMetadataService(trackImport, NullLogger<TrackMetadataService>.Instance, time),
                new EmbeddedLyricsImporter(lyrics.Object, NullLogger<EmbeddedLyricsImporter>.Instance),
                tracking,
                _tagService,
                throttler,
                messenger.Object,
                NullLogger<FolderImportProcessor>.Instance);

            PostImportDominantColorTask dominantColor = new(
                Mock.Of<IDominantColorCalculator>(),
                _albumRepository,
                Mock.Of<IAlbumPictureService>(),
                _artistRepository,
                Mock.Of<IArtistPictureService>(),
                NullLogger<PostImportDominantColorTask>.Instance);

            PostImportApiEnrichmentTask apiEnrichment = new(
                artistImport,
                albumImport,
                Mock.Of<IArtistApiService>(),
                Mock.Of<IAlbumApiService>(),
                Mock.Of<IArtistPictureService>(),
                Mock.Of<IAlbumPictureService>(),
                Mock.Of<IBackdropPicture>(),
                mediator.Object,
                NullLogger<PostImportApiEnrichmentTask>.Instance);

            return new ImportService(
                folderResolver,
                _options,
                new Statistics(_trackRepository, _albumRepository, _artistRepository, _genreRepository, NullLogger<Statistics>.Instance),
                trackImport,
                genreImport,
                albumImport,
                artistImport,
                new CleanLibraryService(_trackRepository, _artistRepository, _albumRepository, _genreRepository, NullLogger<CleanLibraryService>.Instance),
                telemetry.Object,
                new ImportProgressService(messenger.Object),
                tracking,
                fileSystem,
                folderProcessor,
                dominantColor,
                apiEnrichment,
                throttler,
                _writeGate,
                NullLogger<ImportService>.Instance);
        }
    }
}