using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Player;
using Rok.Logic.ViewModels.Player;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rok.Pages;

public sealed partial class PlayerView : UserControl
{
    public PlayerViewModel ViewModel { get; set; }

    private readonly DispatcherTimer _progressionTimer;


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
    }

    private void InitProgressionTimer()
    {
        _progressionTimer.Tick += ProgresionTimer_Tick;
        _progressionTimer.Start();
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
