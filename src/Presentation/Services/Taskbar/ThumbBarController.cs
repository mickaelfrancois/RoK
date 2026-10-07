using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Services.PlayerCommand;

namespace Rok.Services.Taskbar;

/// <summary>Keeps the thumbnail toolbar in sync with the player and routes its clicks to the player commands.</summary>
public sealed class ThumbBarController : IDisposable
{
    private readonly IPlayerService _player;
    private readonly IPlayerCommandService _commands;
    private readonly IMessenger _messenger;
    private readonly IThumbBarHost _host;
    private readonly Action<Action> _dispatch;
    private readonly ILogger<ThumbBarController> _logger;
    private readonly List<IDisposable> _subscriptions = [];

    private ThumbBarState? _lastState;
    private bool _started;
    private bool _disposed;

    public ThumbBarController(
        IPlayerService player,
        IPlayerCommandService commands,
        IMessenger messenger,
        IThumbBarHost host,
        Action<Action> dispatch,
        ILogger<ThumbBarController> logger)
    {
        _player = player;
        _commands = commands;
        _messenger = messenger;
        _host = host;
        _dispatch = dispatch;
        _logger = logger;
    }

    /// <summary>Subscribes to the player messages and pushes the initial state to the host.</summary>
    public void Start()
    {
        if (_started || _disposed)
            return;

        _started = true;

        _subscriptions.Add(_messenger.Subscribe<MediaStateChanged>(_ => RequestRefresh()));
        _subscriptions.Add(_messenger.Subscribe<MediaChangedMessage>(_ => RequestRefresh()));
        _subscriptions.Add(_messenger.Subscribe<PlaylistChanged>(_ => RequestRefresh()));
        _subscriptions.Add(_messenger.Subscribe<RadioStationChanged>(_ => RequestRefresh()));
        _subscriptions.Add(_messenger.Subscribe<RepeatModeChanged>(_ => RequestRefresh()));
        _host.ButtonClicked += OnButtonClicked;

        RequestRefresh();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();
        _host.ButtonClicked -= OnButtonClicked;
        _host.Dispose();
    }

    private void RequestRefresh() => _dispatch(Refresh);

    private void Refresh()
    {
        if (_disposed)
            return;

        var state = ThumbBarStatePolicy.From(_player);

        if (state == _lastState)
            return;

        _lastState = state;
        _host.Apply(state);
    }

    private void OnButtonClicked(object? sender, ThumbBarButton button) => _dispatch(() => HandleClick(button));

    private void HandleClick(ThumbBarButton button)
    {
        if (_disposed)
            return;

        var state = ThumbBarStatePolicy.From(_player);

        try
        {
            switch (button)
            {
                case ThumbBarButton.Previous when state.IsPreviousEnabled:
                    _commands.Previous();
                    break;
                case ThumbBarButton.PlayPause when state.IsPlayPauseEnabled:
                    _commands.Toggle();
                    break;
                case ThumbBarButton.Next when state.IsNextEnabled:
                    _commands.Next();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Thumbnail toolbar command '{Button}' failed", button);
        }
    }
}