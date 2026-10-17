using System.Globalization;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Edits an album through <see cref="AlbumEditService"/>, then runs the real library import again and checks that the rescan leaves the database as the edit left it.
/// </summary>
public sealed class AlbumEditRescanTests
{
    private const string NewAlbumName = "Brand New Album";
    private const string NewArtistName = "Brand New Artist";
    private const string NewGenre = "Blues";
    private const int NewYear = 2005;
    private const string NeighbourRetitled = "Neighbour retitled by another reader";

    [Fact(DisplayName = "rename_album_then_rescan_changes_nothing_flac")]
    public async Task Rescan_AfterRenameFlac_ShouldChangeNothing()
    {
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);

        await AssertRenameThenRescanAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "rename_album_then_rescan_changes_nothing_mp3")]
    public async Task Rescan_AfterRenameMp3_ShouldChangeNothing()
    {
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateMp3);

        await AssertRenameThenRescanAsync(harness);
    }

    [Fact(DisplayName = "rename_album_then_rescan_changes_nothing_when_tracks_take_the_genre_of_their_artist")]
    public async Task Rescan_AfterRenameWithArtistGenreOption_ShouldChangeNothing()
    {
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac, importTrackWithArtistGenre: true);

        await AssertRenameThenRescanAsync(harness, genreFromArtist: true);
    }

    [Fact(DisplayName = "rescan_without_any_edit_recreates_the_own_genre_of_a_track_when_tracks_take_the_genre_of_their_artist")]
    public async Task Rescan_WithoutEditAndArtistGenreOption_ShouldRecreateTheOwnGenre()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac, importTrackWithArtistGenre: true);
        await harness.ImportTwiceAsync();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);

        // Act
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.Equal(1, rescan.GenresImported);
        Assert.Equal(1, rescan.GenresDeleted);
    }

    [Fact(DisplayName = "genre_edit_is_refused_and_leaves_database_and_files_untouched_when_tracks_take_the_genre_of_their_artist")]
    public async Task Edit_GenreChangeWithArtistGenreOption_ShouldBeRefusedWithoutAnyWrite()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac, importTrackWithArtistGenre: true);
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        WriteSnapshot before = harness.TakeSnapshot();
        string genreBefore = harness.ReadFile(AlbumTagWriteHarness.FirstFile).Genre;

        // Act
        Result<AlbumTagWriteOutcome> result = await harness.TryEditAsync(albumId, AlbumTagWriteHarness.AlbumName, AlbumTagWriteHarness.OldArtist, null, NewGenre);
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("album.edit.genre_from_artist", result.Errors.Single().Code);
        Assert.Equal(genreBefore, harness.ReadFile(AlbumTagWriteHarness.FirstFile).Genre);
        Assert.Equal(0, harness.PendingCount());
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Genres WHERE name = @name", new { name = NewGenre }));
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after);
    }

    [Fact(DisplayName = "change_album_artist_to_an_existing_artist_then_rescan_changes_nothing")]
    public async Task Rescan_AfterArtistChangeToExistingArtist_ShouldChangeNothing()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long oldArtistId = harness.ArtistId(AlbumTagWriteHarness.OldArtist);
        long existingId = harness.ArtistId(AlbumTagWriteHarness.ExistingArtist);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        harness.SeedAlbumData(albumId);

        // Act
        AlbumTagWriteOutcome outcome = await harness.EditAsync(albumId, AlbumTagWriteHarness.AlbumName, AlbumTagWriteHarness.ExistingArtist, null, null);
        WriteSnapshot before = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(0, harness.PendingCount());
        Assert.Equal(existingId.ToString(CultureInfo.InvariantCulture), after.Value("Albums", albumId, "artistId"));

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
        {
            Assert.Equal(existingId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(file), "artistId"));
            Assert.Equal(AlbumTagWriteHarness.ExistingArtist, harness.ReadFile(file).Artist);
        }

        Assert.Equal(3, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @albumId AND artistId = @existingId", new { albumId, existingId }));

        // Counters of both artists.
        Assert.Equal("1", before.Value("Artists", oldArtistId, "trackCount"));
        Assert.Equal("1", before.Value("Artists", oldArtistId, "albumCount"));
        Assert.Equal("4", before.Value("Artists", existingId, "trackCount"));
        Assert.Equal("2", before.Value("Artists", existingId, "albumCount"));
        harness.AssertCountersMatchTracks();

        // Positive control: the folder was processed again.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 0);
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = AlbumTagWriteHarness.AlbumName }));
        Assert.Equal("0", after.Value("Albums", albumId, "isCompilation"));
        AssertUserDataKept(harness, after, albumId);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after, neighbourId);
    }

    [Fact(DisplayName = "change_album_artist_leaving_the_old_artist_without_album_deletes_it_at_rescan")]
    public async Task Rescan_AfterArtistChangeEmptyingTheOldArtist_ShouldDeleteOnlyTheOldArtist()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac, oldArtistHasOtherAlbum: false);
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long oldArtistId = harness.ArtistId(AlbumTagWriteHarness.OldArtist);
        long existingId = harness.ArtistId(AlbumTagWriteHarness.ExistingArtist);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        harness.SeedAlbumData(albumId);

        // Act
        AlbumTagWriteOutcome outcome = await harness.EditAsync(albumId, AlbumTagWriteHarness.AlbumName, AlbumTagWriteHarness.ExistingArtist, null, null);
        WriteSnapshot before = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal("0", before.Value("Artists", oldArtistId, "trackCount"));
        Assert.Equal("0", before.Value("Artists", oldArtistId, "albumCount"));
        Assert.Equal("4", before.Value("Artists", existingId, "trackCount"));
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 1);
        Assert.False(after.Rows("Artists").ContainsKey(oldArtistId));
        Assert.Equal(existingId.ToString(CultureInfo.InvariantCulture), after.Value("Albums", albumId, "artistId"));
        AssertUserDataKept(harness, after, albumId);
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "change_album_artist_to_a_new_artist_creates_it")]
    public async Task Rescan_AfterArtistChangeToNewArtist_ShouldNotCreateItAgain()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        int artistCount = harness.Scalar<int>("SELECT COUNT(*) FROM Artists");
        harness.SeedAlbumData(albumId);

        // Act
        AlbumTagWriteOutcome outcome = await harness.EditAsync(albumId, AlbumTagWriteHarness.AlbumName, NewArtistName, null, null);
        WriteSnapshot before = harness.TakeSnapshot();
        long newArtistId = harness.ArtistId(NewArtistName);
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(artistCount + 1, before.Rows("Artists").Count);
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Artists WHERE name = @name COLLATE NOCASE", new { name = NewArtistName }));
        Assert.Equal(newArtistId.ToString(CultureInfo.InvariantCulture), before.Value("Albums", albumId, "artistId"));
        Assert.Equal("3", before.Value("Artists", newArtistId, "trackCount"));
        Assert.Equal("1", before.Value("Artists", newArtistId, "albumCount"));

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
        {
            Assert.Equal(NewArtistName, harness.ReadFile(file).Artist);
        }

        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 0);
        Assert.Equal(artistCount + 1, after.Rows("Artists").Count);
        AssertUserDataKept(harness, after, albumId);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after, neighbourId);
    }

    [Fact(DisplayName = "change_year_and_genre_then_rescan_changes_nothing_flac")]
    public async Task Rescan_AfterYearAndGenreChangeFlac_ShouldChangeNothing()
    {
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);

        await AssertYearAndGenreThenRescanAsync(harness);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "change_year_and_genre_then_rescan_changes_nothing_mp3")]
    public async Task Rescan_AfterYearAndGenreChangeMp3_ShouldChangeNothing()
    {
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateMp3);

        await AssertYearAndGenreThenRescanAsync(harness);
    }

    [Fact(DisplayName = "rescan_after_a_partial_edit_never_recreates_the_old_album_or_artist")]
    public async Task Rescan_AfterPartialEdit_ShouldNeverRecreateTheOldAlbumOrArtist()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long existingId = harness.ArtistId(AlbumTagWriteHarness.ExistingArtist);
        long failedId = harness.TrackId(AlbumTagWriteHarness.SecondFile);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        int artistCount = harness.Scalar<int>("SELECT COUNT(*) FROM Artists");
        harness.SeedAlbumData(albumId);
        harness.FailWritesTo(AlbumTagWriteHarness.SecondFile);

        // Act
        AlbumTagWriteOutcome partial = await harness.EditAsync(albumId, NewAlbumName, AlbumTagWriteHarness.ExistingArtist, NewYear, NewGenre);
        WriteSnapshot afterIncident = harness.TakeSnapshot();
        harness.Touch(AlbumTagWriteHarness.SecondFile, 2);
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 3);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot afterRescan = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, partial.State);
        Assert.Equal(AlbumTagWriteHarness.SecondFile, Path.GetFileNameWithoutExtension(Assert.Single(partial.Failures).FilePath));
        Assert.Equal(1, harness.PendingCount());
        Assert.Equal(AlbumTagWriteHarness.OldArtist, harness.ReadFile(AlbumTagWriteHarness.SecondFile).Artist);
        Assert.Equal(AlbumTagWriteHarness.AlbumName, harness.ReadFile(AlbumTagWriteHarness.SecondFile).Album);
        Assert.Equal(NewAlbumName, harness.ReadFile(AlbumTagWriteHarness.FirstFile).Album);

        // Positive control: the neighbour changed by another reader is taken over, so the folder is processed again.
        Assert.Equal(NeighbourRetitled, afterRescan.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        Assert.Equal(0, rescan.TracksImported);
        AssertNothingRecreated(rescan, artistsDeleted: 0);

        // The file still to write is read with the database values: nothing of the old album or artist comes back.
        Assert.Equal(artistCount, afterRescan.Rows("Artists").Count);
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = AlbumTagWriteHarness.AlbumName }));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = NewAlbumName }));
        Assert.Equal("0", afterRescan.Value("Albums", albumId, "isCompilation"));
        Assert.Equal(albumId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", failedId, "albumId"));
        Assert.Equal(existingId.ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", failedId, "artistId"));
        Assert.Equal(harness.GenreId(NewGenre).ToString(CultureInfo.InvariantCulture), afterRescan.Value("Tracks", failedId, "genreId"));
        Assert.Equal(1, harness.PendingCount());
        Assert.Equal(AlbumTagWriteHarness.AlbumName, harness.ReadFile(AlbumTagWriteHarness.SecondFile).Album);
        AssertUserDataKept(harness, afterRescan, albumId);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(afterIncident, afterRescan, failedId, neighbourId);
    }

    [Fact(DisplayName = "resume_after_a_partial_edit_completes_and_next_rescan_is_neutral")]
    public async Task Resume_AfterPartialEdit_ShouldCompleteAndTheNextRescanBeNeutral()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long failedId = harness.TrackId(AlbumTagWriteHarness.SecondFile);
        harness.SeedAlbumData(albumId);
        harness.FailWritesTo(AlbumTagWriteHarness.SecondFile);

        AlbumTagWriteOutcome partial = await harness.EditAsync(albumId, NewAlbumName, AlbumTagWriteHarness.ExistingArtist, NewYear, NewGenre);
        harness.Touch(AlbumTagWriteHarness.SecondFile, 2);
        await harness.ImportAsync();
        harness.StopFailingWrites();

        // Act
        AlbumTagWriteOutcome resumed = await harness.ResumeAsync(albumId);
        WriteSnapshot afterResume = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.SecondFile, "Retitled by another reader", 4);
        ImportStatisticsDto finalRescan = await harness.ImportAsync();
        WriteSnapshot final = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, partial.State);
        Assert.Equal(EArtistRenameState.Completed, resumed.State);
        Assert.Equal(0, harness.PendingCount());

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
        {
            FileTags tags = harness.ReadFile(file);

            Assert.Equal(NewAlbumName, tags.Album);
            Assert.Equal(AlbumTagWriteHarness.ExistingArtist, tags.Artist);
            Assert.Equal(NewYear, tags.Year);
        }

        Assert.Equal(NewGenre, harness.ReadFile(AlbumTagWriteHarness.SecondFile).Genre);
        Assert.Equal(AlbumTagWriteHarness.OwnGenre, harness.ReadFile(AlbumTagWriteHarness.OwnGenreFile).Genre);

        // Positive control: the file written by the resume was changed by another reader and taken over.
        Assert.Equal("Retitled by another reader", final.Value("Tracks", failedId, "title"));
        Assert.Equal(1, finalRescan.TracksUpdated);
        AssertNothingRecreated(finalRescan, artistsDeleted: 0);
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = AlbumTagWriteHarness.AlbumName }));
        Assert.Equal("0", final.Value("Albums", albumId, "isCompilation"));
        AssertUserDataKept(harness, final, albumId);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(afterResume, final, failedId);
    }

    private static async Task AssertRenameThenRescanAsync(AlbumTagWriteHarness harness, bool genreFromArtist = false)
    {
        // Arrange
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        int albumCount = harness.Scalar<int>("SELECT COUNT(*) FROM Albums");
        harness.SeedAlbumData(albumId);

        // Act
        AlbumTagWriteOutcome outcome = await harness.EditAsync(albumId, NewAlbumName, AlbumTagWriteHarness.OldArtist, null, null);
        WriteSnapshot before = harness.TakeSnapshot();
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(albumId, outcome.AlbumId);
        Assert.Equal(0, harness.PendingCount());
        Assert.Equal(NewAlbumName, before.Value("Albums", albumId, "name"));

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
        {
            FileTags tags = harness.ReadFile(file);

            Assert.Equal(NewAlbumName, tags.Album);
            Assert.Equal(AlbumTagWriteHarness.OldArtist, tags.Artist);
            Assert.Equal(albumId, harness.AlbumIdOf(file));
        }

        Assert.Equal("Neighbour album", harness.ReadFile(AlbumTagWriteHarness.NeighbourFile).Album);

        // Positive control: the folder was processed again, so the neighbour changed by another reader was taken over.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));

        // With the genre of the artist given to every track, the import also reprocesses the track that has its own genre, and changes nothing but its edit date: that is how the import behaves without any edit.
        long[] reprocessed = genreFromArtist ? [neighbourId, harness.TrackId(AlbumTagWriteHarness.OwnGenreFile)] : [neighbourId];

        Assert.Equal(reprocessed.Length, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 0, genresRecreated: genreFromArtist ? 1 : 0);

        Assert.Equal(albumCount, after.Rows("Albums").Count);
        Assert.Equal(0, harness.Scalar<int>("SELECT COUNT(*) FROM Albums WHERE name = @name COLLATE NOCASE", new { name = AlbumTagWriteHarness.AlbumName }));
        Assert.Equal(NewAlbumName, after.Value("Albums", albumId, "name"));
        Assert.Equal("0", after.Value("Albums", albumId, "isCompilation"));
        AssertUserDataKept(harness, after, albumId);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after, reprocessed);
    }

    private static async Task AssertYearAndGenreThenRescanAsync(AlbumTagWriteHarness harness)
    {
        // Arrange
        await harness.ImportTwiceAsync();
        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);
        long neighbourId = harness.TrackId(AlbumTagWriteHarness.NeighbourFile);
        long oldGenreId = harness.GenreId(AlbumTagWriteHarness.OldGenre);
        long ownGenreId = harness.GenreId(AlbumTagWriteHarness.OwnGenre);
        harness.SeedAlbumData(albumId);

        Assert.Equal(oldGenreId, harness.Scalar<long>("SELECT genreId FROM Albums WHERE id = @albumId", new { albumId }));

        // Act
        AlbumTagWriteOutcome outcome = await harness.EditAsync(albumId, AlbumTagWriteHarness.AlbumName, AlbumTagWriteHarness.OldArtist, NewYear, NewGenre);
        WriteSnapshot before = harness.TakeSnapshot();
        long newGenreId = harness.GenreId(NewGenre);
        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, NeighbourRetitled, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();
        WriteSnapshot after = harness.TakeSnapshot();

        // Assert
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(0, harness.PendingCount());
        Assert.Equal(NewYear.ToString(CultureInfo.InvariantCulture), before.Value("Albums", albumId, "year"));
        Assert.Equal(newGenreId.ToString(CultureInfo.InvariantCulture), before.Value("Albums", albumId, "genreId"));

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
        {
            Assert.Equal(NewYear, harness.ReadFile(file).Year);
        }

        // The genre changes only on the tracks that carried the old genre of the album.
        foreach (string file in new[] { AlbumTagWriteHarness.FirstFile, AlbumTagWriteHarness.SecondFile })
        {
            Assert.Equal(NewGenre, harness.ReadFile(file).Genre);
            Assert.Equal(newGenreId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(file), "genreId"));
        }

        Assert.Equal(AlbumTagWriteHarness.OwnGenre, harness.ReadFile(AlbumTagWriteHarness.OwnGenreFile).Genre);
        Assert.Equal(ownGenreId.ToString(CultureInfo.InvariantCulture), after.Value("Tracks", harness.TrackId(AlbumTagWriteHarness.OwnGenreFile), "genreId"));
        Assert.Equal(AlbumTagWriteHarness.OldGenre, harness.ReadFile(AlbumTagWriteHarness.NeighbourFile).Genre);
        Assert.Equal(2, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @albumId AND genreId = @newGenreId", new { albumId, newGenreId }));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @albumId AND genreId = @ownGenreId", new { albumId, ownGenreId }));

        // Positive control: the folder was processed again.
        Assert.Equal(NeighbourRetitled, after.Value("Tracks", neighbourId, "title"));
        Assert.Equal(1, rescan.TracksUpdated);
        AssertNothingRecreated(rescan, artistsDeleted: 0);
        Assert.Equal(NewYear.ToString(CultureInfo.InvariantCulture), after.Value("Albums", albumId, "year"));
        Assert.Equal(newGenreId.ToString(CultureInfo.InvariantCulture), after.Value("Albums", albumId, "genreId"));
        AssertUserDataKept(harness, after, albumId);
        harness.AssertCountersMatchTracks();
        AlbumTagWriteHarness.AssertSameExceptTouched(before, after, neighbourId);
    }

    private static void AssertNothingRecreated(ImportStatisticsDto statistics, int artistsDeleted, int genresRecreated = 0)
    {
        Assert.Equal(0, statistics.TracksImported);
        Assert.Equal(0, statistics.AlbumsImported);
        Assert.Equal(0, statistics.ArtistsImported);
        Assert.Equal(genresRecreated, statistics.GenresImported);
        Assert.Equal(0, statistics.TracksDeleted);
        Assert.Equal(0, statistics.AlbumsDeleted);
        Assert.Equal(artistsDeleted, statistics.ArtistsDeleted);
        Assert.Equal(genresRecreated, statistics.GenresDeleted);
    }

    private static void AssertUserDataKept(AlbumTagWriteHarness harness, WriteSnapshot snapshot, long albumId)
    {
        Assert.Equal("1", snapshot.Value("Albums", albumId, "isFavorite"));
        Assert.Equal("7", snapshot.Value("Albums", albumId, "listenCount"));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @albumId", new { albumId }));
        Assert.Equal(1, harness.Scalar<int>("SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = 'Album' AND ScopeId = @albumId", new { albumId }));
        Assert.Equal(3, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @albumId", new { albumId }));
        Assert.Equal(3, harness.Scalar<int>("SELECT COUNT(*) FROM ListeningEvents"));
    }
}