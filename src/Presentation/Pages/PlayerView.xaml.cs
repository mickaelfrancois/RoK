using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Rok.Application.Player;
using Rok.Commons;
using Rok.Services.Accessibility;
using Rok.ViewModels.Player;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rok.Pages;

public sealed partial class PlayerView : UserControl
{
    public PlayerViewModel ViewModel { get; set; }

    private const double TitleGap = 12;
    private const double TitleMinWidth = 100;

    private readonly DispatcherTimer _progressionTimer;
    private static readonly SolidColorBrush _haloWarningBrush = new(Colors.Red);
    private static readonly SolidColorBrush _haloDefaultBrush = new(Colors.White);

    private Storyboard? _sleepModeStoryboard;
    private bool _titleWidthUpdatePending;

    private readonly IDisposable _mediaChangedSubscription;

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

        IMessenger messenger = App.ServiceProvider.GetRequiredService<IMessenger>();
        _mediaChangedSubscription = messenger.Subscribe<MediaChangedMessage>((message) => MediaChanged(message));

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
        mainGrid.SizeChanged += OnMainGridSizeChanged;
        layoutStates.CurrentStateChanged += OnLayoutStateChanged;
        UpdateTitleMaxWidth();
        SetupSleepModeAnimation();

        KeyboardShortcutInstaller installer = App.ServiceProvider.GetRequiredService<KeyboardShortcutInstaller>();
        AttachPlaybackShortcuts(installer);
    }

    private void AttachPlaybackShortcuts(KeyboardShortcutInstaller installer)
    {
        playPauseButton.KeyboardAccelerators.Add(
            installer.Build(ShortcutId.PlayPause, OnPlayPauseAccelerator));

        KeyboardAccelerators.Add(installer.Build(ShortcutId.Next, OnNextAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.Previous, OnPreviousAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.VolumeUp, OnVolumeUpAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.VolumeDown, OnVolumeDownAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.Mute, OnMuteAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.Shuffle, OnShuffleAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.Repeat, OnRepeatAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.SeekForward, OnSeekForwardAccelerator));
        KeyboardAccelerators.Add(installer.Build(ShortcutId.SeekBackward, OnSeekBackwardAccelerator));
    }

    private bool IsTextInputFocused()
    {
        return FocusManager.GetFocusedElement(XamlRoot) is TextBox or AutoSuggestBox or PasswordBox;
    }

    private bool IsSliderFocused()
    {
        return FocusManager.GetFocusedElement(XamlRoot) is Slider;
    }

    private void OnPlayPauseAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            args.Handled = false;
            return;
        }

        args.Handled = true;

        if (ViewModel?.TogglePlayPauseCommand?.CanExecute(null) == true)
            ViewModel.TogglePlayPauseCommand.Execute(null);
    }

    private void OnNextAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel?.SkipNextCommand?.CanExecute(null) == true)
            ViewModel.SkipNextCommand.Execute(null);
    }

    private void OnPreviousAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel?.SkipPreviousCommand?.CanExecute(null) == true)
            ViewModel.SkipPreviousCommand.Execute(null);
    }

    private void OnVolumeUpAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel != null)
            ViewModel.Volume = Math.Min(100d, ViewModel.Volume + 5);
    }

    private void OnVolumeDownAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel != null)
            ViewModel.Volume = Math.Max(0d, ViewModel.Volume - 5);
    }

    private void OnVolumeWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint((UIElement)sender).Properties;

        if (properties.IsHorizontalMouseWheel)
            return;

        ViewModel?.ApplyWheelDelta(properties.MouseWheelDelta);
        e.Handled = true;
    }

    private void OnMuteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel?.MuteCommand?.CanExecute(null) == true)
            ViewModel.MuteCommand.Execute(null);
    }

    private void OnShuffleAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel?.ToggleShuffleCommand?.CanExecute(null) == true)
            ViewModel.ToggleShuffleCommand.Execute(null);
    }

    private void OnRepeatAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel?.CycleRepeatModeCommand?.CanExecute(null) == true)
            ViewModel.CycleRepeatModeCommand.Execute(null);
    }

    private void OnSeekForwardAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsSliderFocused())
        {
            args.Handled = false;
            return;
        }

        args.Handled = true;

        IPlayerService playerService = App.ServiceProvider.GetRequiredService<IPlayerService>();
        playerService.Position += 5;
    }

    private void OnSeekBackwardAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsSliderFocused())
        {
            args.Handled = false;
            return;
        }

        args.Handled = true;

        IPlayerService playerService = App.ServiceProvider.GetRequiredService<IPlayerService>();
        playerService.Position = Math.Max(0, playerService.Position - 5);
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        mainGrid.SizeChanged -= OnMainGridSizeChanged;
        layoutStates.CurrentStateChanged -= OnLayoutStateChanged;
        _sleepModeStoryboard?.Stop();
        _mediaChangedSubscription.Dispose();
    }

    private void OnMainGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTitleMaxWidth();
    }

    private void OnLayoutStateChanged(object sender, VisualStateChangedEventArgs e)
    {
        UpdateTitleMaxWidth();
        ScheduleTitleMaxWidthUpdate();
    }

    private void ScheduleTitleMaxWidthUpdate()
    {
        if (_titleWidthUpdatePending)
            return;

        _titleWidthUpdatePending = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _titleWidthUpdatePending = false;
            UpdateTitleMaxWidth();
        });
    }

    private void UpdateTitleMaxWidth()
    {
        if (currentTrack.Visibility != Visibility.Visible || currentTrack.ActualWidth <= 0 || playerButtons.ActualWidth <= 0)
            return;

        double titleLeft = trackTitle.TransformToVisual(mainGrid).TransformPoint(new Windows.Foundation.Point(0, 0)).X;
        double controlsLeft = playerButtons.TransformToVisual(mainGrid).TransformPoint(new Windows.Foundation.Point(0, 0)).X;
        double columnRight = currentTrack.TransformToVisual(mainGrid).TransformPoint(new Windows.Foundation.Point(currentTrack.ActualWidth, 0)).X;
        double scoreBlockWidth = trackScore.Visibility == Visibility.Visible
            ? trackScore.ActualWidth + trackScore.Margin.Left
            : 0;

        double maxWidth = PlayerTitleWidth.Compute(titleLeft, controlsLeft, columnRight, scoreBlockWidth, TitleGap, TitleMinWidth);

        if (double.IsInfinity(trackTitle.MaxWidth) || Math.Abs(trackTitle.MaxWidth - maxWidth) > 1)
            trackTitle.MaxWidth = maxWidth;
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
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(ViewModel.IsMusicMode)
            || e.PropertyName == nameof(ViewModel.IsRadioMode)
            || e.PropertyName == nameof(ViewModel.Mode))
        {
            ScheduleTitleMaxWidthUpdate();
        }

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
                ? _haloWarningBrush
                : _haloDefaultBrush;
        }
    }

    private void SleepFlyout_Opening(object sender, object e)
    {
        ViewModel.RefreshSleepTime();
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