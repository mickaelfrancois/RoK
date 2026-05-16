namespace Rok.Application.Interfaces;

public interface ICrashStore
{
    int GetCrashCount();

    DateTimeOffset? GetLastCrashDate();

    bool HasLastCrashExpired(int days);
}