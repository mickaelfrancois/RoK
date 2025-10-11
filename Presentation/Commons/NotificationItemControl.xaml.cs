using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Rok.Commons;

public sealed partial class NotificationItemControl : UserControl, IDisposable
{
    private readonly Action<NotificationItemControl> _removeCallback;
    private DispatcherTimer? _hideTimer;

    public NotificationItemControl(ShowNotificationMessage message, Action<NotificationItemControl> removeCallback)
    {
        this.InitializeComponent();

        _removeCallback = removeCallback;

        notificationInfoBar.Severity = message.Type switch
        {
            NotificationType.Success => InfoBarSeverity.Success,
            NotificationType.Error => InfoBarSeverity.Error,
            NotificationType.Warning => InfoBarSeverity.Warning,
            NotificationType.Informational => InfoBarSeverity.Informational,
            _ => InfoBarSeverity.Informational
        };

        notificationInfoBar.Title = message.Title;
        notificationInfoBar.Message = message.Message;
        notificationInfoBar.IsOpen = true;
    }

    public void StartAutoHide()
    {
        _hideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };

        _hideTimer.Tick += (s, e) =>
        {
            _hideTimer.Stop();
            Hide();
        };

        _hideTimer.Start();
    }

    public void Hide()
    {
        _hideTimer?.Stop();

        DoubleAnimation fadeOut = new()
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(0.5)
        };

        Storyboard storyboard = new();
        Storyboard.SetTarget(fadeOut, this);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyboard.Children.Add(fadeOut);

        storyboard.Completed += (s, e) => _removeCallback(this);
        storyboard.Begin();
    }

    private void NotificationInfoBar_CloseButtonClick(InfoBar sender, object args)
    {
        Hide();
    }

    public void Dispose()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }
}