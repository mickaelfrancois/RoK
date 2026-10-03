using Rok.Application.Player;

namespace Rok.ViewModels.Listening.Services;

public sealed class ListeningSleepTimerTracker : IDisposable
{
    private static readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(1);

    private readonly IPlayerSleepModeService _sleepModeService;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private ITimer? _timer;
    private int _remainingMinutes;
    private bool _disposed;

    public ListeningSleepTimerTracker(IPlayerSleepModeService sleepModeService, TimeProvider timeProvider)
    {
        _sleepModeService = sleepModeService;
        _timeProvider = timeProvider;
        _sleepModeService.SleepTimerStateChanged += OnSleepTimerStateChanged;
    }

    /// <summary>Whole minutes left on the sleep timer, rounded up; 0 when the timer is inactive.</summary>
    public int RemainingMinutes
    {
        get
        {
            lock (_lock)
            {
                return _remainingMinutes;
            }
        }
    }

    /// <summary>Raised only when <see cref="RemainingMinutes"/> changes. May be raised from a thread pool thread.</summary>
    public event EventHandler<int>? RemainingMinutesChanged;

    private void OnSleepTimerStateChanged(object? sender, bool isActive)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _timer?.Dispose();
            _timer = null;

            if (isActive)
            {
                _timer = _timeProvider.CreateTimer(OnTick, null, _tickInterval, _tickInterval);
            }
        }

        Refresh(isActive);
    }

    private void OnTick(object? state)
    {
        Refresh(true);
    }

    private void Refresh(bool isActive)
    {
        int minutes;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            minutes = isActive
                ? Math.Max(1, (int)Math.Ceiling(_sleepModeService.GetRemainingSleepTimeInSeconds() / 60d))
                : 0;

            if (minutes == _remainingMinutes)
            {
                return;
            }

            _remainingMinutes = minutes;
        }

        RemainingMinutesChanged?.Invoke(this, minutes);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }

        _sleepModeService.SleepTimerStateChanged -= OnSleepTimerStateChanged;
    }
}