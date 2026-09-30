using System.Xml.Linq;
using Rok.Application.Player.Output;
using Rok.Services;

namespace Rok.PresentationTests.Services;

public class AudioOutputTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static string[] AllKeys() =>
    [
        .. Enum.GetValues<EAudioOutputStatus>().Select(AudioOutputTextKeys.Status),
        .. Enum.GetValues<EExclusiveFallbackReason>().Select(AudioOutputTextKeys.FallbackToolTip)
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    [Fact(DisplayName = "status_and_fallback_reasons_map_to_dedicated_resource_keys")]
    public void EveryValue_MapsToADedicatedKey()
    {
        // Act
        string[] keys = AllKeys();

        // Assert
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact(DisplayName = "every_audio_output_key_is_translated_in_every_language")]
    public void EveryKey_ExistsInEveryResourceFile()
    {
        // Arrange
        string folder = StringsFolder();

        foreach (string language in Languages)
        {
            HashSet<string?> names = [.. XDocument.Load(Path.Combine(folder, language, "Resources.resw")).Root!.Elements("data").Select(data => data.Attribute("name")?.Value)];

            // Act
            string[] missing = [.. AllKeys().Where(key => !names.Contains(key))];

            // Assert
            Assert.True(missing.Length == 0, $"{language} misses: {string.Join(", ", missing)}");
        }
    }
}