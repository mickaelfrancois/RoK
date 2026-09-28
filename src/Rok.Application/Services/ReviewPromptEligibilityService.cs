using Rok.Application.Interfaces;

namespace Rok.Application.Services;

public sealed class ReviewPromptEligibilityService(IAppOptions appOptions, ICrashStore crashStore, TimeProvider timeProvider) : IReviewPromptEligibilityService
{
    internal const int MinSessions = 3;
    internal const int MinTotalTracks = 20;
    internal const int CrashCooldownDays = 7;
    internal const int PromptIntervalDays = 30;

    public bool ShouldShowReviewPrompt()
    {
        if (appOptions.HasRated)
            return false;

        if (crashStore.GetCrashCount() > 0 && !crashStore.HasLastCrashExpired(CrashCooldownDays))
            return false;

        if (appOptions.SessionsCount < MinSessions)
            return false;

        if (appOptions.TotalTracksListened < MinTotalTracks)
            return false;

        if (appOptions.ReviewLastPromptDate.HasValue
            && timeProvider.GetUtcNow() - appOptions.ReviewLastPromptDate.Value < TimeSpan.FromDays(PromptIntervalDays))
            return false;

        return true;
    }
}