using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Rok.Logic.ViewModels.Player;
using System.Threading;

namespace Rok.Commons;

public sealed partial class FullScreenControl : UserControl
{
    public PlayerViewModel PlayerViewModel { get; set; }

    private const int BackdropRotationDelaySeconds = 15;
    private int _trackAnimationIndex = 1;
    private readonly int _maxTrackAnimation = 3;

    private Storyboard? _kenBurnsStoryboard;
    private Storyboard? _backdropFadeIn;
    private Storyboard? _backdropFadeOut;
    private Storyboard? _coverEntrance;
    private DispatcherTimer? _backdropTimer;
    private List<string> _backdrops = new();
    private int _currentBackdropIndex;
    private readonly object _backdropsLock = new();
    private BackdropPicture? _backdropService;
    private readonly ILogger<FullScreenControl> _logger = App.ServiceProvider.GetRequiredService<ILogger<FullScreenControl>>();

    private readonly object _trackChangeLock = new();
    private CancellationTokenSource? _trackChangeCts;


    public FullScreenControl()
    {
        InitializeComponent();

        PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
        DataContext = PlayerViewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }


    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ListBox? list = sender as ListBox;

        if (list != null && e.AddedItems.Count > 0)
            list.ScrollIntoView(e.AddedItems[0]);
    }

    public async Task TrackChangedAsync(TrackDto newTrack, TrackDto? previousTrack)
    {
        _logger.LogDebug("Track changed - updating full screen view.");

        CancellationToken ct;
        lock (_trackChangeLock)
        {
            _trackChangeCts?.Cancel();
            _trackChangeCts?.Dispose();
            _trackChangeCts = new CancellationTokenSource();
            ct = _trackChangeCts.Token;
        }

        try
        {
            _backdropTimer?.Stop();
            _trackAnimationIndex++;

            if (_trackAnimationIndex > _maxTrackAnimation)
                _trackAnimationIndex = 1;

            switch (_trackAnimationIndex)
            {
                case 1:
                    changeTrackAnimation1?.Storyboard.Begin();
                    break;

                case 2:
                    changeTrackAnimation2?.Storyboard.Begin();
                    break;

                case 3:
                    changeTrackAnimation3?.Storyboard.Begin();
                    break;
            }

            if (newTrack.ArtistId != previousTrack?.ArtistId)
            {
                await LoadBackdropsForCurrentArtistAsync(newTrack.ArtistName, ct);
                if (ct.IsCancellationRequested)
                    return;

                _coverEntrance?.Begin();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling track change.");
        }

        _backdropTimer?.Start();
    }


    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _kenBurnsStoryboard = (Storyboard)Resources["KenBurnsAnimation"];
        _backdropFadeIn = (Storyboard)Resources["BackdropFadeIn"];
        _backdropFadeOut = (Storyboard)Resources["BackdropFadeOut"];
        _coverEntrance = (Storyboard)Resources["CoverEntranceAnimation"];

        _backdropService = App.ServiceProvider.GetRequiredService<BackdropPicture>();

        _backdropTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(BackdropRotationDelaySeconds)
        };
        _backdropTimer.Tick += OnBackdropTimerTick;
        _backdropTimer.Start();

        _kenBurnsStoryboard?.Begin();
        _coverEntrance?.Begin();
    }


    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _backdropTimer?.Stop();
        _backdropTimer = null;

        _kenBurnsStoryboard?.Stop();

        lock (_trackChangeLock)
        {
            _trackChangeCts?.Cancel();
            _trackChangeCts?.Dispose();
            _trackChangeCts = null;
        }
    }


    private async Task LoadBackdropsForCurrentArtistAsync(string artistName, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(artistName) || _backdropService == null || ct.IsCancellationRequested)
                return;

            List<string> newBackdrops = _backdropService.GetBackdrops(artistName);

            lock (_backdropsLock)
            {
                _backdrops = newBackdrops ?? new List<string>();
                _currentBackdropIndex = 0;
            }

            if (ct.IsCancellationRequested)
                return;

            if (_backdrops.Count > 0)
                await ShowNextBackdropAsync(ct);
            else
                await ShowGenericBackdropAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Operation was canceled, no action needed.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading backdrops for artist.");
        }
    }


    private async void OnBackdropTimerTick(object? sender, object e)
    {
        try
        {
            await ShowNextBackdropAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backdrop timer tick.");
        }
    }


    private async Task ShowNextBackdropAsync(CancellationToken ct = default)
    {
        string? backdropPath = null;

        lock (_backdropsLock)
        {
            if (_backdrops.Count == 0)
            {
                backdropPath = null;
            }
            else
            {
                if (_currentBackdropIndex < 0 || _currentBackdropIndex >= _backdrops.Count)
                    _currentBackdropIndex = 0;

                backdropPath = _backdrops[_currentBackdropIndex];

                _currentBackdropIndex = (_currentBackdropIndex + 1) % _backdrops.Count;
            }
        }

        if (backdropPath == null)
        {
            await ShowGenericBackdropAsync(ct);
            return;
        }

        try
        {
            _backdropFadeOut?.Begin();
            await Task.Delay(1000, ct);

            if (ct.IsCancellationRequested)
                return;

            BackdropImage.Source = new BitmapImage(new Uri($"file:///{backdropPath}"));

            _backdropFadeIn?.Begin();
            _kenBurnsStoryboard?.Stop();
            _kenBurnsStoryboard?.Begin();
        }
        catch (OperationCanceledException)
        {
            // Operation was canceled, no action needed.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing next backdrop.");
        }
    }

    private async Task ShowGenericBackdropAsync(CancellationToken ct = default)
    {
        try
        {
            if (_backdropService == null || ct.IsCancellationRequested)
                return;

            string genericBackdrop = _backdropService.GetRandomGenericBackdrop();

            _backdropFadeOut?.Begin();
            await Task.Delay(1000, ct);

            if (ct.IsCancellationRequested)
                return;

            BackdropImage.Source = new BitmapImage(new Uri(genericBackdrop));

            _backdropFadeIn?.Begin();
            _kenBurnsStoryboard?.Stop();
            _kenBurnsStoryboard?.Begin();
        }
        catch (OperationCanceledException)
        {
            // Operation was canceled, no action needed.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing generic backdrop.");
        }
    }
}