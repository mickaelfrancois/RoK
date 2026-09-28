using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Application.Services;

public sealed class ReviewPromptService(
    IAppOptions appOptions,
    IReviewPromptEligibilityService eligibility,
    IStoreReviewService storeReview,
    ITelemetryClient telemetryClient,
    ISettingsFile settingsFile,
    TimeProvider timeProvider,
    ILogger<ReviewPromptService> logger) : IReviewPromptService
{
    private const string TelemetryType = "Review";

    private bool _isRunning;

    public async Task OnTrackStartedAsync(IReviewPromptView view)
    {
        appOptions.TotalTracksListened++;

        if (_isRunning || !eligibility.ShouldShowReviewPrompt())
            return;

        _isRunning = true;
        appOptions.ReviewLastPromptDate = timeProvider.GetUtcNow();

        try
        {
            Capture("Prompted");
            await RunFlowAsync(view);
        }
        finally
        {
            await SaveSettingsAsync();
            _isRunning = false;
        }
    }

    private async Task RunFlowAsync(IReviewPromptView view)
    {
        bool? sentiment = await view.AskSentimentAsync();

        if (sentiment == true)
        {
            Capture("SentimentYes");

            StoreReviewStatus status = await storeReview.RequestRateAndReviewAsync();
            Capture("StoreDialog", new Dictionary<string, object> { ["status"] = status.ToString() });

            if (status == StoreReviewStatus.Succeeded)
                appOptions.HasRated = true;

            return;
        }

        if (sentiment == false)
        {
            Capture("SentimentNo");

            if (await view.AskFeedbackAsync() == true)
            {
                await view.OpenFeedbackPageAsync();
                Capture("FeedbackOpened");
            }
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await settingsFile.SaveAsync(appOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to save settings after the rating prompt.");
        }
    }

    private void Capture(string eventName, Dictionary<string, object>? properties = null) =>
        _ = telemetryClient.CaptureEventAsync(TelemetryType, eventName, properties);
}