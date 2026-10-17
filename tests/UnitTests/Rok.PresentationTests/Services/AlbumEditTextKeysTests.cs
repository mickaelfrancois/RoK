using System.Xml.Linq;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tracks.Services;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class AlbumEditTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static readonly string[] ScreenKeys =
    [
        "albumNameInput.Header",
        "albumArtistInput.Header",
        "albumYearInput.Header",
        "albumGenreInput.Header",
        "albumGenreLockedNote.Text",
        "AlbumTagWriteDialog.Title",
        "AlbumTagWriteDialog.CloseButtonText",
        "albumTagWritePendingBar.Title",
        "albumTagWritePendingBar.Message",
        "albumTagWriteResumeButton.Content"
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

    [Theory(DisplayName = "every_known_error_code_maps_to_a_specific_key")]
    [InlineData("album.edit.disabled")]
    [InlineData("album.edit.no_artist")]
    [InlineData("album.edit.pending")]
    [InlineData("album.edit.compilation_artist")]
    [InlineData("album.edit.artist_pending_rename")]
    [InlineData("album.edit.unchanged")]
    [InlineData("album.edit.genre_unused")]
    [InlineData("album.edit.genre_from_artist")]
    [InlineData("album.rename.collision")]
    [InlineData("album.not_found")]
    public void KnownCode_MapsToASpecificKey(string code)
    {
        // Act
        string key = AlbumEditTextKeys.Error(code);

        // Assert
        Assert.NotEqual(AlbumEditTextKeys.GenericError, key);
        Assert.Contains(key, AlbumEditTextKeys.AllKeys);
    }

    [Theory(DisplayName = "unknown_or_missing_code_maps_to_the_generic_key")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something.else")]
    public void UnknownCode_MapsToTheGenericKey(string? code)
    {
        // Act
        string key = AlbumEditTextKeys.Error(code);

        // Assert
        Assert.Equal(AlbumEditTextKeys.GenericError, key);
    }

    [Fact(DisplayName = "every_album_edit_key_is_translated_in_every_language")]
    public void EveryKey_ExistsInEveryResourceFile()
    {
        // Arrange
        string folder = StringsFolder();
        string[] blockerKeys = [.. Enum.GetValues<EArtistRenameBlock>().Select(ArtistRenameTextKeys.Blocker)];
        string[] expected = [.. AlbumEditTextKeys.AllKeys, .. blockerKeys, .. TrackEditTextKeys.AllErrorKeys, .. ScreenKeys];

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

    [Fact(DisplayName = "the_formatted_album_edit_texts_keep_their_placeholders_in_every_language")]
    public void FormattedTexts_KeepTheirPlaceholders()
    {
        // Arrange
        string folder = StringsFolder();

        (string Key, string[] Placeholders)[] formatted =
        [
            (AlbumEditTextKeys.ChangeName, ["{0}", "{1}"]),
            (AlbumEditTextKeys.ChangeArtist, ["{0}", "{1}"]),
            (AlbumEditTextKeys.ChangeYear, ["{0}", "{1}"]),
            (AlbumEditTextKeys.ChangeGenre, ["{0}", "{1}"]),
            (AlbumEditTextKeys.Counts, ["{0}"]),
            (AlbumEditTextKeys.ArtistCreated, ["{0}"]),
            (AlbumEditTextKeys.GenreCreated, ["{0}"]),
            (AlbumEditTextKeys.GenreKept, ["{0}"]),
            (AlbumEditTextKeys.OtherArtistKept, ["{0}"]),
            (AlbumEditTextKeys.OrphanArtist, ["{0}", "{1}"]),
            (AlbumEditTextKeys.OrphanFavoriteArtist, ["{0}", "{1}"]),
            (AlbumEditTextKeys.Collision, ["{0}", "{1}", "{2}"])
        ];

        foreach (string language in Languages)
        {
            Dictionary<string, string> values = Values(folder, language);

            foreach ((string key, string[] placeholders) in formatted)
            {
                // Act
                string text = values[key];

                // Assert
                Assert.True(placeholders.All(text.Contains), $"{language} {key}");
            }
        }
    }
}