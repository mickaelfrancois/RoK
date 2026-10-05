namespace Rok.Services.Taskbar;

/// <summary>What the thumbnail toolbar shows: the play/pause icon and which buttons are enabled.</summary>
public record ThumbBarState(bool ShowPause, bool IsPlayPauseEnabled, bool IsPreviousEnabled, bool IsNextEnabled);