using System.Xml.Linq;
using Rok.Application.Player;
using Rok.ViewModels.Player;

namespace Rok.PresentationTests.ViewModels.Player;

public class PlayerModeTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static string[] AllKeys() =>
    [
        .. Enum.GetValues<ERepeatMode>().Select(PlayerModeTextKeys.Repeat),
        PlayerModeTextKeys.Shuffle(true),
        PlayerModeTextKeys.Shuffle(false)
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    [Fact(DisplayName = "every_repeat_and_shuffle_state_maps_to_a_distinct_resource_key")]
    public void EveryState_MapsToADistinctKey()
    {
        // Act
        string[] keys = AllKeys();

        // Assert
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact(DisplayName = "every_player_mode_key_is_translated_in_every_language")]
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