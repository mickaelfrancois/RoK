using System.Xml.Linq;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class ArtistMergeTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static readonly string[] ScreenKeys =
    [
        "MergeArtistDialog.Title",
        "artistMenuMerge.Text"
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    [Theory(DisplayName = "every_known_merge_error_code_maps_to_a_specific_key")]
    [InlineData("artist.merge.disabled")]
    [InlineData("artist.merge.same_artist")]
    [InlineData("artist.merge.pending_rename")]
    [InlineData("artist.not_found")]
    public void KnownCode_MapsToASpecificKey(string code)
    {
        // Act
        string key = ArtistMergeTextKeys.Error(code);

        // Assert
        Assert.NotEqual(ArtistMergeTextKeys.GenericError, key);
    }

    [Theory(DisplayName = "unknown_or_missing_merge_code_maps_to_the_generic_key")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something.else")]
    public void UnknownCode_MapsToTheGenericKey(string? code)
    {
        // Act
        string key = ArtistMergeTextKeys.Error(code);

        // Assert
        Assert.Equal(ArtistMergeTextKeys.GenericError, key);
    }

    [Fact(DisplayName = "merge_text_keys_exist_in_every_language")]
    public void EveryKey_ExistsInEveryResourceFile()
    {
        // Arrange
        string folder = StringsFolder();
        string[] expected = [.. ArtistMergeTextKeys.AllKeys, .. ScreenKeys];

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

    [Fact(DisplayName = "the_merge_formatted_texts_keep_their_placeholders_in_every_language")]
    public void FormattedTexts_KeepTheirPlaceholders()
    {
        // Arrange
        string folder = StringsFolder();
        (string Key, int Count)[] formatted =
        [
            (ArtistMergeTextKeys.Collision, 3),
            (ArtistMergeTextKeys.Summary, 2),
            (ArtistMergeTextKeys.Counts, 2),
            (ArtistMergeTextKeys.AlbumLine, 2),
            (ArtistMergeTextKeys.ConflictLine, 3),
            (ArtistMergeTextKeys.PictureTarget, 1),
            (ArtistMergeTextKeys.PictureSource, 1)
        ];

        foreach (string language in Languages)
        {
            Dictionary<string, string> values = XDocument.Load(Path.Combine(folder, language, "Resources.resw")).Root!.Elements("data")
                .ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")!.Value);

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

    [Fact(DisplayName = "the_merge_blockers_reuse_the_rename_blocker_keys")]
    public void Blockers_ReuseTheRenameKeys()
    {
        // Act
        string[] keys = [.. Enum.GetValues<EArtistRenameBlock>().Select(ArtistRenameTextKeys.Blocker)];

        // Assert
        Assert.All(keys, key => Assert.Contains(key, ArtistRenameTextKeys.AllKeys));
    }

    [Fact(DisplayName = "the_preview_lines_name_the_albums_conflicts_playlists_and_picture")]
    public void Texts_BuildThePreviewLines()
    {
        // Arrange
        ArtistMergePreview preview = new(
            7,
            "Source",
            9,
            "Target",
            12,
            3,
            [new AlbumMergeGroupPreview("Back in Black", 2)],
            [new ArtistMergeFieldConflict("Biography", new string('a', 100), "short\nbio")],
            [],
            [new SmartPlaylistMergeImpact(1, "Rock", true), new SmartPlaylistMergeImpact(2, "Mix", false)],
            EArtistMergePicture.Source);
        Func<string, string> localize = key => key switch
        {
            ArtistMergeTextKeys.Summary => "{0} into {1}",
            ArtistMergeTextKeys.Counts => "{0} files, {1} albums",
            ArtistMergeTextKeys.AlbumLine => "{0} x{1}",
            ArtistMergeTextKeys.ConflictLine => "{0}: {1} / {2}",
            ArtistMergeTextKeys.PlaylistRewritten => "rewritten",
            ArtistMergeTextKeys.PictureSource => "picture of {0}",
            _ => key
        };

        // Act
        string summary = ArtistMergeTexts.Summary(preview, localize);
        string counts = ArtistMergeTexts.Counts(preview, localize);
        IReadOnlyList<string> albums = ArtistMergeTexts.AlbumLines(preview, localize);
        IReadOnlyList<string> conflicts = ArtistMergeTexts.ConflictLines(preview, localize);
        IReadOnlyList<string> playlists = ArtistMergeTexts.PlaylistLines(preview, localize);
        string? picture = ArtistMergeTexts.PictureLine(preview, localize);

        // Assert
        Assert.Equal("Source into Target", summary);
        Assert.Equal("12 files, 3 albums", counts);
        Assert.Equal(["Back in Black x2"], albums);
        Assert.Equal([$"Biography: {new string('a', 60)}… / short bio"], conflicts);
        Assert.Equal(["Rock (rewritten)", "Mix"], playlists);
        Assert.Equal("picture of Source", picture);
    }

    [Fact(DisplayName = "no_picture_line_is_given_when_neither_artist_has_a_picture")]
    public void PictureLine_NoPicture_ReturnsNull()
    {
        // Arrange
        ArtistMergePreview preview = new(7, "Source", 9, "Target", 1, 1, [], [], [], [], EArtistMergePicture.None);

        // Act
        string? picture = ArtistMergeTexts.PictureLine(preview, key => key);

        // Assert
        Assert.Null(picture);
    }

    [Fact(DisplayName = "an_unknown_merge_code_or_an_empty_text_falls_back_to_the_generic_message")]
    public void ErrorMessage_EmptyText_FallsBackToTheGenericMessage()
    {
        // Act
        string message = ArtistMergeTexts.ErrorMessage("artist.merge.same_artist", key => key == ArtistMergeTextKeys.GenericError ? "generic" : string.Empty);

        // Assert
        Assert.Equal("generic", message);
    }

    [Fact(DisplayName = "the_collision_intro_gives_the_name_and_the_counts")]
    public void CollisionIntro_GivesTheNameAndTheCounts()
    {
        // Act
        string intro = ArtistMergeTexts.CollisionIntro(new ArtistRenameCollision("Taken", 2, 14), _ => "{0}|{1}|{2}");

        // Assert
        Assert.Equal("Taken|2|14", intro);
    }
}