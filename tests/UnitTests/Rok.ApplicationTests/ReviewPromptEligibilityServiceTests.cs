using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Services;

namespace Rok.ApplicationTests;

public class ReviewPromptEligibilityServiceTests
{
    private readonly Mock<IAppOptions> mockAppOptions;
    private readonly Mock<ICrashStore> mockCrashStore;
    private readonly ReviewPromptEligibilityService service;

    public ReviewPromptEligibilityServiceTests()
    {
        mockAppOptions = new Mock<IAppOptions>();
        mockCrashStore = new Mock<ICrashStore>();
        service = new ReviewPromptEligibilityService(mockAppOptions.Object, mockCrashStore.Object);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsFalse_WhenUserHasAlreadyRated()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(true);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(10);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns((DateTimeOffset?)null);
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(0);

        bool result = service.ShouldShowReviewPrompt(10, 10, 3, 45);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsFalse_WhenRecentCrashExists()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(10);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns((DateTimeOffset?)null);
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(1);
        mockCrashStore.Setup(c => c.HasLastCrashExpired(45)).Returns(false);

        bool result = service.ShouldShowReviewPrompt(10, 10, 3, 45);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsTrue_WhenCrashHasExpired()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(10);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns((DateTimeOffset?)null);
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(1);
        mockCrashStore.Setup(c => c.HasLastCrashExpired(45)).Returns(true);

        bool result = service.ShouldShowReviewPrompt(10, 10, 3, 45);

        Assert.True(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsFalse_WhenNotEnoughSessions()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(2);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns((DateTimeOffset?)null);
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(0);

        bool result = service.ShouldShowReviewPrompt(10, 10, 3, 45);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsFalse_WhenLastPromptWasTooRecent()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(10);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns(DateTimeOffset.UtcNow.AddDays(-30));
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(0);

        bool result = service.ShouldShowReviewPrompt(10, 10, 3, 45);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsTrue_WhenLastPromptWasLongAgo()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(10);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns(DateTimeOffset.UtcNow.AddDays(-50));
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(0);

        bool result = service.ShouldShowReviewPrompt(10, 10, 3, 45);

        Assert.True(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsFalse_WhenNotEnoughTracksListened()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(10);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns((DateTimeOffset?)null);
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(0);

        bool result = service.ShouldShowReviewPrompt(5, 10, 3, 45);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsTrue_WhenAllConditionsAreMet()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(10);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns((DateTimeOffset?)null);
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(0);

        bool result = service.ShouldShowReviewPrompt(10, 10, 3, 45);

        Assert.True(result);
    }

    [Fact]
    public void ShouldShowReviewPrompt_ReturnsTrue_WhenNoCrashesExist()
    {
        mockAppOptions.SetupGet(o => o.HasRated).Returns(false);
        mockAppOptions.SetupGet(o => o.SessionsCount).Returns(5);
        mockAppOptions.SetupGet(o => o.ReviewLastPromptDate).Returns((DateTimeOffset?)null);
        mockCrashStore.Setup(c => c.GetCrashCount()).Returns(0);

        bool result = service.ShouldShowReviewPrompt(15, 10, 3, 45);

        Assert.True(result);
    }
}