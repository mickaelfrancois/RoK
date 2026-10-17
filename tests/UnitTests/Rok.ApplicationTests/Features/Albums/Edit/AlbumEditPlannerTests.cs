using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Features.Albums.Edit;

public class AlbumEditPlannerTests
{
    private const long ArtistId = 10;
    private const long GenreId = 20;

    private static AlbumEditSubject Subject(int? year = 1999)
    {
        var album = new AlbumEntity { Id = 1, Name = "Old", ArtistId = ArtistId, GenreId = GenreId, Year = year };

        return new AlbumEditSubject(album, "Old Artist", "Rock");
    }

    private static TrackEntity Track(long id, long? artistId = ArtistId, long? genreId = GenreId)
    {
        return new TrackEntity { Id = id, AlbumId = 1, ArtistId = artistId, GenreId = genreId, MusicFile = $"C:\\m\\{id}.flac" };
    }

    private static TagFieldValues File(string album = "Old", string artist = "Old Artist", int? year = 1999, string genre = "Rock")
    {
        return new TagFieldValues("t", 1, null, "", null) { Album = album, Artist = artist, Year = year, Genre = genre };
    }

    private static Dictionary<long, TagFieldValues> Files(params long[] ids)
    {
        return ids.ToDictionary(id => id, _ => File());
    }

    [Fact(DisplayName = "planner_writes_the_new_name_on_files_that_do_not_carry_it")]
    public void Plan_NameChanged_WritesAlbumOnFilesWithoutIt()
    {
        // Arrange
        TrackEntity[] tracks = [Track(1), Track(2)];
        Dictionary<long, TagFieldValues> files = Files(1, 2);
        files[2] = File(album: "new name");

        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), tracks, files, " New Name ", "Old Artist", null, null);

        // Assert
        Assert.True(plan.NameChanged);
        PlannedTrackFields write = Assert.Single(plan.Writes);
        Assert.Equal(1, write.Track.Id);
        Assert.Equal(ETagWriteFields.Album, write.Fields);
        Assert.Equal("New Name", write.Changes.Album);
    }

    [Fact(DisplayName = "planner_writes_year_only_where_it_differs")]
    public void Plan_YearChanged_WritesYearOnDifferingFiles()
    {
        // Arrange
        TrackEntity[] tracks = [Track(1), Track(2)];
        Dictionary<long, TagFieldValues> files = Files(1, 2);
        files[2] = File(year: 2001);

        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), tracks, files, "Old", "Old Artist", 2001, null);

        // Assert
        Assert.True(plan.YearChanged);
        PlannedTrackFields write = Assert.Single(plan.Writes);
        Assert.Equal(1, write.Track.Id);
        Assert.Equal(ETagWriteFields.Year, write.Fields);
        Assert.Equal(2001, write.Changes.Year);
    }

    [Fact(DisplayName = "planner_keeps_the_year_when_none_is_given")]
    public void Plan_NullYear_DoesNotWriteYear()
    {
        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), [Track(1)], Files(1), "Old", "Old Artist", null, null);

        // Assert
        Assert.False(plan.YearChanged);
        Assert.Empty(plan.Writes);
    }

    [Fact(DisplayName = "planner_writes_artist_only_on_tracks_of_the_album_artist")]
    public void Plan_ArtistChanged_SkipsTracksOfOtherArtists()
    {
        // Arrange
        TrackEntity[] tracks = [Track(1), Track(2, artistId: 99), Track(3)];

        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), tracks, Files(1, 2, 3), "Old", "New Artist", null, null);

        // Assert
        Assert.True(plan.ArtistChanged);
        Assert.Equal([1L, 3L], plan.Writes.Select(w => w.Track.Id));
        Assert.All(plan.Writes, w => Assert.Equal(ETagWriteFields.Artist, w.Fields));
        Assert.All(plan.Writes, w => Assert.Equal("New Artist", w.Changes.Artist));
        Assert.Equal(1, plan.OtherArtistTrackCount);
    }

    [Fact(DisplayName = "planner_writes_genre_only_on_tracks_of_the_old_album_genre")]
    public void Plan_GenreChanged_SkipsTracksWithTheirOwnGenre()
    {
        // Arrange
        TrackEntity[] tracks = [Track(1), Track(2, genreId: 77), Track(3), Track(4, genreId: null)];

        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), tracks, Files(1, 2, 3, 4), "Old", "Old Artist", null, "Jazz");

        // Assert
        Assert.True(plan.GenreChanged);
        Assert.Equal([1L, 3L], plan.Writes.Select(w => w.Track.Id));
        Assert.All(plan.Writes, w => Assert.Equal(ETagWriteFields.Genre, w.Fields));
        Assert.All(plan.Writes, w => Assert.Equal("Jazz", w.Changes.Genre));
        Assert.Equal(2, plan.GenreTrackCount);
        Assert.Equal(2, plan.GenreKeptCount);
    }

    [Fact(DisplayName = "planner_combines_fields_in_one_write_per_track")]
    public void Plan_SeveralFields_OneWritePerTrack()
    {
        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), [Track(1)], Files(1), "New", "New Artist", 2005, "Jazz");

        // Assert
        PlannedTrackFields write = Assert.Single(plan.Writes);
        Assert.Equal(ETagWriteFields.Album | ETagWriteFields.Artist | ETagWriteFields.Year | ETagWriteFields.Genre, write.Fields);
        Assert.Equal("New", write.Changes.Album);
        Assert.Equal("New Artist", write.Changes.Artist);
        Assert.Equal(2005, write.Changes.Year);
        Assert.Equal("Jazz", write.Changes.Genre);
    }

    [Fact(DisplayName = "planner_writes_a_track_whose_file_values_are_unknown")]
    public void Plan_MissingFileValues_Writes()
    {
        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), [Track(1)], new Dictionary<long, TagFieldValues>(), "New", "Old Artist", null, null);

        // Assert
        Assert.Single(plan.Writes);
    }

    [Fact(DisplayName = "planner_reports_no_change_when_every_field_is_the_same")]
    public void Plan_NothingChanged_HasNoChanges()
    {
        // Act
        AlbumEditPlan plan = AlbumEditPlanner.Plan(Subject(), [Track(1)], Files(1), "old", "OLD ARTIST", 1999, "rock");

        // Assert
        Assert.False(plan.HasChanges);
        Assert.Empty(plan.Writes);
    }

    [Fact(DisplayName = "planner_flags_out_of_sync_files")]
    public void FindOutOfSync_FileWithNeitherOldNorNewName_Blocks()
    {
        // Arrange
        TrackEntity track = Track(1);

        // Act
        ArtistRenameBlocker? blocker = AlbumEditPlanner.FindOutOfSync(Subject(), track, File(album: "Something else"), "New", "Old Artist");

        // Assert
        Assert.NotNull(blocker);
        Assert.Equal(EArtistRenameBlock.OutOfSync, blocker.Reason);
        Assert.Equal(track.MusicFile, blocker.FilePath);
        Assert.Equal("Something else", blocker.Detail);
    }

    [Theory(DisplayName = "planner_accepts_files_carrying_the_old_or_the_new_name")]
    [InlineData("Old")]
    [InlineData("new")]
    public void FindOutOfSync_FileWithOldOrNewName_ReturnsNull(string fileAlbum)
    {
        // Act
        ArtistRenameBlocker? blocker = AlbumEditPlanner.FindOutOfSync(Subject(), Track(1), File(album: fileAlbum), "New", "Old Artist");

        // Assert
        Assert.Null(blocker);
    }

    [Fact(DisplayName = "planner_flags_a_file_with_neither_old_nor_new_artist")]
    public void FindOutOfSync_FileWithUnknownArtist_Blocks()
    {
        // Act
        ArtistRenameBlocker? blocker = AlbumEditPlanner.FindOutOfSync(Subject(), Track(1), File(artist: "Stranger"), "Old", "New Artist");

        // Assert
        Assert.NotNull(blocker);
        Assert.Equal(EArtistRenameBlock.OutOfSync, blocker.Reason);
        Assert.Equal("Stranger", blocker.Detail);
    }

    [Fact(DisplayName = "planner_ignores_the_artist_of_tracks_of_another_artist")]
    public void FindOutOfSync_TrackOfOtherArtist_ReturnsNull()
    {
        // Act
        ArtistRenameBlocker? blocker = AlbumEditPlanner.FindOutOfSync(Subject(), Track(1, artistId: 99), File(artist: "Stranger"), "Old", "New Artist");

        // Assert
        Assert.Null(blocker);
    }

    [Fact(DisplayName = "planner_never_blocks_on_genre_or_year_or_unreadable_values")]
    public void FindOutOfSync_GenreYearOrUnknownValues_ReturnsNull()
    {
        // Act
        ArtistRenameBlocker? differing = AlbumEditPlanner.FindOutOfSync(Subject(), Track(1), File(year: 1, genre: "Whatever"), "Old", "Old Artist");
        ArtistRenameBlocker? unreadable = AlbumEditPlanner.FindOutOfSync(Subject(), Track(1), null, "New", "New Artist");

        // Assert
        Assert.Null(differing);
        Assert.Null(unreadable);
    }
}