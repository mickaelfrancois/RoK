using System.Xml.Linq;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class AlbumMergeTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static readonly string[] ScreenKeys =
    [
        "MergeAlbumDialog.Title",
        "albumMenuMerge.Text"
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    private static Dictionary<string, string> Values(string folder, string language)
    {
        return XDocument.Load(Path.Combine(folder, language, "Resources.resw")).Root!.Elements("data")
            .ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")!.Value);
    }

    [Theory(DisplayName = "every_known_album_merge_error_code_maps_to_a_specific_key")]
    [InlineData("album.merge.disabled")]
    [InlineData("album.merge.same_album")]
    [InlineData("album.merge.compilation")]
    [InlineData("album.merge.no_artist")]
    [InlineData("album.merge.pending")]
    [InlineData("album.merge.artist_pending_rename")]
    [InlineData("album.merge.conflicts")]
    [InlineData("album.not_found")]
    public void KnownCode_MapsToASpecificKey(string code)
    {
        // Act
        string key = AlbumMergeTextKeys.Error(code);

        // Assert
        Assert.NotEqual(AlbumMergeTextKeys.GenericError, key);
    }

    [Theory(DisplayName = "unknown_or_missing_album_merge_code_maps_to_the_generic_key")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something.else")]
    public void UnknownCode_MapsToTheGenericKey(string? code)
    {
        // Act
        string key = AlbumMergeTextKeys.Error(code);

        // Assert
        Assert.Equal(AlbumMergeTextKeys.GenericError, key);
    }

    [Fact(DisplayName = "album_merge_text_keys_exist_in_every_language")]
    public void EveryKey_ExistsInEveryResourceFile()
    {
        // Arrange
        string folder = StringsFolder();
        string[] expected = [.. AlbumMergeTextKeys.AllKeys, .. ScreenKeys];

        foreach (string language in Languages)
        {
            XElement[] data = [.. XDocument.Load(Path.Combine(folder, language, "Resources.resw")).Root!.Elements("data")];
            HashSet<string?> names = [.. data.Select(item => item.Attribute("name")?.Value)];

            // Act
            string[] missing = [.. expected.Where(key => !names.Contains(key))];
            string[] empty = [.. data.Where(item => expected.Contains(item.Attribute("name")?.Value) && string.IsNullOrWhiteSpace(item.Element("value")?.Value)).Select(item => item.Attribute("name")!.Value)];

            // Assert
            Assert.True(missing.Length == 0, $"{language} misses: {string.Join(", ", missing)}");
            Assert.True(empty.Length == 0, $"{language} has empty values: {string.Join(", ", empty)}");
        }
    }

    [Fact(DisplayName = "the_album_merge_formatted_texts_keep_their_placeholders_in_every_language")]
    public void FormattedTexts_KeepTheirPlaceholders()
    {
        // Arrange
        string folder = StringsFolder();
        (string Key, int Count)[] formatted =
        [
            (AlbumMergeTextKeys.Collision, 3),
            (AlbumMergeTextKeys.Summary, 4),
            (AlbumMergeTextKeys.ArtistChange, 2),
            (AlbumMergeTextKeys.ConflictLine, 3),
            (AlbumMergeTextKeys.DiscConflictLine, 4),
            (AlbumMergeTextKeys.ShiftDiscs, 1)
        ];

        foreach (string language in Languages)
        {
            Dictionary<string, string> values = Values(folder, language);

            foreach ((string key, int count) in formatted)
            {
                // Act
                string text = values[key];

                // Assert
                for (int index = 0; index < count; index++)
                    Assert.True(text.Contains($"{{{index}}}"), $"{language} {key} misses {{{index}}}");
            }
        }
    }

    [Fact(DisplayName = "the_album_merge_reuses_the_edit_texts_for_the_counts_and_the_orphan_artist")]
    public void ReusedKeys_ExistInEveryLanguage()
    {
        // Arrange
        string folder = StringsFolder();
        string[] reused = [AlbumEditTextKeys.Counts, AlbumEditTextKeys.OrphanArtist, AlbumEditTextKeys.OrphanFavoriteArtist];

        foreach (string language in Languages)
        {
            Dictionary<string, string> values = Values(folder, language);

            // Act
            string[] missing = [.. reused.Where(key => !values.ContainsKey(key))];

            // Assert
            Assert.True(missing.Length == 0, $"{language} misses: {string.Join(", ", missing)}");
        }
    }

    [Fact(DisplayName = "the_album_merge_preview_lines_name_the_notes_conflicts_and_discs")]
    public void Texts_BuildThePreviewLines()
    {
        // Arrange
        AlbumMergePreview preview = new(
            7,
            "Source",
            9,
            "Target",
            "Artist A",
            "Artist B",
            true,
            3,
            10,
            3,
            [new AlbumMergeFieldConflict("Wikipedia", new string('a', 100), "short\nlink")],
            [new DiscConflict(1, 2, "Song S", "Song T")],
            1,
            true,
            new OrphanArtistWarning(4, "Artist A", true, 12),
            []);
        Func<string, string> localize = key => key switch
        {
            AlbumMergeTextKeys.Summary => "{0}/{1} into {2}/{3}",
            AlbumMergeTextKeys.ArtistChange => "{0} moves to {1}",
            AlbumMergeTextKeys.SeparateFolders => "folders",
            AlbumMergeTextKeys.ConflictLine => "{0}: {1} / {2}",
            AlbumMergeTextKeys.DiscConflictLine => "d{0} t{1} {2} {3}",
            AlbumMergeTextKeys.ShiftDiscs => "+{0}",
            AlbumEditTextKeys.Counts => "{0} files",
            AlbumEditTextKeys.OrphanFavoriteArtist => "{0} favorite {1}",
            _ => key
        };

        // Act
        string summary = AlbumMergeTexts.Summary(preview, localize);
        string counts = AlbumMergeTexts.Counts(preview, localize);
        IReadOnlyList<string> notes = AlbumMergeTexts.NoteLines(preview, localize);
        IReadOnlyList<string> conflicts = AlbumMergeTexts.ConflictLines(preview, localize);
        IReadOnlyList<string> discs = AlbumMergeTexts.DiscConflictLines(preview, localize);
        string shift = AlbumMergeTexts.ShiftDiscs(preview, localize);

        // Assert
        Assert.Equal("Source/Artist A into Target/Artist B", summary);
        Assert.Equal("3 files", counts);
        Assert.Equal(["Artist A moves to Artist B", "folders", "Artist A favorite 12"], notes);
        Assert.Equal([$"Wikipedia: {new string('a', 60)}… / short link"], conflicts);
        Assert.Equal(["d1 t2 Song S Song T"], discs);
        Assert.Equal("+1", shift);
    }

    [Fact(DisplayName = "the_album_merge_collision_intro_gives_the_name_the_artist_and_the_count")]
    public void CollisionIntro_GivesTheNameTheArtistAndTheCount()
    {
        // Act
        string intro = AlbumMergeTexts.CollisionIntro(new AlbumCollision(9, "Taken", "Someone", 14), _ => "{0}|{1}|{2}");

        // Assert
        Assert.Equal("Taken|Someone|14", intro);
    }

    [Fact(DisplayName = "an_empty_album_merge_text_falls_back_to_the_generic_message")]
    public void ErrorMessage_EmptyText_FallsBackToTheGenericMessage()
    {
        // Act
        string message = AlbumMergeTexts.ErrorMessage("album.merge.same_album", key => key == AlbumMergeTextKeys.GenericError ? "generic" : string.Empty);

        // Assert
        Assert.Equal("generic", message);
    }

    [Fact(DisplayName = "an_album_choice_shows_the_name_then_the_artist")]
    public void AlbumMergeChoice_Label_ShowsTheNameThenTheArtist()
    {
        // Act
        string with = new AlbumMergeChoice(1, "Abbey Road", "The Beatles").Label;
        string without = new AlbumMergeChoice(2, "Untitled", string.Empty).ToString();

        // Assert
        Assert.Equal("Abbey Road — The Beatles", with);
        Assert.Equal("Untitled", without);
    }
}