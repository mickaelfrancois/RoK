using Rok.Application.Features.ListeningEvents;
using Rok.ViewModels.Common;

namespace Rok.PresentationTests.ViewModels.Common;

public class ListeningStatsViewModelTests
{
    private static ListeningStatsDto BuildStats(int listenCount = 5, long durationSeconds = 3600)
    {
        return new ListeningStatsDto
        {
            CompletedListenCount = listenCount,
            TotalDurationPlayedSeconds = durationSeconds,
            FirstListenedAt = new DateTime(2023, 3, 10, 12, 0, 0, DateTimeKind.Utc),
            LastListenedAt = new DateTime(2026, 4, 5, 21, 0, 0, DateTimeKind.Utc),
            PeakHour = 18,
            MonthlyListens =
            [
                new MonthlyListenCountDto { Year = 2026, Month = 3, Count = 2 },
                new MonthlyListenCountDto { Year = 2026, Month = 4, Count = 4 }
            ]
        };
    }

    [Fact(DisplayName = "SetStats should expose formatted values when the album was listened")]
    public void SetStats_ShouldExposeFormattedValues_WhenListened()
    {
        // Arrange
        ListeningStatsViewModel sut = new();

        // Act
        sut.SetStats(BuildStats(listenCount: 47, durationSeconds: 7320));

        // Assert
        Assert.True(sut.ShowStats);
        Assert.False(sut.ShowNeverListened);
        Assert.Equal("47", sut.ListenCountValue);
        Assert.Equal("2 h 02 min", sut.DurationValue);
        Assert.Contains("2023", sut.SinceValue);
        Assert.Equal("18h - 21h", sut.PeakHourValue);
    }

    [Fact(DisplayName = "SetStats should show never listened state when there is no completed listen")]
    public void SetStats_ShouldShowNeverListened_WhenNoCompletedListen()
    {
        // Arrange
        ListeningStatsViewModel sut = new();
        ListeningStatsDto stats = new();

        // Act
        sut.SetStats(stats);

        // Assert
        Assert.False(sut.ShowStats);
        Assert.True(sut.ShowNeverListened);
    }

    [Fact(DisplayName = "SetStats should format durations under one hour in minutes")]
    public void SetStats_ShouldFormatShortDurations_InMinutes()
    {
        // Arrange
        ListeningStatsViewModel sut = new();

        // Act
        sut.SetStats(BuildStats(durationSeconds: 2700));

        // Assert
        Assert.Equal("45 min", sut.DurationValue);
    }

    [Fact(DisplayName = "SetStats should scale monthly bars relative to the most active month")]
    public void SetStats_ShouldScaleMonthlyBars_RelativeToMostActiveMonth()
    {
        // Arrange
        ListeningStatsViewModel sut = new();
        ListeningStatsDto stats = BuildStats();
        stats.MonthlyListens =
        [
            new MonthlyListenCountDto { Year = 2026, Month = 2, Count = 0 },
            new MonthlyListenCountDto { Year = 2026, Month = 3, Count = 2 },
            new MonthlyListenCountDto { Year = 2026, Month = 4, Count = 4 }
        ];

        // Act
        sut.SetStats(stats);

        // Assert
        Assert.Equal(3, sut.MonthlyBars.Count);
        Assert.Equal(2, sut.MonthlyBars[0].Height);
        Assert.Equal(20, sut.MonthlyBars[1].Height);
        Assert.Equal(40, sut.MonthlyBars[2].Height);
    }

    [Fact(DisplayName = "SetStats should include the listen count in each bar tooltip")]
    public void SetStats_ShouldIncludeListenCount_InBarTooltips()
    {
        // Arrange
        ListeningStatsViewModel sut = new();

        // Act
        sut.SetStats(BuildStats());

        // Assert
        Assert.EndsWith(": 4", sut.MonthlyBars[1].Tooltip);
        Assert.Contains("2026", sut.MonthlyBars[1].Tooltip);
    }

    [Fact(DisplayName = "SetProgression should expose counts and label")]
    public void SetProgression_ShouldExposeCountsAndLabel()
    {
        // Arrange
        ListeningStatsViewModel sut = new();

        // Act
        sut.SetProgression(12, 14);

        // Assert
        Assert.True(sut.ShowProgression);
        Assert.Equal(12, sut.ListenedItemCount);
        Assert.Equal(14, sut.TotalItemCount);
        Assert.Equal("12/14", sut.ProgressionLabel);
    }

    [Fact(DisplayName = "SetProgression should stay hidden when the album has no track")]
    public void SetProgression_ShouldStayHidden_WhenNoTrack()
    {
        // Arrange
        ListeningStatsViewModel sut = new();

        // Act
        sut.SetProgression(0, 0);

        // Assert
        Assert.False(sut.ShowProgression);
    }
}
