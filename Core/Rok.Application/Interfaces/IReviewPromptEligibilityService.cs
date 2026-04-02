namespace Rok.Application.Interfaces;

public interface IReviewPromptEligibilityService
{
    bool ShouldShowReviewPrompt(int tracksListened, int maxTracksBeforePrompt, int minSessionsBeforePrompt, int minDaysBeforePrompt);
}
