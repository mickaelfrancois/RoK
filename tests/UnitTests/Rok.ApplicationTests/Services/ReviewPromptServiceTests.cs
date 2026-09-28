using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Options;
using Rok.Application.Services;

namespace Rok.ApplicationTests.Services;

public class ReviewPromptServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly AppOptions _options = new() { SessionsCount = 5, TotalTracksListened = 5 };
    private readonly Mock<IReviewPromptEligibilityService> _eligibility = new();
    private readonly Mock<IStoreReviewService> _storeReview = new();
    private readonly Mock<ITelemetryClient> _telemetry = new();
    private readonly Mock<ISettingsFile> _settingsFile = new();
    private readonly Mock<IReviewPromptView> _view = new();

    private ReviewPromptService CreateService(IReviewPromptEligibilityService? eligibility = null) =>
        new(_options, eligibility ?? _eligibility.Object, _storeReview.Object, _telemetry.Object, _settingsFile.Object, _time, NullLogger<ReviewPromptService>.Instance);

    private void SetupEligible() => _eligibility.Setup(e => e.ShouldShowReviewPrompt()).Returns(true);

    private void VerifyEvent(string eventName, Times times) =>
        _telemetry.Verify(t => t.CaptureEventAsync("Review", eventName, It.IsAny<Dictionary<string, object>?>()), times);

    [Fact(DisplayName = "track_started_increments_total_tracks_listened")]
    public async Task TrackStarted_IncrementsTotalTracksListened()
    {
        // Arrange
        _eligibility.Setup(e => e.ShouldShowReviewPrompt()).Returns(false);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        Assert.Equal(6, _options.TotalTracksListened);
    }

    [Fact(DisplayName = "not_eligible_does_not_prompt_nor_save")]
    public async Task NotEligible_DoesNotPromptNorSave()
    {
        // Arrange
        _eligibility.Setup(e => e.ShouldShowReviewPrompt()).Returns(false);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        _view.Verify(v => v.AskSentimentAsync(), Times.Never);
        _settingsFile.Verify(s => s.SaveAsync(It.IsAny<IAppOptions>()), Times.Never);
        _telemetry.Verify(t => t.CaptureEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>()), Times.Never);
    }

    [Fact(DisplayName = "eligible_sets_last_prompt_date_and_emits_prompted")]
    public async Task Eligible_SetsLastPromptDate_AndEmitsPrompted()
    {
        // Arrange
        SetupEligible();

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        Assert.Equal(_time.GetUtcNow(), _options.ReviewLastPromptDate);
        VerifyEvent("Prompted", Times.Once());
    }

    [Fact(DisplayName = "sentiment_yes_opens_store_dialog_and_emits_status")]
    public async Task SentimentYes_OpensStoreDialog_AndEmitsStatus()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(true);
        _storeReview.Setup(s => s.RequestRateAndReviewAsync()).ReturnsAsync(StoreReviewStatus.Succeeded);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        _storeReview.Verify(s => s.RequestRateAndReviewAsync(), Times.Once);
        VerifyEvent("SentimentYes", Times.Once());
        _telemetry.Verify(t => t.CaptureEventAsync("Review", "StoreDialog",
            It.Is<Dictionary<string, object>?>(p => p != null && (string)p["status"] == "Succeeded")), Times.Once);
    }

    [Fact(DisplayName = "store_dialog_succeeded_sets_has_rated")]
    public async Task StoreDialogSucceeded_SetsHasRated()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(true);
        _storeReview.Setup(s => s.RequestRateAndReviewAsync()).ReturnsAsync(StoreReviewStatus.Succeeded);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        Assert.True(_options.HasRated);
    }

    [Fact(DisplayName = "store_dialog_canceled_keeps_has_rated_false")]
    public async Task StoreDialogCanceled_KeepsHasRatedFalse()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(true);
        _storeReview.Setup(s => s.RequestRateAndReviewAsync()).ReturnsAsync(StoreReviewStatus.CanceledByUser);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        Assert.False(_options.HasRated);
        Assert.Equal(_time.GetUtcNow(), _options.ReviewLastPromptDate);
        _telemetry.Verify(t => t.CaptureEventAsync("Review", "StoreDialog",
            It.Is<Dictionary<string, object>?>(p => p != null && (string)p["status"] == "CanceledByUser")), Times.Once);
    }

    [Theory(DisplayName = "store_dialog_error_keeps_has_rated_false")]
    [InlineData(StoreReviewStatus.NetworkError)]
    [InlineData(StoreReviewStatus.Error)]
    public async Task StoreDialogError_KeepsHasRatedFalse(StoreReviewStatus status)
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(true);
        _storeReview.Setup(s => s.RequestRateAndReviewAsync()).ReturnsAsync(status);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        Assert.False(_options.HasRated);
    }

    [Fact(DisplayName = "prompt_comes_back_after_thirty_days_when_store_dialog_canceled")]
    public async Task PromptComesBack_AfterThirtyDays_WhenStoreDialogCanceled()
    {
        // Arrange
        _options.SessionsCount = 3;
        _options.TotalTracksListened = 19;
        ReviewPromptEligibilityService eligibility = new(_options, new Mock<ICrashStore>().Object, _time);
        ReviewPromptService service = CreateService(eligibility);
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(true);
        _storeReview.Setup(s => s.RequestRateAndReviewAsync()).ReturnsAsync(StoreReviewStatus.CanceledByUser);

        // Act
        await service.OnTrackStartedAsync(_view.Object);
        _time.Advance(TimeSpan.FromDays(29));
        await service.OnTrackStartedAsync(_view.Object);
        int promptsAfter29Days = _view.Invocations.Count(i => i.Method.Name == nameof(IReviewPromptView.AskSentimentAsync));
        _time.Advance(TimeSpan.FromDays(1));
        await service.OnTrackStartedAsync(_view.Object);

        // Assert
        Assert.Equal(1, promptsAfter29Days);
        _view.Verify(v => v.AskSentimentAsync(), Times.Exactly(2));
        Assert.False(_options.HasRated);
    }

    [Fact(DisplayName = "sentiment_no_emits_sentiment_no_and_asks_feedback")]
    public async Task SentimentNo_EmitsSentimentNo_AndAsksFeedback()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(false);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        VerifyEvent("SentimentNo", Times.Once());
        _view.Verify(v => v.AskFeedbackAsync(), Times.Once);
        _storeReview.Verify(s => s.RequestRateAndReviewAsync(), Times.Never);
    }

    [Fact(DisplayName = "feedback_accepted_opens_page_and_emits_feedback_opened")]
    public async Task FeedbackAccepted_OpensPage_AndEmitsFeedbackOpened()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(false);
        _view.Setup(v => v.AskFeedbackAsync()).ReturnsAsync(true);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        _view.Verify(v => v.OpenFeedbackPageAsync(), Times.Once);
        VerifyEvent("FeedbackOpened", Times.Once());
    }

    [Fact(DisplayName = "feedback_declined_does_not_open_page")]
    public async Task FeedbackDeclined_DoesNotOpenPage()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(false);
        _view.Setup(v => v.AskFeedbackAsync()).ReturnsAsync(false);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        _view.Verify(v => v.OpenFeedbackPageAsync(), Times.Never);
        VerifyEvent("FeedbackOpened", Times.Never());
    }

    [Fact(DisplayName = "sentiment_dismissed_emits_only_prompted")]
    public async Task SentimentDismissed_EmitsOnlyPrompted()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync((bool?)null);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        VerifyEvent("Prompted", Times.Once());
        _telemetry.Verify(t => t.CaptureEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>()), Times.Once);
        _storeReview.Verify(s => s.RequestRateAndReviewAsync(), Times.Never);
    }

    [Fact(DisplayName = "completed_flow_saves_settings_once")]
    public async Task CompletedFlow_SavesSettingsOnce()
    {
        // Arrange
        SetupEligible();
        _view.Setup(v => v.AskSentimentAsync()).ReturnsAsync(true);
        _storeReview.Setup(s => s.RequestRateAndReviewAsync()).ReturnsAsync(StoreReviewStatus.Succeeded);

        // Act
        await CreateService().OnTrackStartedAsync(_view.Object);

        // Assert
        _settingsFile.Verify(s => s.SaveAsync(_options), Times.Once);
    }

    [Fact(DisplayName = "settings_save_failure_does_not_throw")]
    public async Task SettingsSaveFailure_DoesNotThrow()
    {
        // Arrange
        SetupEligible();
        _settingsFile.Setup(s => s.SaveAsync(It.IsAny<IAppOptions>())).ThrowsAsync(new IOException("disk full"));

        // Act
        Exception? exception = await Record.ExceptionAsync(() => CreateService().OnTrackStartedAsync(_view.Object));

        // Assert
        Assert.Null(exception);
    }

    [Fact(DisplayName = "track_started_while_prompt_open_does_not_prompt_twice")]
    public async Task TrackStarted_WhilePromptOpen_DoesNotPromptTwice()
    {
        // Arrange
        SetupEligible();
        TaskCompletionSource<bool?> sentiment = new();
        _view.Setup(v => v.AskSentimentAsync()).Returns(sentiment.Task);
        ReviewPromptService service = CreateService();

        // Act
        Task first = service.OnTrackStartedAsync(_view.Object);
        await service.OnTrackStartedAsync(_view.Object);
        sentiment.SetResult(null);
        await first;

        // Assert
        _view.Verify(v => v.AskSentimentAsync(), Times.Once);
        Assert.Equal(7, _options.TotalTracksListened);
    }
}