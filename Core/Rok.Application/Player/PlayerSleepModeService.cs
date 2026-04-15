namespace Rok.Application.Player;

public sealed class PlayerSleepModeService : IDisposable, IPlayerSleepModeService
{
    private readonly IPlayerService _player;
    private readonly TimeProvider _timeProvider;
    private ITimer? _sleepTimer;
    private TimeSpan _sleepRemaining;
    private bool _isActive;

    public bool IsSleepTimerActive => _isActive;
    public event EventHandler<bool>? SleepTimerStateChanged;
    public int GetRemainingSleepTimeInSeconds() => (int)_sleepRemaining.TotalSeconds;


    public PlayerSleepModeService(IPlayerService player, TimeProvider? timeProvider = null)
    {
        _player = player;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void StartSleepTimer(int minutes)
    {
        if (minutes <= 0)
            return;

        StopSleepTimer();

        _sleepRemaining = TimeSpan.FromMinutes(minutes);
        _isActive = true;
        _sleepTimer = _timeProvider.CreateTimer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        SleepTimerStateChanged?.Invoke(this, true);
    }

    public void StopSleepTimer()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _sleepTimer?.Dispose();
        _sleepTimer = null;
        SleepTimerStateChanged?.Invoke(this, false);
    }


    private void OnTimerTick(object? state)
    {
        _sleepRemaining -= TimeSpan.FromSeconds(1);

        if (_sleepRemaining <= TimeSpan.Zero)
        {
            StopSleepTimer();
            _player.Stop(true);
        }
    }

    public void Dispose()
    {
        StopSleepTimer();
    }
}