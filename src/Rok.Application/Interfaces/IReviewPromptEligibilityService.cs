namespace Rok.Application.Interfaces;

public interface IReviewPromptEligibilityService
{
    /// <summary>
    /// Returns <c>true</c> when the user may be asked to rate the app now.
    /// </summary>
    bool ShouldShowReviewPrompt();
}