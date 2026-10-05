using Microsoft.Extensions.Time.Testing;
using Rok.Services.Diagnostics;

namespace Rok.PresentationTests.Services.Diagnostics;

public class CrashBreadcrumbsTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact(DisplayName = "breadcrumbs_keep_only_the_last_twelve_entries_in_order")]
    public void Breadcrumbs_KeepOnlyTheLastTwelveEntriesInOrder()
    {
        // Arrange
        CrashBreadcrumbs breadcrumbs = new(_time);

        // Act
        for (int i = 1; i <= 15; i++)
            breadcrumbs.Add("nav", $"step{i}");

        IReadOnlyList<string> snapshot = breadcrumbs.Snapshot();

        // Assert
        Assert.Equal(12, snapshot.Count);
        Assert.EndsWith("step4", snapshot[0]);
        Assert.EndsWith("step15", snapshot[^1]);
    }

    [Fact(DisplayName = "breadcrumbs_truncate_long_entries")]
    public void Breadcrumbs_TruncateLongEntries()
    {
        // Arrange
        CrashBreadcrumbs breadcrumbs = new(_time);

        // Act
        breadcrumbs.Add("nav", new string('x', 500));

        // Assert
        Assert.Equal(CrashBreadcrumbs.MaxEntryLength, breadcrumbs.Snapshot()[0].Length);
    }

    [Fact(DisplayName = "breadcrumbs_report_relative_milliseconds")]
    public void Breadcrumbs_ReportRelativeMilliseconds()
    {
        // Arrange
        CrashBreadcrumbs breadcrumbs = new(_time);
        breadcrumbs.Add("nav", "first");
        _time.Advance(TimeSpan.FromMilliseconds(300));
        breadcrumbs.Add("nav", "second");
        _time.Advance(TimeSpan.FromMilliseconds(100));

        // Act
        IReadOnlyList<string> snapshot = breadcrumbs.Snapshot();

        // Assert
        Assert.Equal("-400 nav first", snapshot[0]);
        Assert.Equal("-100 nav second", snapshot[1]);
    }

    [Fact(DisplayName = "breadcrumbs_accept_concurrent_writers")]
    public void Breadcrumbs_AcceptConcurrentWriters()
    {
        // Arrange
        CrashBreadcrumbs breadcrumbs = new(_time);

        // Act
        Parallel.For(0, 1000, i => breadcrumbs.Add("nav", i.ToString()));

        // Assert
        Assert.Equal(CrashBreadcrumbs.Capacity, breadcrumbs.Snapshot().Count);
    }
}