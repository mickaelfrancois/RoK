using System.Xml.Linq;
using Moq;
using Rok.Services;
using Rok.Services.Taskbar;

namespace Rok.PresentationTests.Services.Taskbar;

public class ThumbBarTextKeysTests
{
    private static readonly string[] Languages = ["en-US", "fr-FR", "es-ES", "uk-UA"];

    private static string[] AllKeys() =>
    [
        ThumbBarTextKeys.Previous,
        ThumbBarTextKeys.Play,
        ThumbBarTextKeys.Pause,
        ThumbBarTextKeys.Next
    ];

    private static string StringsFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Strings")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Presentation", "Strings");
    }

    [Fact(DisplayName = "every_thumb_bar_tooltip_key_is_translated_in_every_language")]
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

    [Fact(DisplayName = "tooltip_key_switches_between_play_and_pause")]
    public void TooltipKey_PlayPause_DependsOnShowPause()
    {
        // Act
        var whenPlaying = ThumbBarTextKeys.TooltipKey(ThumbBarButton.PlayPause, true);
        var whenPaused = ThumbBarTextKeys.TooltipKey(ThumbBarButton.PlayPause, false);

        // Assert
        Assert.Equal(ThumbBarTextKeys.Pause, whenPlaying);
        Assert.Equal(ThumbBarTextKeys.Play, whenPaused);
        Assert.Equal(ThumbBarTextKeys.Previous, ThumbBarTextKeys.TooltipKey(ThumbBarButton.Previous, true));
        Assert.Equal(ThumbBarTextKeys.Next, ThumbBarTextKeys.TooltipKey(ThumbBarButton.Next, false));
    }

    [Fact(DisplayName = "labels_are_read_from_the_resource_provider")]
    public void Labels_From_ReadsEveryKey()
    {
        // Arrange
        var resources = new Mock<IStringResourceProvider>();
        resources.Setup(r => r.GetString(It.IsAny<string>())).Returns<string>(key => $"text:{key}");

        // Act
        var labels = ThumbBarLabels.From(resources.Object);

        // Assert
        Assert.Equal("text:taskbarThumbPrevious", labels.Previous);
        Assert.Equal("text:taskbarThumbPlay", labels.Play);
        Assert.Equal("text:taskbarThumbPause", labels.Pause);
        Assert.Equal("text:taskbarThumbNext", labels.Next);
    }
}