namespace Rok.Services.Taskbar;

/// <summary>Resource keys of the thumbnail toolbar tooltips.</summary>
public static class ThumbBarTextKeys
{
    public const string Previous = "taskbarThumbPrevious";
    public const string Play = "taskbarThumbPlay";
    public const string Pause = "taskbarThumbPause";
    public const string Next = "taskbarThumbNext";

    public static string TooltipKey(ThumbBarButton button, bool showPause) => button switch
    {
        ThumbBarButton.Previous => Previous,
        ThumbBarButton.Next => Next,
        _ => showPause ? Pause : Play
    };
}