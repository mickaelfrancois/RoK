using System.Globalization;

namespace Rok.Commons;

public sealed partial class AnimatedNumberHelper(Action<string> textSetter, Func<double, string>? formatter = null, TimeSpan? duration = null) : IDisposable
{
    private double _currentValue;
    private double _targetValue;
    private double _startValue;
    private DateTimeOffset _animationStart;
    private DispatcherTimer? _timer;

    private readonly Action<string> _textSetter = textSetter;
    private readonly Func<double, string> _formatter = formatter ?? (v => v.ToString("N0", CultureInfo.CurrentCulture));
    private readonly TimeSpan _duration = duration ?? TimeSpan.FromMilliseconds(600);


    public void AnimateTo(double target)
    {
        Stop();

        _startValue = _currentValue;
        _targetValue = target;
        _animationStart = DateTimeOffset.UtcNow;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }


    private void OnTick(object? sender, object e)
    {
        double elapsed = (DateTimeOffset.UtcNow - _animationStart).TotalMilliseconds;
        double progress = Math.Min(elapsed / _duration.TotalMilliseconds, 1.0);
        double eased = 1 - Math.Pow(1 - progress, 3);

        _currentValue = _startValue + ((_targetValue - _startValue) * eased);
        _textSetter(_formatter(_currentValue));

        if (progress >= 1.0)
        {
            _currentValue = _targetValue;
            _textSetter(_formatter(_targetValue));
            Stop();
        }
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }
    }

    public void Dispose() => Stop();
}