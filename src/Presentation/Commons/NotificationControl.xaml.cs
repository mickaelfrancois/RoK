using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

public sealed partial class NotificationControl : UserControl
{
    private const int MaxNotifications = 5;
    private readonly List<NotificationItemControl> _notifications = new();
    private readonly IDisposable _notificationSubscription;

    public NotificationControl()
    {
        this.InitializeComponent();

        this.Visibility = Visibility.Collapsed;
        IMessenger messenger = App.ServiceProvider.GetRequiredService<IMessenger>();
        _notificationSubscription = messenger.Subscribe<ShowNotificationMessage>(ShowNotification);
        this.Unloaded += (_, _) => _notificationSubscription.Dispose();
    }


    public void ShowNotification(ShowNotificationMessage message)
    {
        NotificationItemControl notification = new(message, RemoveNotification);

        _notifications.Insert(0, notification);
        notificationsPanel.Children.Insert(0, notification);

        while (_notifications.Count > MaxNotifications)
        {
            NotificationItemControl oldestNotification = _notifications[_notifications.Count - 1];
            RemoveNotification(oldestNotification);
        }

        if (this.Visibility == Visibility.Collapsed)
            this.Visibility = Visibility.Visible;

        notification.StartAutoHide();
    }

    private void RemoveNotification(NotificationItemControl notification)
    {
        if (_notifications.Contains(notification))
        {
            _notifications.Remove(notification);
            notificationsPanel.Children.Remove(notification);
            notification.Dispose();
        }

        if (_notifications.Count == 0)
            this.Visibility = Visibility.Collapsed;
    }
}
