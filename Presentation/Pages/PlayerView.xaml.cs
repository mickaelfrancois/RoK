using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Rok.Application.Player;
using Rok.ViewModels.Player;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rok.Pages;

public sealed partial class PlayerView : UserControl
{
    public PlayerViewModel ViewModel { get; set; }

    private readonly DispatcherTimer _progressionTimer;
    private Storyboard? _sleepModeStoryboard;

    public PlayerView()
    {
        this.InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();

        DataContext = ViewModel;

        _progressionTimer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        InitProgressionTimer();

        Messenger.Subscribe<MediaChangedMessage>((message) => MediaChanged(message));

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void InitProgressionTimer()
    {
        _progressionTimer.Tick += ProgresionTimer_Tick;
        _progressionTimer.Start();
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SetupSleepModeAnimation();
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _sleepModeStoryboard?.Stop();
    }

    private void SetupSleepModeAnimation()
    {
        _sleepModeStoryboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true
        };

        DoubleAnimation opacityAnimation = new()
        {
            From = 0.0,
            To = 0.4,
            Duration = new Duration(TimeSpan.FromMilliseconds(1500)),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(opacityAnimation, sleepModeHalo);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        _sleepModeStoryboard.Children.Add(opacityAnimation);

        if (ViewModel.IsSleepModeActive)
            _sleepModeStoryboard.Begin();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsSleepModeActive))
        {
            if (ViewModel.IsSleepModeActive)
                _sleepModeStoryboard?.Begin();
            else
                _sleepModeStoryboard?.Stop();
        }
        else if (e.PropertyName == nameof(ViewModel.RemainingSleepTime))
        {
            sleepModeHalo.Background = ViewModel.RemainingSleepTime < 60
                ? new SolidColorBrush(Colors.Red)
                : new SolidColorBrush(Colors.White);
        }
    }

    public Symbol GetIconFromPlaybackState(EPlaybackState state)
    {
        if (state == EPlaybackState.Playing)
            return Symbol.Pause;
        else
            return Symbol.Play;
    }

    private void Slider_ManipulationStarted(object sender, Microsoft.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
    {
        if (_progressionTimer.IsEnabled)
            _progressionTimer.Stop();
    }


    private void Slider_PointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Slider slider = (Slider)sender;

        slider.Tag = slider.Value;

        ViewModel.SetPosition(slider.Value);

        _progressionTimer.Start();
    }


    private void ProgresionTimer_Tick(object? sender, object? e)
    {
        progressSlider.Tag = progressSlider.Value = ViewModel.ListenDuration.TotalSeconds;
    }

    private void MediaChanged(MediaChangedMessage message)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await TrackChangedAsync(message.NewTrack, message.PreviousTrack);
        });
    }


    public async Task TrackChangedAsync(TrackDto newTrack, TrackDto? previousTrack)
    {
        if (previousTrack == null)
            return;

        if (previousTrack.ArtistId != newTrack.ArtistId)
            this.changeTrackArtistAnimation?.Storyboard.Begin();

        if (previousTrack.Id != newTrack.Id)
            this.changeTrackTitleAnimation?.Storyboard.Begin();

        if (previousTrack.AlbumId != newTrack.AlbumId)
            this.changeTrackAlbumAnimation?.Storyboard.Begin();

        if (previousTrack.Score != newTrack.Score)
            this.changeTrackScoreAnimation?.Storyboard.Begin();
    }
}
