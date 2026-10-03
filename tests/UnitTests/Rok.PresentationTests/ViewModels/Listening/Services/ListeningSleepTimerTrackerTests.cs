using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Player;
using Rok.ViewModels.Listening.Services;

namespace Rok.PresentationTests.ViewModels.Listening.Services;

public class ListeningSleepTimerTrackerTests
{
    private readonly Mock<IPlayerSleepModeService> _sleepMode = new();
    private readonly FakeTimeProvider _time = new();
    private int _remainingSeconds;

    private ListeningSleepTimerTracker CreateTracker()
    {
        _sleepMode.Setup(s => s.GetRemainingSleepTimeInSeconds()).Returns(() => _remainingSeconds);

        return new ListeningSleepTimerTracker(_sleepMode.Object, _time);
    }

    private void AdvanceOneSecond()
    {
        _remainingSeconds--;
        _time.Advance(TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "tracker_reports_remaining_minutes_when_timer_starts")]
    public void Tracker_ReportsRemainingMinutes_WhenTimerStarts()
    {
        // Arrange
        using var tracker = CreateTracker();
        var raised = new List<int>();
        tracker.RemainingMinutesChanged += (_, minutes) => raised.Add(minutes);
        _remainingSeconds = 900;

        // Act
        _sleepMode.Raise(s => s.SleepTimerStateChanged += null, _sleepMode.Object, true);

        // Assert
        Assert.Equal(15, tracker.RemainingMinutes);
        Assert.Equal([15], raised);
    }

    [Fact(DisplayName = "tracker_raises_change_once_per_minute")]
    public void Tracker_RaisesChange_OncePerMinute()
    {
        // Arrange
        using var tracker = CreateTracker();
        _remainingSeconds = 900;
        _sleepMode.Raise(s => s.SleepTimerStateChanged += null, _sleepMode.Object, true);
        var raised = new List<int>();
        tracker.RemainingMinutesChanged += (_, minutes) => raised.Add(minutes);

        // Act
        for (var i = 0; i < 60; i++)
        {
            AdvanceOneSecond();
        }

        // Assert
        Assert.Equal([14], raised);
    }

    [Fact(DisplayName = "tracker_stops_ticking_when_timer_stops")]
    public void Tracker_StopsTicking_WhenTimerStops()
    {
        // Arrange
        using var tracker = CreateTracker();
        _remainingSeconds = 900;
        _sleepMode.Raise(s => s.SleepTimerStateChanged += null, _sleepMode.Object, true);
        var raised = new List<int>();
        tracker.RemainingMinutesChanged += (_, minutes) => raised.Add(minutes);

        // Act
        _sleepMode.Raise(s => s.SleepTimerStateChanged += null, _sleepMode.Object, false);
        for (var i = 0; i < 120; i++)
        {
            AdvanceOneSecond();
        }

        // Assert
        Assert.Equal(0, tracker.RemainingMinutes);
        Assert.Equal([0], raised);
    }

    [Fact(DisplayName = "tracker_dispose_unsubscribes_and_stops_timer")]
    public void Tracker_Dispose_UnsubscribesAndStopsTimer()
    {
        // Arrange
        var tracker = CreateTracker();
        _remainingSeconds = 900;
        _sleepMode.Raise(s => s.SleepTimerStateChanged += null, _sleepMode.Object, true);
        var raised = new List<int>();
        tracker.RemainingMinutesChanged += (_, minutes) => raised.Add(minutes);

        // Act
        using (tracker)
        {
        }


        _sleepMode.Raise(s => s.SleepTimerStateChanged += null, _sleepMode.Object, true);
        for (var i = 0; i < 120; i++)
        {
            AdvanceOneSecond();
        }

        // Assert
        Assert.Empty(raised);
    }
}