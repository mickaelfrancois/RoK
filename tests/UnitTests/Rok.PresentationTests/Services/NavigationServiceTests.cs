using Moq;
using Rok.Application.Interfaces;
using Rok.Pages;
using Rok.Services;
using Rok.Services.Diagnostics;

namespace Rok.PresentationTests.Services;

public class NavigationServiceTests
{
    private readonly Mock<ITelemetryClient> _telemetry = new();
    private readonly NavigationTrail _trail = new(TimeProvider.System);
    private readonly CrashBreadcrumbs _breadcrumbs = new(TimeProvider.System);

    private NavigationService CreateService() => new(_telemetry.Object, _trail, _breadcrumbs);

    [Fact(DisplayName = "navigate_to_listening_is_skipped_when_listening_is_already_displayed")]
    public void NavigateToListening_IsSkipped_WhenListeningIsAlreadyDisplayed()
    {
        // Arrange
        NavigationService service = CreateService();
        _trail.OnNavigated(nameof(ListeningPage));

        // Act
        service.NavigateToListening();

        // Assert
        _telemetry.Verify(t => t.CaptureScreenAsync(It.IsAny<string>()), Times.Never);
        Assert.Contains(_breadcrumbs.Snapshot(), entry => entry.Contains("nav.skip ListeningPage"));
    }

    [Fact(DisplayName = "navigate_to_is_skipped_for_listening_page_without_parameter")]
    public void NavigateTo_IsSkipped_ForListeningPageWithoutParameter()
    {
        // Arrange
        NavigationService service = CreateService();
        _trail.OnNavigated(nameof(ListeningPage));

        // Act
        service.NavigateTo(typeof(ListeningPage));

        // Assert
        _telemetry.Verify(t => t.CaptureScreenAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "current_and_previous_page_names_come_from_the_trail")]
    public void CurrentAndPreviousPageNames_ComeFromTheTrail()
    {
        // Arrange
        NavigationService service = CreateService();

        // Act
        _trail.OnNavigated("WelcomePage");
        _trail.OnNavigated("AlbumsPage");

        // Assert
        Assert.Equal("AlbumsPage", service.CurrentPageName);
        Assert.Equal("WelcomePage", service.PreviousPageName);
    }
}