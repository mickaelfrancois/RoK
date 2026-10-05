namespace Rok.Services.Taskbar;

/// <summary>Translated tooltips of the thumbnail toolbar.</summary>
public record ThumbBarLabels(string Previous, string Play, string Pause, string Next)
{
    public static ThumbBarLabels From(IStringResourceProvider resources) => new(
        resources.GetString(ThumbBarTextKeys.Previous),
        resources.GetString(ThumbBarTextKeys.Play),
        resources.GetString(ThumbBarTextKeys.Pause),
        resources.GetString(ThumbBarTextKeys.Next));
}