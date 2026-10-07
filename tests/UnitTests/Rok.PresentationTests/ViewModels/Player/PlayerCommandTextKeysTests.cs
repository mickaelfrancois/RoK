using System.Xml.Linq;
using Rok.Application.Player;
using Rok.ViewModels.Player;

namespace Rok.PresentationTests.ViewModels.Player;

public class PlayerCommandTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static readonly string[] OwnKeys =
    [
        PlayerCommandTextKeys.Queue,
        PlayerCommandTextKeys.ShowCurrentPlaylist,
        PlayerCommandTextKeys.ShowLyrics,
        PlayerCommandTextKeys.ExitFullScreen,
        PlayerCommandTextKeys.ExitCompactMode,
        PlayerCommandTextKeys.Volume,
        PlayerCommandTextKeys.PlayPause(EPlaybackState.Playing),
        PlayerCommandTextKeys.PlayPause(EPlaybackState.Paused)
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    private static Dictionary<string, string> Load(string language) =>
        XDocument.Load(Path.Combine(StringsFolder(), language, "Resources.resw")).Root!
            .Elements("data")
            .ToDictionary(data => data.Attribute("name")!.Value, data => data.Element("value")?.Value ?? string.Empty);

    [Fact(DisplayName = "every_player_command_key_is_non_empty_and_distinct")]
    public void AllKeys_AreNonEmptyAndDistinct()
    {
        // Act
        IReadOnlyList<string> keys = PlayerCommandTextKeys.All;

        // Assert
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact(DisplayName = "every_player_command_key_exists_in_every_language")]
    public void EveryKey_ExistsInEveryResourceFile()
    {
        foreach (string language in Languages)
        {
            // Arrange
            Dictionary<string, string> entries = Load(language);

            // Act
            string[] missing = [.. PlayerCommandTextKeys.All.Where(key => !entries.ContainsKey(key))];

            // Assert
            Assert.True(missing.Length == 0, $"{language} misses: {string.Join(", ", missing)}");
        }
    }

    [Fact(DisplayName = "player_command_keys_contain_no_square_bracket")]
    public void AllKeys_ContainNoBracket()
    {
        // Act
        IReadOnlyList<string> keys = PlayerCommandTextKeys.All;

        // Assert
        Assert.All(keys, key => Assert.False(key.Contains('[') || key.Contains(']')));
    }

    [Fact(DisplayName = "player_command_values_never_hardcode_a_shortcut")]
    public void OwnKeyValues_ContainNoShortcut()
    {
        foreach (string language in Languages)
        {
            // Arrange
            Dictionary<string, string> entries = Load(language);

            // Act
            string[] offending = [.. OwnKeys.Where(key => entries[key].Contains("Ctrl", StringComparison.Ordinal) || entries[key].Contains('(') || entries[key].Contains("F11", StringComparison.Ordinal))];

            // Assert
            Assert.True(offending.Length == 0, $"{language} hardcodes a shortcut in: {string.Join(", ", offending)}");
        }
    }

    [Fact(DisplayName = "obsolete_player_tooltip_keys_are_removed_from_every_language")]
    public void ObsoleteKeys_AreAbsentFromEveryResourceFile()
    {
        // Arrange
        string[] obsolete =
        [
            "PlayerSleepTimerToolTips.Text",
            "PlayerCurrentListeningToolTips.Text",
            "PlayerFullScreenToolTips.Text",
            "PlayerFullScreenCurrentListeningToolTips.Text",
            "PlayerFullScreenLyricsToolTips.Text",
            "compactExitButton.ToolTipService.ToolTip",
            "compactExitButton.AutomationProperties.Name"
        ];

        foreach (string language in Languages)
        {
            // Act
            Dictionary<string, string> entries = Load(language);
            string[] stillPresent = [.. obsolete.Where(entries.ContainsKey)];

            // Assert
            Assert.True(stillPresent.Length == 0, $"{language} still has: {string.Join(", ", stillPresent)}");
        }
    }

    [Theory(DisplayName = "play_pause_key_is_pause_only_while_playing")]
    [MemberData(nameof(PlaybackStates))]
    public void PlayPause_MapsPlayingToPauseAndEverythingElseToPlay(EPlaybackState state)
    {
        // Act
        string key = PlayerCommandTextKeys.PlayPause(state);

        // Assert
        Assert.Equal(state == EPlaybackState.Playing ? "playerPause" : "playerPlay", key);
    }

    public static TheoryData<EPlaybackState> PlaybackStates() => [.. Enum.GetValues<EPlaybackState>()];
}