using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Options;
using Rok.Application.Services;

namespace Rok.ApplicationTests;

public class ReviewPromptEligibilityServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ICrashStore> _crashStore = new();
    private readonly AppOptions _options = new()
    {
        SessionsCount = ReviewPromptEligibilityService.MinSessions,
        TotalTracksListened = ReviewPromptEligibilityService.MinTotalTracks,
        HasRated = false,
        ReviewLastPromptDate = null
    };

    private ReviewPromptEligibilityService CreateService() => new(_options, _crashStore.Object, _time);

    [Fact(DisplayName = "eligible_when_three_sessions_and_twenty_tracks")]
    public void Eligible_WhenThreeSessionsAndTwentyTracks()
    {
        // Arrange
        _options.SessionsCount = 3;
        _options.TotalTracksListened = 20;

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "not_eligible_with_two_sessions")]
    public void NotEligible_WithTwoSessions()
    {
        // Arrange
        _options.SessionsCount = 2;
        _options.TotalTracksListened = 50;

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "not_eligible_with_nineteen_tracks")]
    public void NotEligible_WithNineteenTracks()
    {
        // Arrange
        _options.SessionsCount = 10;
        _options.TotalTracksListened = 19;

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "not_eligible_when_already_rated")]
    public void NotEligible_WhenAlreadyRated()
    {
        // Arrange
        _options.HasRated = true;

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "not_eligible_within_seven_days_after_crash")]
    public void NotEligible_WithinSevenDaysAfterCrash()
    {
        // Arrange
        _crashStore.Setup(c => c.GetCrashCount()).Returns(1);
        _crashStore.Setup(c => c.HasLastCrashExpired(7)).Returns(false);

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "eligible_when_last_crash_is_older_than_seven_days")]
    public void Eligible_WhenLastCrashIsOlderThanSevenDays()
    {
        // Arrange
        _crashStore.Setup(c => c.GetCrashCount()).Returns(1);
        _crashStore.Setup(c => c.HasLastCrashExpired(7)).Returns(true);

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.True(result);
        _crashStore.Verify(c => c.HasLastCrashExpired(7), Times.Once);
    }

    [Fact(DisplayName = "eligible_when_no_crash_recorded")]
    public void Eligible_WhenNoCrashRecorded()
    {
        // Arrange
        _crashStore.Setup(c => c.GetCrashCount()).Returns(0);

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.True(result);
        _crashStore.Verify(c => c.HasLastCrashExpired(It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "not_eligible_when_last_prompt_is_29_days_old")]
    public void NotEligible_WhenLastPromptIs29DaysOld()
    {
        // Arrange
        _options.ReviewLastPromptDate = _time.GetUtcNow().AddDays(-29);

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "eligible_when_last_prompt_is_30_days_old")]
    public void Eligible_WhenLastPromptIs30DaysOld()
    {
        // Arrange
        _options.ReviewLastPromptDate = _time.GetUtcNow().AddDays(-30);

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "eligible_when_never_prompted")]
    public void Eligible_WhenNeverPrompted()
    {
        // Arrange
        _options.ReviewLastPromptDate = null;

        // Act
        bool result = CreateService().ShouldShowReviewPrompt();

        // Assert
        Assert.True(result);
    }
}