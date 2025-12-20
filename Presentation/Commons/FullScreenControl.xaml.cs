using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Rok.Logic.ViewModels.Player;
using System.Threading;

namespace Rok.Commons;

public sealed partial class FullScreenControl : UserControl
{
    public PlayerViewModel PlayerViewModel { get; set; }

    private int _trackAnimationIndex = 1;
    private readonly int _maxTrackAnimation = 3;

    private Storyboard? _kenBurnsStoryboard;
    private Storyboard? _coverEntrance;
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
    }


    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _kenBurnsStoryboard = (Storyboard)Resources["KenBurnsAnimation"];
        _coverEntrance = (Storyboard)Resources["CoverEntranceAnimation"];

        _backdropService = App.ServiceProvider.GetRequiredService<BackdropPicture>();

        _kenBurnsStoryboard?.Begin();
        _coverEntrance?.Begin();
    }


    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _kenBurnsStoryboard?.Stop();
        _coverEntrance?.Stop();

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

            await ShowNextBackdropAsync(ct);
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
                if (_backdrops.Count == 1)
                {
                    backdropPath = _backdrops[0];

                }
                else
                {
                    _currentBackdropIndex = Random.Shared.Next(0, _backdrops.Count);
                    backdropPath = _backdrops[_currentBackdropIndex];
                }
            }
        }

        if (backdropPath == null && _backdropService != null && !ct.IsCancellationRequested)
            backdropPath = _backdropService.GetRandomGenericBackdrop();

        try
        {
            if (ct.IsCancellationRequested)
                return;

            BackdropImage.Source = new BitmapImage(new Uri($"file:///{backdropPath}"));

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
}