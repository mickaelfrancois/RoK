namespace Rok.Application.Interfaces;

/// <summary>
/// Screens shown by the rating prompt flow.
/// </summary>
public interface IReviewPromptView
{
    /// <summary>
    /// Asks whether the user enjoys the app: <c>true</c> for yes, <c>false</c> for no, <c>null</c> when dismissed.
    /// </summary>
    Task<bool?> AskSentimentAsync();

    /// <summary>
    /// Offers to send feedback: <c>true</c> when the user accepts.
    /// </summary>
    Task<bool?> AskFeedbackAsync();

    /// <summary>
    /// Opens the page where the user can send feedback.
    /// </summary>
    Task OpenFeedbackPageAsync();
}