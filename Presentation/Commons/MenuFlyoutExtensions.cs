using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Playlists.PlaylistMenu;

namespace Rok.Commons;

public static class MenuFlyoutExtensions
{
    public static readonly DependencyProperty PlaylistMenuServiceProperty =
        DependencyProperty.RegisterAttached(
            "PlaylistMenuService",
            typeof(IPlaylistMenuService),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(null, OnPlaylistMenuServiceChanged));

    public static readonly DependencyProperty TrackIdProperty =
        DependencyProperty.RegisterAttached(
            "TrackId",
            typeof(long),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(0));

    public static void SetPlaylistMenuService(DependencyObject obj, IPlaylistMenuService value)
        => obj.SetValue(PlaylistMenuServiceProperty, value);

    public static IPlaylistMenuService GetPlaylistMenuService(DependencyObject obj)
        => (IPlaylistMenuService)obj.GetValue(PlaylistMenuServiceProperty);

    public static void SetTrackId(DependencyObject obj, long value)
        => obj.SetValue(TrackIdProperty, value);

    public static long GetTrackId(DependencyObject obj)
        => (long)obj.GetValue(TrackIdProperty);

    private static async void OnPlaylistMenuServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MenuFlyout menuFlyout && e.NewValue is IPlaylistMenuService service)
        {
            await UpdatePlaylistMenuItems(menuFlyout, service);
            service.PlaylistsChanged += async (_, _) =>
            {
                if (menuFlyout.DispatcherQueue.HasThreadAccess)
                {
                    await UpdatePlaylistMenuItems(menuFlyout, service);
                }
                else
                {
                    menuFlyout.DispatcherQueue.TryEnqueue(async () =>
                        await UpdatePlaylistMenuItems(menuFlyout, service));
                }
            };
        }
    }

    private static async Task UpdatePlaylistMenuItems(MenuFlyout menuFlyout, IPlaylistMenuService service)
    {
        ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

        MenuFlyoutSubItem? existingAddToItem = menuFlyout.Items.OfType<MenuFlyoutSubItem>()
             .FirstOrDefault(item => item.Tag?.ToString() == "AddToPlaylistSubMenu");

        if (existingAddToItem != null)
            menuFlyout.Items.Remove(existingAddToItem);

        IEnumerable<PlaylistMenuItem> playlists = await service.GetPlaylistMenuItemsAsync();

        if (!menuFlyout.Items.OfType<MenuFlyoutSeparator>().Any())
            menuFlyout.Items.Add(new MenuFlyoutSeparator());

        MenuFlyoutSubItem addToSubMenu = new()
        {
            Text = resourceLoader.GetString("MenuFlyout_AddTo_Text") ?? "Add to",
            Icon = new FontIcon { Glyph = "\uE710" }, // Icon "Add"
            Tag = "AddToPlaylistSubMenu"
        };

        if (playlists.Any())
        {
            foreach (PlaylistMenuItem playlist in playlists.OrderBy(c => c.Name))
            {
                MenuFlyoutItem menuItem = new()
                {
                    Text = playlist.Name,
                    Icon = new FontIcon { Glyph = playlist.Icon },
                    Tag = "PlaylistItem"
                };

                menuItem.Click += async (sender, _) =>
                {
                    long trackId = GetTrackId(menuFlyout);
                    await service.AddTrackToPlaylistAsync(playlist.Id, trackId);
                };

                addToSubMenu.Items.Add(menuItem);
            }

            if (addToSubMenu.Items.Count > 0)
                addToSubMenu.Items.Add(new MenuFlyoutSeparator());
        }

        MenuFlyoutItem newPlaylistItem = new()
        {
            Text = resourceLoader.GetString("MenuFlyout_NewPlaylist_Text") ?? "New playlist...",
            Icon = new FontIcon { Glyph = "\uE710" }, // Icon "Add"
            Tag = "NewPlaylistItem"
        };

        newPlaylistItem.Click += async (sender, _) =>
        {
            if (App.MainWindow?.Content?.XamlRoot != null)
            {
                string? playlistName = await ShowCreatePlaylistDialogAsync(App.MainWindow.Content.XamlRoot);
                if (!string.IsNullOrWhiteSpace(playlistName))
                {
                    long trackId = GetTrackId(menuFlyout);
                    await service.CreateNewPlaylistWithTrackAsync(playlistName, trackId);
                }
            }
        };

        addToSubMenu.Items.Add(newPlaylistItem);
        menuFlyout.Items.Add(addToSubMenu);
    }


    private static async Task<string?> ShowCreatePlaylistDialogAsync(XamlRoot xamlRoot)
    {
        ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

        TextBox textBox = new()
        {
            PlaceholderText = resourceLoader.GetString("NewPlaylistDialog_TextBox_PlaceholderText") ?? "Nom de la playlist",
            Margin = new Thickness(0, 10, 0, 0)
        };

        TextBlock descriptionText = new()
        {
            Text = resourceLoader.GetString("NewPlaylistDialog_Description_Text") ?? "Entrez le nom de votre nouvelle playlist :"
        };

        ContentDialog dialog = new()
        {
            Title = resourceLoader.GetString("NewPlaylistDialog_Title") ?? "Nouvelle playlist",
            Content = new StackPanel
            {
                Children = { descriptionText, textBox }
            },
            PrimaryButtonText = resourceLoader.GetString("NewPlaylistDialog_PrimaryButton") ?? "Créer",
            CloseButtonText = resourceLoader.GetString("NewPlaylistDialog_CloseButton") ?? "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        dialog.PrimaryButtonClick += (sender, args) =>
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                args.Cancel = true;
            }
        };

        ContentDialogResult result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary ? textBox.Text?.Trim() : null;
    }
}
