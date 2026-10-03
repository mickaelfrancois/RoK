namespace Rok.ViewModels.Listening.Services;

public static class ListeningHeaderLabels
{
    public static string FavoriteToggle(string name, bool isFavorite, string addFormat, string removeFormat)
    {
        return string.Format(isFavorite ? removeFormat : addFormat, name);
    }

    public static string SleepButton(bool isActive, int remainingSeconds, string idleLabel, string remainingFormat)
    {
        if (!isActive)
        {
            return idleLabel;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(remainingSeconds / 60d));

        return string.Format(remainingFormat, minutes);
    }
}