using Rok.Application.Interfaces;

namespace Rok.Application.Services;

public sealed class ReviewPromptEligibilityService(IAppOptions appOptions, ICrashStore crashStore) : IReviewPromptEligibilityService
{
    public bool ShouldShowReviewPrompt(int tracksListened, int maxTracksBeforePrompt, int minSessionsBeforePrompt, int minDaysBeforePrompt)
    {
        if (appOptions.HasRated)
            return false;

        if (crashStore.GetCrashCount() > 0 && !crashStore.HasLastCrashExpired(minDaysBeforePrompt))
            return false;

        if (appOptions.SessionsCount < minSessionsBeforePrompt)
            return false;

        if (appOptions.ReviewLastPromptDate.HasValue
            && DateTimeOffset.UtcNow - appOptions.ReviewLastPromptDate.Value < TimeSpan.FromDays(minDaysBeforePrompt))
            return false;

        if (tracksListened < maxTracksBeforePrompt)
            return false;

        return true;
    }
}
