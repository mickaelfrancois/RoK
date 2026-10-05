using Microsoft.Extensions.Time.Testing;
using Rok.Services.Diagnostics;

namespace Rok.PresentationTests.Services.Diagnostics;

public class NavigationTrailTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact(DisplayName = "navigating_sets_pending_page_and_navigating_phase")]
    public void Navigating_SetsPendingPageAndNavigatingPhase()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("WelcomePage");

        // Act
        trail.OnNavigating("AlbumsPage");
        NavigationSnapshot snapshot = trail.Snapshot();

        // Assert
        Assert.Equal("AlbumsPage", snapshot.PendingPage);
        Assert.Equal(NavigationPhase.Navigating, snapshot.Phase);
        Assert.Equal("WelcomePage", snapshot.CurrentPage);
    }

    [Fact(DisplayName = "navigated_moves_current_to_previous_and_clears_pending")]
    public void Navigated_MovesCurrentToPreviousAndClearsPending()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("WelcomePage");
        trail.OnNavigating("AlbumsPage");

        // Act
        trail.OnNavigated("AlbumsPage");
        NavigationSnapshot snapshot = trail.Snapshot();

        // Assert
        Assert.Equal("AlbumsPage", snapshot.CurrentPage);
        Assert.Equal("WelcomePage", snapshot.PreviousPage);
        Assert.Null(snapshot.PendingPage);
        Assert.Equal(NavigationPhase.Navigated, snapshot.Phase);
    }

    [Fact(DisplayName = "loaded_for_current_page_sets_loaded_phase")]
    public void Loaded_ForCurrentPage_SetsLoadedPhase()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("AlbumsPage");

        // Act
        trail.OnLoaded("AlbumsPage");

        // Assert
        Assert.Equal(NavigationPhase.Loaded, trail.Snapshot().Phase);
    }

    [Fact(DisplayName = "loaded_for_a_stale_page_is_ignored")]
    public void Loaded_ForStalePage_IsIgnored()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("WelcomePage");
        trail.OnNavigated("AlbumsPage");

        // Act
        trail.OnLoaded("WelcomePage");

        // Assert
        Assert.Equal(NavigationPhase.Navigated, trail.Snapshot().Phase);
    }

    [Fact(DisplayName = "failed_navigation_sets_failed_phase_and_keeps_current")]
    public void Failed_SetsFailedPhaseAndKeepsCurrent()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("AlbumsPage");
        trail.OnNavigating("ArtistPage");

        // Act
        trail.OnFailed("ArtistPage");
        NavigationSnapshot snapshot = trail.Snapshot();

        // Assert
        Assert.Equal(NavigationPhase.Failed, snapshot.Phase);
        Assert.Equal("AlbumsPage", snapshot.CurrentPage);
        Assert.Null(snapshot.PendingPage);
    }

    [Fact(DisplayName = "ms_in_phase_uses_time_provider")]
    public void MsInPhase_UsesTimeProvider()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigating("AlbumsPage");

        // Act
        _time.Advance(TimeSpan.FromMilliseconds(250));

        // Assert
        Assert.Equal(250, trail.Snapshot().MsInPhase);
    }

    [Fact(DisplayName = "is_redundant_when_target_is_current_page_without_parameter")]
    public void IsRedundant_WhenTargetIsCurrentPageWithoutParameter()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("ListeningPage");

        // Act
        bool redundant = trail.IsRedundant("ListeningPage", hasParameter: false);

        // Assert
        Assert.True(redundant);
    }

    [Fact(DisplayName = "is_not_redundant_while_a_navigation_is_pending")]
    public void IsNotRedundant_WhileNavigationIsPending()
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("ListeningPage");
        trail.OnNavigating("AlbumsPage");

        // Act
        bool redundant = trail.IsRedundant("ListeningPage", hasParameter: false);

        // Assert
        Assert.False(redundant);
    }

    [Theory(DisplayName = "is_not_redundant_with_a_parameter_or_another_page")]
    [InlineData("ListeningPage", true)]
    [InlineData("AlbumsPage", false)]
    public void IsNotRedundant_WithParameterOrAnotherPage(string target, bool hasParameter)
    {
        // Arrange
        NavigationTrail trail = new(_time);
        trail.OnNavigated("ListeningPage");

        // Act
        bool redundant = trail.IsRedundant(target, hasParameter);

        // Assert
        Assert.False(redundant);
    }
}