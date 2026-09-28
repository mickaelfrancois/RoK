namespace Rok.Application.Interfaces;

public interface IReviewPromptService
{
    /// <summary>
    /// Counts a started track and runs the rating prompt flow when the user is eligible.
    /// Must be called on the UI thread.
    /// </summary>
    Task OnTrackStartedAsync(IReviewPromptView view);
}