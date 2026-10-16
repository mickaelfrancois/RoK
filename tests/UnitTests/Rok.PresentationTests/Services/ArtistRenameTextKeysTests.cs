using System.Xml.Linq;
using Rok.Application.Features.Artists.Rename;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class ArtistRenameTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static readonly string[] ScreenKeys =
    [
        "artistNameInput.Header",
        "RenameArtistDialog.Title",
        "RenameArtistDialog.CloseButtonText",
        "artistRenamePendingBar.Title",
        "artistRenamePendingBar.Message",
        "artistRenameResumeButton.Content"
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    [Fact(DisplayName = "every_blocker_reason_has_its_own_key")]
    public void EveryBlocker_HasItsOwnKey()
    {
        // Act
        string[] keys = [.. Enum.GetValues<EArtistRenameBlock>().Select(ArtistRenameTextKeys.Blocker)];

        // Assert
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.All(keys, key => Assert.Contains(key, ArtistRenameTextKeys.AllKeys));
    }

    [Theory(DisplayName = "every_known_error_code_maps_to_a_specific_key")]
    [InlineData("artist.rename.disabled")]
    [InlineData("artist.rename.collision")]
    [InlineData("artist.rename.pending")]
    [InlineData("artist.rename.unchanged")]
    [InlineData("artist.rename.invalid_name")]
    [InlineData("artist.not_found")]
    public void KnownCode_MapsToASpecificKey(string code)
    {
        // Act
        string key = ArtistRenameTextKeys.Error(code);

        // Assert
        Assert.NotEqual(ArtistRenameTextKeys.GenericError, key);
    }

    [Theory(DisplayName = "unknown_or_missing_code_maps_to_the_generic_key")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something.else")]
    public void UnknownCode_MapsToTheGenericKey(string? code)
    {
        // Act
        string key = ArtistRenameTextKeys.Error(code);

        // Assert
        Assert.Equal(ArtistRenameTextKeys.GenericError, key);
    }

    [Fact(DisplayName = "every_artist_rename_key_is_translated_in_every_language")]
    public void EveryKey_ExistsInEveryResourceFile()
    {
        // Arrange
        string folder = StringsFolder();
        string[] expected = [.. ArtistRenameTextKeys.AllKeys, .. TrackEditTextKeys.AllErrorKeys, .. ScreenKeys];

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

    [Fact(DisplayName = "the_collision_and_counts_texts_keep_their_placeholders_in_every_language")]
    public void FormattedTexts_KeepTheirPlaceholders()
    {
        // Arrange
        string folder = StringsFolder();

        foreach (string language in Languages)
        {
            Dictionary<string, string> values = XDocument.Load(Path.Combine(folder, language, "Resources.resw")).Root!.Elements("data")
                .ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")!.Value);

            // Act
            string collision = values[ArtistRenameTextKeys.Collision];
            string counts = values[ArtistRenameTextKeys.Counts];

            // Assert
            Assert.True(collision.Contains("{0}") && collision.Contains("{1}") && collision.Contains("{2}"), $"{language} collision text");
            Assert.True(counts.Contains("{0}") && counts.Contains("{1}"), $"{language} counts text");
        }
    }
}