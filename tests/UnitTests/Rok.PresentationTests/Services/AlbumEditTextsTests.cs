using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class AlbumEditTextsTests
{
    private static string Localize(string key) => key;

    private static AlbumEditPreview Preview(
        string newName = "Abbey Road",
        string newArtist = "The Beatles",
        int? newYear = 1969,
        string newGenre = "Rock",
        bool createsArtist = false,
        int genreKept = 0,
        OrphanArtistWarning? orphan = null,
        IReadOnlyList<ArtistRenameBlocker>? blockers = null) => new(
            7, "Abbey Road", newName, "The Beatles", newArtist, createsArtist, 1969, newYear, "Rock", newGenre, false, 12, 0, genreKept, 0, null, orphan, blockers ?? []);

    [Fact(DisplayName = "only_the_fields_that_change_get_a_line")]
    public void ChangeLines_OnlyChangedFields()
    {
        // Arrange
        AlbumEditPreview preview = Preview(newName: "Abbey Road (Remaster)", newYear: 1970);

        // Act
        IReadOnlyList<string> lines = AlbumEditTexts.ChangeLines(preview, Localize);

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, line => line.StartsWith(AlbumEditTextKeys.ChangeName, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(AlbumEditTextKeys.ChangeYear, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "the_notes_count_the_files_and_warn_about_the_orphan_artist_and_the_kept_genres")]
    public void NoteLines_CountsAndWarnings()
    {
        // Arrange
        AlbumEditPreview preview = Preview(createsArtist: true, genreKept: 2, orphan: new OrphanArtistWarning(3, "Old", true, 40));

        // Act
        IReadOnlyList<string> lines = AlbumEditTexts.NoteLines(preview, Localize);

        // Assert
        Assert.Contains(lines, line => line.StartsWith(AlbumEditTextKeys.Counts, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(AlbumEditTextKeys.ArtistCreated, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(AlbumEditTextKeys.GenreKept, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(AlbumEditTextKeys.OrphanFavoriteArtist, StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line == AlbumEditTextKeys.OrphanArtist);
    }

    [Fact(DisplayName = "a_preview_with_a_blocker_cannot_start")]
    public void Prompt_WithBlocker_CannotStart()
    {
        // Arrange
        AlbumEditPreview preview = Preview(blockers: [new ArtistRenameBlocker("a.mp3", EArtistRenameBlock.Locked, null)]);

        // Act
        AlbumTagWritePrompt prompt = AlbumEditTexts.Prompt(preview, Localize);

        // Assert
        Assert.False(prompt.CanStart);
        Assert.False(prompt.IsResume);
        Assert.Single(prompt.Blockers);
    }

    [Fact(DisplayName = "a_preview_without_blocker_can_start")]
    public void Prompt_WithoutBlocker_CanStart()
    {
        // Act
        AlbumTagWritePrompt prompt = AlbumEditTexts.Prompt(Preview(), Localize);

        // Assert
        Assert.True(prompt.CanStart);
    }

    [Fact(DisplayName = "the_resume_prompt_has_no_change_line_and_can_start")]
    public void ResumePrompt_HasOnlyTheIntro()
    {
        // Act
        AlbumTagWritePrompt prompt = AlbumEditTexts.ResumePrompt(Localize);

        // Assert
        Assert.True(prompt.IsResume);
        Assert.True(prompt.CanStart);
        Assert.Empty(prompt.Changes);
        Assert.Equal(AlbumEditTextKeys.ResumeIntro, Assert.Single(prompt.Notes));
    }

    [Fact(DisplayName = "an_unknown_error_code_gives_the_generic_message")]
    public void ErrorMessage_UnknownCode_IsGeneric()
    {
        // Act
        string message = AlbumEditTexts.ErrorMessage("nope", Localize);

        // Assert
        Assert.Equal(AlbumEditTextKeys.GenericError, message);
    }
}