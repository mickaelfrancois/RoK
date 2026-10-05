using System.Runtime.InteropServices;
using Rok.Services.Diagnostics;

namespace Rok.PresentationTests.Services.Diagnostics;

public class CrashContextBuilderTests
{
    private static readonly COMException _exception = new("boom", unchecked((int)0x8000FFFF));

    [Fact(DisplayName = "build_keeps_the_four_legacy_keys")]
    public void Build_KeepsTheFourLegacyKeys()
    {
        // Arrange
        NavigationSnapshot snapshot = new("WelcomePage", "unknownPrevious", "AlbumsPage", NavigationPhase.Navigating, 12);

        // Act
        Dictionary<string, object> properties = CrashContextBuilder.Build(_exception, snapshot, []);

        // Assert
        Assert.Equal("WelcomePage", properties["currentPage"]);
        Assert.Equal("unknownPrevious", properties["previousPage"]);
        Assert.Equal(typeof(COMException).FullName, properties["exceptionType"]);
        Assert.Equal(unchecked((int)0x8000FFFF), properties["hresult"]);
    }

    [Fact(DisplayName = "build_adds_navigation_phase_pending_page_ms_in_phase_and_breadcrumbs")]
    public void Build_AddsNavigationPhasePendingPageMsInPhaseAndBreadcrumbs()
    {
        // Arrange
        NavigationSnapshot snapshot = new("WelcomePage", null, "AlbumsPage", NavigationPhase.Navigating, 12);
        string[] breadcrumbs = ["-30 welcome.reveal n=4", "-5 nav.navigating AlbumsPage"];

        // Act
        Dictionary<string, object> properties = CrashContextBuilder.Build(_exception, snapshot, breadcrumbs);

        // Assert
        Assert.Equal("navigating", properties["navigationPhase"]);
        Assert.Equal("AlbumsPage", properties["pendingPage"]);
        Assert.Equal(12L, properties["msInPhase"]);
        Assert.Equal("-30 welcome.reveal n=4 | -5 nav.navigating AlbumsPage", properties["breadcrumbs"]);
    }

    [Fact(DisplayName = "build_falls_back_to_unknown_without_snapshot")]
    public void Build_FallsBackToUnknownWithoutSnapshot()
    {
        // Arrange & Act
        Dictionary<string, object> properties = CrashContextBuilder.Build(_exception, null, []);

        // Assert
        Assert.Equal("unknown", properties["currentPage"]);
        Assert.Equal("unknown", properties["previousPage"]);
        Assert.Equal("unknown", properties["navigationPhase"]);
        Assert.Equal("unknown", properties["pendingPage"]);
        Assert.Equal("unknown", properties["breadcrumbs"]);
    }

    [Fact(DisplayName = "build_payload_stays_under_size_cap")]
    public void Build_PayloadStaysUnderSizeCap()
    {
        // Arrange
        NavigationSnapshot snapshot = new("WelcomePage", "AlbumsPage", "ListeningPage", NavigationPhase.Navigated, 99999);
        CrashBreadcrumbs breadcrumbs = new(TimeProvider.System);

        for (int i = 0; i < 40; i++)
            breadcrumbs.Add("category", new string('x', 200));

        // Act
        Dictionary<string, object> properties = CrashContextBuilder.Build(_exception, snapshot, breadcrumbs.Snapshot());
        string json = System.Text.Json.JsonSerializer.Serialize(properties);

        // Assert
        Assert.True(json.Length <= 2048, $"payload is {json.Length} characters");
    }
}