using System.Globalization;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Merges two albums through <see cref="AlbumMergeService"/>, then runs the real library import again and checks that the rescan leaves the database as the merge left it.
/// </summary>
public sealed class AlbumMergeRescanTests
{
    private const string SourceAlbum = "Source Album";
    private const string ExistingAlbum = "Existing Album";
    private const string SourceFirst = "b1-track";
    private const string SourceSecond = "b2-track";
    private const string ExistingSecond = "e2-track";
    private const string NeighbourRetitled = "Neighbour retitled by another reader";
    private const int TargetEvents = 3;

    [Fact(DisplayName = "merge_albums_of_the_same_artist_and_folder_then_rescan_changes_nothing_flac")]
    public async Task Rescan_AfterMergeInTheSameFolderFlac_ShouldChangeNothing()
    {
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);

        await AssertSameFolderMergeThenRescanAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "merge_albums_of_the_same_artist_and_folder_then_rescan_changes_nothing_mp3")]
    public async Task Rescan_AfterMergeInTheSameFolderMp3_ShouldChangeNothing()
    {
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateMp3);

        await AssertSameFolderMergeThenRescanAsync(harness);
    }

    [Fact(DisplayName = "merge_albums_of_different_artists_and_folders_then_rescan_changes_nothing")]
    public async Task Rescan_AfterMergeOfDifferentArtistsAndFolders_ShouldChangeNothing()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);
        harness.AddFile("existing", ExistingSecond, ExistingAlbum, AlbumTagWriteHarness.ExistingArtist, AlbumTagWriteHarness.OldGenre, 2001);
        await harness.ImportTwiceAsync();
        long targetId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long sourceId = harness.AlbumIdOf(AlbumTagWriteHarness.ExistingFile);
        long oldArtistId = harness.ArtistId(AlbumTagWriteHarness.OldArtist);
        long existingArtistId = harness.ArtistId(AlbumTagWriteHarness.ExistingArtist);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        string[] sourceFiles = [AlbumTagWriteHarness.ExistingFile, ExistingSecond];
        long[] sourceIds = sourceFiles.Select(harness.TrackId).ToArray();
        SeedBoth(harness, targetId, sourceId, sourceFiles);

        AlbumMergePreview preview = await harness.PreviewMergeAsync(sourceId, targetId);

        // Act
        Result<AlbumTagWriteOutcome> result = await harness.MergeAsync(sourceId, targetId, false);
        WriteSnapshot before = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);

        foreach (string file in sourceFiles)
        {
            harness.Touch(file, 3);
        }

        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.True(preview.ArtistChanges);
        Assert.True(preview.SeparateFolders);
        Assert.NotNull(preview.OrphanArtist);
        Assert.Equal(2, preview.FileCount);
        Assert.True(result.IsSuccess);
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(targetId, result.Value.AlbumId);
        Assert.Equal([sourceId], result.Value.DeletedAlbumIds);
        Assert.Equal(0, harness.PendingCount());

        foreach (string file in sourceFiles)
        {
            FileTags tags = harness.ReadFile(file);

            Assert.Equal(AlbumTagWriteHarness.AlbumName, tags.Album);
            Assert.Equal(AlbumTagWriteHarness.OldArtist, tags.Artist);
            Assert.Equal(targetId, harness.AlbumIdOf(file));
            Assert.Equal(oldArtistId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(file), "artistId"));
        }

        Assert.Equal("6", before.Value("Artists", oldArtistId, "trackCount"));
        Assert.Equal("2", before.Value("Artists", oldArtistId, "albumCount"));
        Assert.Equal("0", before.Value("Artists", existingArtistId, "trackCount"));
        Assert.Equal("0", before.Value("Artists", existingArtistId, "albumCount"));
        Assert.Equal(TargetEvents + 2, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @targetId AND artistId = @oldArtistId", new { targetId, oldArtistId }));

        // Positive control: the folders were processed again.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 1);
        Assert.False(after.Rows("Artists").ContainsKey(existingArtistId));
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = ExistingAlbum }));
        Assert.Equal("0", after.Value("Albums", targetId, "isCompilation"));
        AssertMergedUserData(harness, after, targetId, sourceId, TargetEvents + 2);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after, ["Artists"], [neighbourId, .. sourceIds]);
    }

    [Fact(DisplayName = "merge_albums_with_colliding_track_numbers_is_refused_then_shifted_and_rescan_changes_nothing")]
    public async Task Rescan_AfterMergeWithDiscShift_ShouldChangeNothing()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);
        harness.AddFile("second", SourceFirst, SourceAlbum, AlbumTagWriteHarness.OldArtist, AlbumTagWriteHarness.OldGenre, 1999, 1, 1);
        harness.AddFile("second", SourceSecond, SourceAlbum, AlbumTagWriteHarness.OldArtist, AlbumTagWriteHarness.OldGenre, 1999, 1, 2);
        harness.SetNumbers(AlbumTagWriteHarness.FirstFile, 1, 1);
        harness.SetNumbers(AlbumTagWriteHarness.SecondFile, 1, 2);
        harness.SetNumbers(AlbumTagWriteHarness.OwnGenreFile, 1, 3);
        await harness.ImportTwiceAsync();
        long targetId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long sourceId = harness.AlbumIdOf(SourceFirst);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        string[] sourceFiles = [SourceFirst, SourceSecond];
        long[] sourceIds = sourceFiles.Select(harness.TrackId).ToArray();
        SeedBoth(harness, targetId, sourceId, sourceFiles);

        AlbumMergePreview preview = await harness.PreviewMergeAsync(sourceId, targetId);
        WriteSnapshot untouched = harness.TakeSnapshot();

        // Act
        Result<AlbumTagWriteOutcome> refused = await harness.MergeAsync(sourceId, targetId, false);
        WriteSnapshot afterRefusal = harness.TakeSnapshot();
        FileTags fileAfterRefusal = harness.ReadFile(SourceFirst);
        uint discAfterRefusal = harness.ReadDisc(SourceFirst);
        Result<AlbumTagWriteOutcome> merged = await harness.MergeAsync(sourceId, targetId, true);
        WriteSnapshot before = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);

        foreach (string file in sourceFiles)
        {
            harness.Touch(file, 3);
        }

        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(2, preview.DiscConflicts.Count);
        Assert.Equal(1, preview.SuggestedDiscOffset);
        Assert.True(refused.IsFailure);
        Assert.Equal("album.merge.conflicts", refused.Errors[0].Code);
        AlbumTagWriteHarness.AssertSameExceptTouched(untouched, afterRefusal);
        Assert.Equal(SourceAlbum, fileAfterRefusal.Album);
        Assert.Equal(1u, discAfterRefusal);

        Assert.True(merged.IsSuccess);
        Assert.Equal(EArtistRenameState.Completed, merged.Value.State);
        Assert.Equal(0, harness.PendingCount());

        foreach (string file in sourceFiles)
        {
            Assert.Equal(AlbumTagWriteHarness.AlbumName, harness.ReadFile(file).Album);
            Assert.Equal(2u, harness.ReadDisc(file));
            Assert.Equal("2", after.Value("Tracks", harness.TrackId(file), "disc"));
            Assert.Equal(targetId, harness.AlbumIdOf(file));
        }

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
        {
            Assert.Equal(1u, harness.ReadDisc(file));
            Assert.Equal("1", after.Value("Tracks", harness.TrackId(file), "disc"));
        }

        Assert.Equal("2", before.Value("Albums", targetId, "discCount"));

        // Positive control: the folders were processed again.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 0);
        Assert.Equal("2", after.Value("Albums", targetId, "discCount"));
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = SourceAlbum }));
        Assert.Equal("0", after.Value("Albums", targetId, "isCompilation"));
        AssertMergedUserData(harness, after, targetId, sourceId, TargetEvents + 2);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after, [neighbourId, .. sourceIds]);
    }

    [Fact(DisplayName = "merge_albums_partially_written_then_rescan_never_recreates_the_source_and_resume_completes")]
    public async Task Rescan_AfterPartialMerge_ShouldNeverRecreateTheSourceAndResumeShouldComplete()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);
        harness.AddFile("existing", ExistingSecond, ExistingAlbum, AlbumTagWriteHarness.ExistingArtist, AlbumTagWriteHarness.OldGenre, 2001);
        await harness.ImportTwiceAsync();
        long targetId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long sourceId = harness.AlbumIdOf(AlbumTagWriteHarness.ExistingFile);
        long oldArtistId = harness.ArtistId(AlbumTagWriteHarness.OldArtist);
        long existingArtistId = harness.ArtistId(AlbumTagWriteHarness.ExistingArtist);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        long failedId = harness.TrackId(ExistingSecond);
        long doneId = harness.TrackId(AlbumTagWriteHarness.ExistingFile);
        SeedBoth(harness, targetId, sourceId, [AlbumTagWriteHarness.ExistingFile, ExistingSecond]);
        harness.FailWritesTo(ExistingSecond);

        // Act
        Result<AlbumTagWriteOutcome> partial = await harness.MergeAsync(sourceId, targetId, false);
        WriteSnapshot before = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);
        harness.Touch(ExistingSecond, 2);
        harness.Touch(AlbumTagWriteHarness.ExistingFile, 2);
        ImportStatisticsDto firstRescan = await harness.ImportAsync();
        WriteSnapshot afterRescan = harness.TakeSnapshot();
        FileTags failedFileAfterRescan = harness.ReadFile(ExistingSecond);
        FileTags doneFileAfterRescan = harness.ReadFile(AlbumTagWriteHarness.ExistingFile);
        harness.StopFailingWrites();

        AlbumTagWriteOutcome resumed = await harness.ResumeAsync(targetId);
        WriteSnapshot afterResume = harness.TakeSnapshot();
        harness.Retitle(ExistingSecond, "Retitled by another reader", 4);
        ImportStatisticsDto finalRescan = await harness.ImportAsync();
        WriteSnapshot final = harness.TakeSnapshot();

        // Assert
        Assert.True(partial.IsSuccess);
        Assert.Equal(EArtistRenameState.Partial, partial.Value.State);
        Assert.Single(before.Rows("PendingTagWrites"));

        // First rescan: the file still tagged with the old values does not bring the source album back.
        Assert.Equal(NeighbourRetitled, afterRescan.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, firstRescan.TracksUpdated);
        AssertNothingRecreated(firstRescan, artistsDeleted: 1);
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = ExistingAlbum }));
        Assert.Equal(targetId, harness.AlbumIdOf(ExistingSecond));
        Assert.Equal(oldArtistId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", failedId, "artistId"));
        Assert.Equal("0", afterRescan.Value("Albums", targetId, "isCompilation"));
        Assert.Single(afterRescan.Rows("PendingTagWrites"));
        Assert.False(afterRescan.Rows("Artists").ContainsKey(existingArtistId));
        Assert.Equal(ExistingAlbum, failedFileAfterRescan.Album);
        Assert.Equal(AlbumTagWriteHarness.ExistingArtist, failedFileAfterRescan.Artist);
        Assert.Equal(AlbumTagWriteHarness.AlbumName, doneFileAfterRescan.Album);
        AssertMergedUserData(harness, afterRescan, targetId, sourceId, TargetEvents + 2);

        // Resume.
        Assert.Equal(EArtistRenameState.Completed, resumed.State);
        Assert.Equal(0, harness.PendingCount());

        foreach (string file in new[] { AlbumTagWriteHarness.ExistingFile, ExistingSecond })
        {
            FileTags tags = harness.ReadFile(file);

            Assert.Equal(AlbumTagWriteHarness.AlbumName, tags.Album);
            Assert.Equal(AlbumTagWriteHarness.OldArtist, tags.Artist);
        }

        // Positive control: the file written by the resume was changed by another reader and taken over.
        Assert.Equal("Retitled by another reader", final.Value("Tracks", failedId, "title"));
        Assert.Equal(1, finalRescan.TracksUpdated);
        AssertNothingRecreated(finalRescan, artistsDeleted: 0);
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = ExistingAlbum }));
        Assert.Equal(targetId, harness.AlbumIdOf(ExistingSecond));
        Assert.Equal("0", final.Value("Albums", targetId, "isCompilation"));
        AssertMergedUserData(harness, final, targetId, sourceId, TargetEvents + 2);
        harness.AssertCountersMatchTracks();
        Assert.Equal("2", final.Value("Genres", harness.GenreId(AlbumTagWriteHarness.OldGenre), "artistCount"));
        AlbumTagWriteHarness.AssertSameExceptTouched(afterResume, final, ["Genres"], [failedId, doneId]);
    }

    private static async Task AssertSameFolderMergeThenRescanAsync(AlbumTagWriteHarness harness)
    {
        // Arrange
        harness.AddFile("album", SourceFirst, SourceAlbum, AlbumTagWriteHarness.OldArtist, AlbumTagWriteHarness.OldGenre, 2003, 2, 1);
        harness.AddFile("album", SourceSecond, SourceAlbum, AlbumTagWriteHarness.OldArtist, AlbumTagWriteHarness.OldGenre, 2003, 2, 2);
        await harness.ImportTwiceAsync();
        long targetId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long sourceId = harness.AlbumIdOf(SourceFirst);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        int albumCount = harness.Scalar<int>("SELECT COUNT(*) FROM Albums");
        SeedBoth(harness, targetId, sourceId, [SourceFirst, SourceSecond]);

        // Act
        Result<AlbumTagWriteOutcome> result = await harness.MergeAsync(sourceId, targetId, false);
        WriteSnapshot before = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(targetId, result.Value.AlbumId);
        Assert.Equal([sourceId], result.Value.DeletedAlbumIds);
        Assert.Equal(0, harness.PendingCount());

        foreach (string file in new[] { SourceFirst, SourceSecond })
        {
            FileTags tags = harness.ReadFile(file);

            Assert.Equal(AlbumTagWriteHarness.AlbumName, tags.Album);
            Assert.Equal(AlbumTagWriteHarness.OldArtist, tags.Artist);
            Assert.Equal(2003, tags.Year);
            Assert.Equal(2u, harness.ReadDisc(file));
            Assert.Equal(targetId, harness.AlbumIdOf(file));
        }

        Assert.Equal("1999", before.Value("Albums", targetId, "year"));

        // Positive control: the folder was processed again, so the neighbour changed by another reader was taken over.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 0);
        Assert.Equal(albumCount - 1, after.Rows("Albums").Count);
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = SourceAlbum }));
        Assert.Equal("1999", after.Value("Albums", targetId, "year"));
        Assert.Equal("0", after.Value("Albums", targetId, "isCompilation"));
        AssertMergedUserData(harness, after, targetId, sourceId, TargetEvents + 2);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after, neighbourId);
    }

    private static void SeedBoth(AlbumTagWriteHarness harness, long targetId, long sourceId, string[] sourceFiles)
    {
        harness.SeedAlbumData(targetId, AlbumTagWriteHarness.AlbumFiles, "Target tag", false, 7, 6.5);
        harness.SeedAlbumData(sourceId, sourceFiles, "Source tag", true, 5, 3.0);
    }

    private static void AssertNothingRecreated(ImportStatisticsDto statistics, int artistsDeleted)
    {
        Assert.Equal(0, statistics.TracksImported);
        Assert.Equal(0, statistics.AlbumsImported);
        Assert.Equal(0, statistics.ArtistsImported);
        Assert.Equal(0, statistics.GenresImported);
        Assert.Equal(0, statistics.TracksDeleted);
        Assert.Equal(0, statistics.AlbumsDeleted);
        Assert.Equal(artistsDeleted, statistics.ArtistsDeleted);
        Assert.Equal(0, statistics.GenresDeleted);
    }

    private static void AssertMergedUserData(AlbumTagWriteHarness harness, WriteSnapshot snapshot, long targetId, long sourceId, int events)
    {
        Assert.False(snapshot.Rows("Albums").ContainsKey(sourceId));
        Assert.Equal("1", snapshot.Value("Albums", targetId, "isFavorite"));
        Assert.Equal("12", snapshot.Value("Albums", targetId, "listenCount"));
        Assert.Equal(2, harness.Scalar<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @targetId", new { targetId }));
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @sourceId", new { sourceId }));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = 'Album' AND ScopeId = @targetId", new { targetId }));
        Assert.Equal(6.5, harness.Scalar<double>("SELECT Band0 FROM EqualizerPreset WHERE Scope = 'Album' AND ScopeId = @targetId", new { targetId }));
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = 'Album' AND ScopeId = @sourceId", new { sourceId }));
        Assert.Equal(events, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @targetId", new { targetId }));
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @sourceId", new { sourceId }));
        Assert.Equal(events, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents"));
    }
}