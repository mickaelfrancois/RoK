using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Domain.Enums;
using Rok.Import;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public sealed class PendingTagWriteOverlayTests
{
    private const string Path = @"C:\music\song.mp3";

    [Fact(DisplayName = "overlay_replaces_album_artist_genre_and_disc_with_database_values")]
    public async Task Apply_AllFieldsPending_ShouldReplaceThemWithDatabaseValues()
    {
        // Arrange
        TrackImport tracks = await LoadAsync(
            new PendingTagWrite(1, ETagWriteFields.Album | ETagWriteFields.Artist | ETagWriteFields.Genre | ETagWriteFields.Disc));
        TrackFile file = FileWithOldValues();

        // Act
        PendingTagWriteOverlay.Apply([file], tracks);

        // Assert
        Assert.Equal("New Album", file.Album);
        Assert.Equal("New Artist", file.Artist);
        Assert.Equal("New Genre", file.Genre);
        Assert.Equal(3, file.Disc);
        Assert.Equal("Title", file.Title);
    }

    [Fact(DisplayName = "overlay_leaves_fields_that_are_not_pending_untouched")]
    public async Task Apply_OnlyAlbumPending_ShouldKeepTheOtherFileValues()
    {
        // Arrange
        TrackImport tracks = await LoadAsync(new PendingTagWrite(1, ETagWriteFields.Album));
        TrackFile file = FileWithOldValues();

        // Act
        PendingTagWriteOverlay.Apply([file], tracks);

        // Assert
        Assert.Equal("New Album", file.Album);
        Assert.Equal("Old Artist", file.Artist);
        Assert.Equal("Old Genre", file.Genre);
        Assert.Equal(1, file.Disc);
    }

    [Fact(DisplayName = "overlay_ignores_the_year_field")]
    public async Task Apply_OnlyYearPending_ShouldLeaveTheFileUntouched()
    {
        // Arrange
        TrackImport tracks = await LoadAsync(new PendingTagWrite(1, ETagWriteFields.Year));
        TrackFile file = FileWithOldValues();

        // Act
        PendingTagWriteOverlay.Apply([file], tracks);

        // Assert
        Assert.Equal("Old Album", file.Album);
        Assert.Equal("Old Artist", file.Artist);
        Assert.Equal("Old Genre", file.Genre);
        Assert.Equal(1, file.Disc);
    }

    [Fact(DisplayName = "overlay_ignores_files_without_pending_row")]
    public async Task Apply_NoPendingRow_ShouldLeaveTheFileUntouched()
    {
        // Arrange
        TrackImport tracks = await LoadAsync();
        TrackFile file = FileWithOldValues();

        // Act
        PendingTagWriteOverlay.Apply([file], tracks);

        // Assert
        Assert.Equal("Old Album", file.Album);
        Assert.Equal("Old Artist", file.Artist);
        Assert.Equal("Old Genre", file.Genre);
        Assert.Equal(1, file.Disc);
    }

    [Fact(DisplayName = "overlay_ignores_files_of_unknown_tracks")]
    public async Task Apply_UnknownFile_ShouldLeaveTheFileUntouched()
    {
        // Arrange
        TrackImport tracks = await LoadAsync(new PendingTagWrite(1, ETagWriteFields.Album));
        TrackFile file = FileWithOldValues();
        file.FullPath = @"C:\music\other.mp3";

        // Act
        PendingTagWriteOverlay.Apply([file], tracks);

        // Assert
        Assert.Equal("Old Album", file.Album);
    }

    private static TrackFile FileWithOldValues()
    {
        return new TrackFile
        {
            FullPath = Path,
            Title = "Title",
            Album = "Old Album",
            Artist = "Old Artist",
            Genre = "Old Genre",
            Disc = 1,
        };
    }

    private static async Task<TrackImport> LoadAsync(params PendingTagWrite[] pending)
    {
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(
        [
            new TrackEntity
            {
                Id = 1,
                MusicFile = Path,
                AlbumName = "New Album",
                ArtistName = "New Artist",
                GenreName = "New Genre",
                Disc = 3,
            }
        ]);
        repository.Setup(r => r.GetPendingTagWritesAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(pending);

        TrackImport import = new(repository.Object);
        await import.LoadCacheAsync();

        return import;
    }
}