using System.Xml.Linq;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Tag;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class TrackEditTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static readonly string[] ScreenKeys =
    [
        "EditTrackDialog.Title",
        "EditTrackDialog.PrimaryButtonText",
        "EditTrackDialog.CloseButtonText",
        "EditTrackTitleInput.Header",
        "EditTrackNumberInput.Header",
        "EditTrackDiscInput.Header",
        "EditTrackComposerInput.Header",
        "EditTrackBpmInput.Header",
        "trackEditText.Text",
        "OptionsWriteTagsSwitch.Header",
        "OptionsWriteTagsDescription.Text"
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    [Fact(DisplayName = "every_tag_write_reason_maps_to_a_known_error_key")]
    public void EveryReason_MapsToAKnownKey()
    {
        foreach (ETagWriteReason reason in Enum.GetValues<ETagWriteReason>().Where(reason => reason != ETagWriteReason.Cancelled && reason != ETagWriteReason.None))
        {
            // Act
            string key = TrackEditTextKeys.Error(TrackTagEditResult.CodeFor(reason));

            // Assert
            Assert.NotEqual(TrackEditTextKeys.GenericError, key);
            Assert.Contains(key, TrackEditTextKeys.AllErrorKeys);
        }
    }

    [Theory(DisplayName = "unknown_or_missing_code_maps_to_the_generic_key")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something.else")]
    public void UnknownCode_MapsToGenericKey(string? code)
    {
        // Act
        string key = TrackEditTextKeys.Error(code);

        // Assert
        Assert.Equal(TrackEditTextKeys.GenericError, key);
    }

    [Fact(DisplayName = "disabled_code_maps_to_the_disabled_key")]
    public void DisabledCode_MapsToDisabledKey()
    {
        // Act
        string key = TrackEditTextKeys.Error(TrackTagEditResult.DisabledCode);

        // Assert
        Assert.Equal("trackEditError_Disabled", key);
    }

    [Fact(DisplayName = "every_track_edit_key_is_translated_in_every_language")]
    public void EveryKey_ExistsInEveryResourceFile()
    {
        // Arrange
        string folder = StringsFolder();
        string[] expected = [.. TrackEditTextKeys.AllErrorKeys, .. TrackEditTextKeys.AllValidationKeys, .. ScreenKeys];

        foreach (string language in Languages)
        {
            HashSet<string?> names = [.. XDocument.Load(Path.Combine(folder, language, "Resources.resw")).Root!.Elements("data").Select(data => data.Attribute("name")?.Value)];

            // Act
            string[] missing = [.. expected.Where(key => !names.Contains(key))];

            // Assert
            Assert.True(missing.Length == 0, $"{language} misses: {string.Join(", ", missing)}");
        }
    }
}