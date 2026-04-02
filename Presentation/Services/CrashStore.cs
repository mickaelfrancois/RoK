using System.Globalization;
using Windows.Storage;

namespace Rok.Services;

internal sealed class CrashStore : ICrashStore
{
    private const string KCrashCountKey = "CrashCount";
    private const string KLastCrashDateKey = "LastCrashDate";

    public int GetCrashCount()
        => (int)(ApplicationData.Current.LocalSettings.Values[KCrashCountKey] ?? 0);

    public DateTimeOffset? GetLastCrashDate()
    {
        object? value = ApplicationData.Current.LocalSettings.Values[KLastCrashDateKey];
        return value is string s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset date) ? date : null;
    }

    public bool HasLastCrashExpired(int days)
    {
        DateTimeOffset? lastCrashDate = GetLastCrashDate();
        return lastCrashDate.HasValue && DateTimeOffset.UtcNow - lastCrashDate.Value >= TimeSpan.FromDays(days);
    }

    public void IncrementCrashCount()
    {
        ApplicationData.Current.LocalSettings.Values[KCrashCountKey] = GetCrashCount() + 1;
        ApplicationData.Current.LocalSettings.Values[KLastCrashDateKey] = DateTimeOffset.UtcNow.ToString("O");
    }
}