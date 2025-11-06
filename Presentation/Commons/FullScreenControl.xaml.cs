using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Rok.Logic.ViewModels.Player;

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
    private List<string> _backdrops = [];
    private int _currentBackdropIndex;
    private string _lastArtistName = string.Empty;
    private BackdropPicture? _backdropService;
    private readonly ILogger<FullScreenControl> _logger = App.ServiceProvider.GetRequiredService<ILogger<FullScreenControl>>();



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


    public async Task TrackChangedAsync()
    {
        _logger.LogDebug("Track changed - updating full screen view.");

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

        string? artistName = PlayerViewModel?.CurrentTrack?.ArtistName;
        if (!string.IsNullOrEmpty(artistName) && artistName != _lastArtistName)
        {
            _lastArtistName = artistName;
            await LoadBackdropsForCurrentArtistAsync();

            _coverEntrance?.Begin();
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
    }


    private async Task LoadBackdropsForCurrentArtistAsync()
    {
        try
        {
            var artistName = PlayerViewModel?.CurrentArtist?.Artist.Name;

            if (string.IsNullOrEmpty(artistName) || _backdropService == null)
                return;

            _backdrops = _backdropService.GetBackdrops(artistName);
            _currentBackdropIndex = 0;

            if (_backdrops.Count > 0)
                await ShowNextBackdropAsync();
            else
                await ShowGenericBackdropAsync();
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


    private async Task ShowNextBackdropAsync()
    {
        if (_backdrops.Count == 0)
        {
            await ShowGenericBackdropAsync();
            return;
        }

        try
        {
            _backdropFadeOut?.Begin();
            await Task.Delay(1000);

            string backdropPath = _backdrops[_currentBackdropIndex];
            BackdropImage.Source = new BitmapImage(new Uri($"file:///{backdropPath}"));

            _currentBackdropIndex = (_currentBackdropIndex + 1) % _backdrops.Count;

            _backdropFadeIn?.Begin();
            _kenBurnsStoryboard?.Stop();
            _kenBurnsStoryboard?.Begin();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing next backdrop.");
        }
    }

    private async Task ShowGenericBackdropAsync()
    {
        try
        {
            if (_backdropService == null)
                return;

            string genericBackdrop = _backdropService.GetRandomGenericBackdrop();

            _backdropFadeOut?.Begin();
            await Task.Delay(1000);

            BackdropImage.Source = new BitmapImage(new Uri(genericBackdrop));

            _backdropFadeIn?.Begin();
            _kenBurnsStoryboard?.Stop();
            _kenBurnsStoryboard?.Begin();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing generic backdrop.");
        }
    }
}
