using Rok.Application.Player;
using Rok.Services.Accessibility;
using Rok.ViewModels.Player;

namespace Rok.PresentationTests.ViewModels.Player;

public class PlayerCommandTextsTests
{
    private static string Localize(string key) => key;

    public static TheoryData<EPlaybackState> AllStates()
    {
        TheoryData<EPlaybackState> data = new();

        foreach (var state in Enum.GetValues<EPlaybackState>())
            data.Add(state);

        return data;
    }

    [Theory(DisplayName = "play_pause_label_is_pause_only_while_playing")]
    [MemberData(nameof(AllStates))]
    public void PlayPauseLabel_FollowsPlaybackState(EPlaybackState state)
    {
        var expected = state == EPlaybackState.Playing ? "playerPause" : "playerPlay";

        var label = PlayerCommandTexts.PlayPauseLabel(state, Localize);

        Assert.Equal(expected, label);
    }

    [Theory(DisplayName = "play_pause_tooltip_carries_the_space_shortcut_once")]
    [MemberData(nameof(AllStates))]
    public void PlayPauseToolTip_EndsWithSpaceShortcut(EPlaybackState state)
    {
        var toolTip = PlayerCommandTexts.PlayPauseToolTip(state, Localize);

        Assert.EndsWith(" (Space)", toolTip);
        Assert.Equal(2, toolTip.Split("(Space)").Length);
    }

    [Fact(DisplayName = "labels_never_contain_a_shortcut")]
    public void Labels_DoNotContainShortcut()
    {
        var texts = PlayerCommandTexts.Create(Localize);

        string[] labels =
        [
            texts.PreviousLabel,
            texts.NextLabel,
            texts.MuteLabel,
            texts.VolumeLabel,
            texts.QueueLabel,
            texts.ShowCurrentPlaylistLabel,
            texts.ShowLyricsLabel,
            texts.ExitCompactLabel,
            texts.ExitFullScreenLabel
        ];

        Assert.All(labels, label => Assert.DoesNotContain("(", label));
    }

    [Fact(DisplayName = "tooltips_end_with_the_catalog_shortcut")]
    public void ToolTips_EndWithCatalogShortcut()
    {
        var texts = PlayerCommandTexts.Create(Localize);

        Assert.Equal("KeyboardShortcut_Action_Previous (Ctrl+←)", texts.PreviousToolTip);
        Assert.Equal("KeyboardShortcut_Action_Next (Ctrl+→)", texts.NextToolTip);
        Assert.Equal("KeyboardShortcut_Action_Mute (Ctrl+M)", texts.MuteToolTip);
        Assert.Equal("playerQueue (Ctrl+0)", texts.QueueToolTip);
        Assert.Equal("playerExitCompactMode (Ctrl+Shift+M)", texts.ExitCompactToolTip);
        Assert.Equal("playerExitFullScreen (F11)", texts.ExitFullScreenToolTip);
    }

    [Fact(DisplayName = "overflow_shortcut_texts_match_the_catalog")]
    public void ShortcutTexts_MatchCatalog()
    {
        var texts = PlayerCommandTexts.Create(Localize);

        Assert.Equal("Ctrl+Shift+M", texts.CompactShortcutText);
        Assert.Equal("F11", texts.FullScreenShortcutText);
    }

    [Fact(DisplayName = "create_succeeds_because_every_used_shortcut_is_in_the_catalog")]
    public void Create_DoesNotThrow()
    {
        var exception = Record.Exception(() => PlayerCommandTexts.Create(Localize));

        Assert.Null(exception);
    }

    [Theory(DisplayName = "mode_tooltips_are_the_state_text_followed_by_the_shortcut")]
    [InlineData(ShortcutId.Shuffle, "Ctrl+H")]
    [InlineData(ShortcutId.Repeat, "Ctrl+T")]
    public void WithShortcut_ComposesStateTextAndShortcut(ShortcutId id, string shortcut)
    {
        var stateText = PlayerModeTextKeys.Shuffle(false);

        var toolTip = KeyboardShortcutFormatter.WithShortcut(stateText, id);

        Assert.Equal($"{stateText} ({shortcut})", toolTip);
    }
}