using Rok.Application.Features.Artists.Rename;
using Rok.Application.Tag;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class ArtistRenameTextsTests
{
    private static string Localize(string key) => key switch
    {
        ArtistRenameTextKeys.Counts => "{0} files, {1} albums",
        ArtistRenameTextKeys.Collision => "{0} exists ({1} albums, {2} tracks)",
        "artistRenameBlock_Locked" => "In use",
        "artistRenameBlock_OutOfSync" => "Changed outside Rok",
        "trackEditError_ReadOnly" => "Read-only",
        "artistRenameError_Collision" => "Taken",
        ArtistRenameTextKeys.GenericError => "Generic",
        _ => string.Empty
    };

    [Fact(DisplayName = "the_summary_shows_the_old_name_then_the_new_name")]
    public void Summary_ShowsOldThenNew()
    {
        // Arrange
        ArtistRenamePreview preview = new(1, "Old", "New", 3, 1, [], null, false);

        // Act
        string summary = ArtistRenameTexts.Summary(preview);

        // Assert
        Assert.Equal("Old → New", summary);
    }

    [Fact(DisplayName = "the_counts_line_gives_the_files_then_the_albums")]
    public void Counts_GivesFilesThenAlbums()
    {
        // Arrange
        ArtistRenamePreview preview = new(1, "Old", "New", 12, 3, [], null, false);

        // Act
        string counts = ArtistRenameTexts.Counts(preview, Localize);

        // Assert
        Assert.Equal("12 files, 3 albums", counts);
    }

    [Fact(DisplayName = "the_collision_message_gives_the_name_the_albums_and_the_tracks")]
    public void CollisionMessage_GivesNameAlbumsAndTracks()
    {
        // Act
        string message = ArtistRenameTexts.CollisionMessage(new ArtistRenameCollision("Taken", 2, 14), Localize);

        // Assert
        Assert.Equal("Taken exists (2 albums, 14 tracks)", message);
    }

    [Fact(DisplayName = "a_blocker_line_gives_the_file_then_the_reason")]
    public void BlockerLines_GiveFileThenReason()
    {
        // Arrange
        ArtistRenameBlocker[] blockers =
        [
            new("C:\\Music\\a.mp3", EArtistRenameBlock.Locked, null),
            new(string.Empty, EArtistRenameBlock.Locked, null),
            new("C:\\Music\\b.mp3", EArtistRenameBlock.OutOfSync, "Other")
        ];

        // Act
        IReadOnlyList<string> lines = ArtistRenameTexts.BlockerLines(blockers, Localize);

        // Assert
        Assert.Equal(["C:\\Music\\a.mp3 — In use", "In use", "C:\\Music\\b.mp3 — Changed outside Rok (Other)"], lines);
    }

    [Fact(DisplayName = "a_failure_line_gives_the_file_then_the_reason_of_the_write")]
    public void FailureLines_GiveFileThenReason()
    {
        // Arrange
        ArtistRenameFailure[] failures = [new("C:\\Music\\a.mp3", ETagWriteReason.ReadOnly)];

        // Act
        IReadOnlyList<string> lines = ArtistRenameTexts.FailureLines(failures, Localize);

        // Assert
        Assert.Equal(["C:\\Music\\a.mp3 — Read-only"], lines);
    }

    [Theory(DisplayName = "an_error_code_maps_to_its_message_or_to_the_generic_one")]
    [InlineData("artist.rename.collision", "Taken")]
    [InlineData("artist.rename.pending", "Generic")]
    [InlineData("something.else", "Generic")]
    [InlineData(null, "Generic")]
    public void ErrorMessage_MapsTheCode(string? code, string expected)
    {
        // Act
        string message = ArtistRenameTexts.ErrorMessage(code, Localize);

        // Assert
        Assert.Equal(expected, message);
    }
}