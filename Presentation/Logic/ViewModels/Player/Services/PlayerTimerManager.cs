namespace Rok.Logic.ViewModels.Player.Services;

public class PlayerTimerManager : IDisposable
{
    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _lyricTimer;
    private readonly DispatcherTimer _backdropTimer;
    private bool _disposed;

    public event EventHandler? UpdateTick;
    public event EventHandler? LyricTick;
    public event EventHandler? BackdropTick;

    public PlayerTimerManager()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (s, e) => UpdateTick?.Invoke(s, EventArgs.Empty);

        _lyricTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(0.2)
        };
        _lyricTimer.Tick += (s, e) => LyricTick?.Invoke(s, EventArgs.Empty);

        _backdropTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(45)
        };
        _backdropTimer.Tick += (s, e) => BackdropTick?.Invoke(s, EventArgs.Empty);
    }

    public void Start()
    {
        _updateTimer.Start();
        _lyricTimer.Start();
        _backdropTimer.Start();
    }

    public void StopBackdropTimer()
    {
        _backdropTimer.Stop();
    }

    public void StartBackdropTimer()
    {
        _backdropTimer.Start();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _updateTimer?.Stop();
            _lyricTimer?.Stop();
            _backdropTimer?.Stop();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}