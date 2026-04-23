using System.Runtime.CompilerServices;
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
            new PropertyMetadata(0L, OnIdChanged));

    public static readonly DependencyProperty AlbumIdProperty =
        DependencyProperty.RegisterAttached(
        "AlbumId",
        typeof(long),
        typeof(MenuFlyoutExtensions),
        new PropertyMetadata(0L, OnIdChanged));

    public static readonly DependencyProperty ArtistIdProperty =
        DependencyProperty.RegisterAttached(
        "ArtistId",
        typeof(long),
        typeof(MenuFlyoutExtensions),
        new PropertyMetadata(0L, OnIdChanged));

    public static readonly DependencyProperty FlattenPlaylistMenuProperty =
        DependencyProperty.RegisterAttached(
            "FlattenPlaylistMenu",
            typeof(bool),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(false, OnIdChanged));

    private static readonly DependencyProperty MenuItemClickHandlerProperty =
        DependencyProperty.RegisterAttached(
            "MenuItemClickHandler",
            typeof(RoutedEventHandler),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(null));

    private static readonly DependencyProperty MenuItemPlaylistIdProperty =
        DependencyProperty.RegisterAttached(
            "MenuItemPlaylistId",
            typeof(long),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(0L));

    private static readonly DependencyProperty MenuItemServiceWeakRefProperty =
        DependencyProperty.RegisterAttached(
            "MenuItemServiceWeakRef",
            typeof(object),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(null));

    private static readonly ConditionalWeakTable<IPlaylistMenuService, List<WeakReference<MenuFlyout>>> s_serviceMenus = new();
    private static readonly object s_lock = new();

    public static void SetPlaylistMenuService(DependencyObject obj, IPlaylistMenuService value)
        => obj.SetValue(PlaylistMenuServiceProperty, value);

    public static IPlaylistMenuService GetPlaylistMenuService(DependencyObject obj)
        => (IPlaylistMenuService)obj.GetValue(PlaylistMenuServiceProperty);

    public static void SetTrackId(DependencyObject obj, long value)
        => obj.SetValue(TrackIdProperty, value);

    public static long GetTrackId(DependencyObject obj)
        => (long)obj.GetValue(TrackIdProperty);

    public static void SetAlbumId(DependencyObject obj, long value)
    => obj.SetValue(AlbumIdProperty, value);

    public static long GetAlbumId(DependencyObject obj)
        => (long)obj.GetValue(AlbumIdProperty);

    public static void SetArtistId(DependencyObject obj, long value)
        => obj.SetValue(ArtistIdProperty, value);

    public static long GetArtistId(DependencyObject obj)
        => (long)obj.GetValue(ArtistIdProperty);

    public static void SetFlattenPlaylistMenu(DependencyObject obj, bool value)
        => obj.SetValue(FlattenPlaylistMenuProperty, value);

    public static bool GetFlattenPlaylistMenu(DependencyObject obj)
        => (bool)obj.GetValue(FlattenPlaylistMenuProperty);

    private static void SetMenuItemClickHandler(DependencyObject obj, RoutedEventHandler? handler)
        => obj.SetValue(MenuItemClickHandlerProperty, handler);

    private static RoutedEventHandler? GetMenuItemClickHandler(DependencyObject obj)
        => (RoutedEventHandler?)obj.GetValue(MenuItemClickHandlerProperty);

    private static void SetMenuItemPlaylistId(DependencyObject obj, long id)
        => obj.SetValue(MenuItemPlaylistIdProperty, id);

    private static long GetMenuItemPlaylistId(DependencyObject obj)
        => (long)obj.GetValue(MenuItemPlaylistIdProperty);

    private static void SetMenuItemServiceWeakRef(DependencyObject obj, object? wr)
        => obj.SetValue(MenuItemServiceWeakRefProperty, wr);

    private static object? GetMenuItemServiceWeakRef(DependencyObject obj)
        => obj.GetValue(MenuItemServiceWeakRefProperty);

    private static async void OnPlaylistMenuServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MenuFlyout menuFlyout)
            return;

        if (e.OldValue is IPlaylistMenuService oldService)
        {
            RemoveMenuFlyoutForService(oldService, menuFlyout);
            CleanupExistingSubmenuClickHandlers(menuFlyout);
        }

        if (e.NewValue is IPlaylistMenuService newService)
        {
            AddMenuFlyoutForService(newService, menuFlyout);
            await UpdatePlaylistMenuItems(menuFlyout, newService);
        }
    }

    private static void OnIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MenuFlyout menuFlyout && GetPlaylistMenuService(menuFlyout) is { } service)
        {
            if (menuFlyout.DispatcherQueue.HasThreadAccess)
                _ = UpdatePlaylistMenuItems(menuFlyout, service);
            else
                menuFlyout.DispatcherQueue.TryEnqueue(() => _ = UpdatePlaylistMenuItems(menuFlyout, service));
        }
    }

    private static void AddMenuFlyoutForService(IPlaylistMenuService service, MenuFlyout menuFlyout)
    {
        lock (s_lock)
        {
            if (!s_serviceMenus.TryGetValue(service, out List<WeakReference<MenuFlyout>>? list))
            {
                list = new List<WeakReference<MenuFlyout>>();
                s_serviceMenus.Add(service, list);

                service.PlaylistsChanged += Service_PlaylistsChanged;
            }

            if (!list.Any(wr => wr.TryGetTarget(out MenuFlyout? mf) && ReferenceEquals(mf, menuFlyout)))
                list.Add(new WeakReference<MenuFlyout>(menuFlyout));
        }
    }

    private static void RemoveMenuFlyoutForService(IPlaylistMenuService service, MenuFlyout menuFlyout)
    {
        lock (s_lock)
        {
            if (s_serviceMenus.TryGetValue(service, out List<WeakReference<MenuFlyout>>? list))
            {
                list.RemoveAll(wr => !wr.TryGetTarget(out MenuFlyout? mf) || ReferenceEquals(mf, menuFlyout));

                if (list.Count == 0)
                {
                    s_serviceMenus.Remove(service);

                    try
                    {
                        service.PlaylistsChanged -= Service_PlaylistsChanged;
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }
    }

    private static void Service_PlaylistsChanged(object? sender, EventArgs e)
    {
        if (sender is not IPlaylistMenuService service)
            return;

        List<WeakReference<MenuFlyout>>? list = null;
        lock (s_lock)
        {
            if (!s_serviceMenus.TryGetValue(service, out list))
                return;

            list = list.ToList();
        }

        foreach (WeakReference<MenuFlyout> wr in list)
        {
            if (wr.TryGetTarget(out MenuFlyout? mf))
            {
                if (mf.DispatcherQueue.HasThreadAccess)
                    _ = UpdatePlaylistMenuItems(mf, service);
                else
                    mf.DispatcherQueue.TryEnqueue(() => _ = UpdatePlaylistMenuItems(mf, service));
            }
        }
    }

    private static void CleanupExistingSubmenuClickHandlers(MenuFlyout menuFlyout)
    {
        MenuFlyoutSubItem? existingAddToItem = menuFlyout.Items.OfType<MenuFlyoutSubItem>()
             .FirstOrDefault(item => item.Tag?.ToString() == "AddToPlaylistSubMenu");

        if (existingAddToItem != null)
        {
            foreach (MenuFlyoutItem? mi in existingAddToItem.Items.OfType<MenuFlyoutItem>().ToList())
            {
                RoutedEventHandler? handler = GetMenuItemClickHandler(mi);
                if (handler != null)
                {
                    try { mi.Click -= handler; }
                    catch { /* Ignore */ }
                    SetMenuItemClickHandler(mi, null);
                }

                try { SetMenuItemServiceWeakRef(mi, null); }
                catch { /* Ignore */ }
                try { SetMenuItemPlaylistId(mi, 0); }
                catch { /* Ignore */ }
            }

            try { existingAddToItem.Items.Clear(); }
            catch { /* Ignore */ }
            try { if (menuFlyout.Items.Contains(existingAddToItem)) menuFlyout.Items.Remove(existingAddToItem); }
            catch { /* Ignore */ }
        }

        foreach (MenuFlyoutItem? mi in menuFlyout.Items.OfType<MenuFlyoutItem>()
                     .Where(item => item.Tag?.ToString() == "PlaylistItem" || item.Tag?.ToString() == "NewPlaylistItem")
                     .ToList())
        {
            RoutedEventHandler? handler = GetMenuItemClickHandler(mi);
            if (handler != null)
            {
                try { mi.Click -= handler; }
                catch { /* Ignore */ }
                SetMenuItemClickHandler(mi, null);
            }

            try { SetMenuItemServiceWeakRef(mi, null); }
            catch { /* Ignore */ }
            try { SetMenuItemPlaylistId(mi, 0); }
            catch { /* Ignore */ }

            try { if (menuFlyout.Items.Contains(mi)) menuFlyout.Items.Remove(mi); }
            catch { /* Ignore */ }
        }

        foreach (MenuFlyoutSeparator? sep in menuFlyout.Items.OfType<MenuFlyoutSeparator>()
                     .Where(s => s.Tag?.ToString()?.StartsWith("AddToPlaylist") == true)
                     .ToList())
        {
            try { if (menuFlyout.Items.Contains(sep)) menuFlyout.Items.Remove(sep); }
            catch { /* Ignore */ }
        }
    }

    private static async Task UpdatePlaylistMenuItems(MenuFlyout menuFlyout, IPlaylistMenuService service)
    {
        ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

        CleanupExistingSubmenuClickHandlers(menuFlyout);

        IEnumerable<PlaylistMenuItem> playlists = await service.GetPlaylistMenuItemsAsync();

        bool flatten = GetFlattenPlaylistMenu(menuFlyout);

        long trackId = GetTrackId(menuFlyout);
        long albumId = GetAlbumId(menuFlyout);
        long artistId = GetArtistId(menuFlyout);

        if (!menuFlyout.Items.OfType<MenuFlyoutSeparator>().Any())
            menuFlyout.Items.Add(new MenuFlyoutSeparator { Tag = "AddToPlaylistSeparator" });

        MenuFlyoutSubItem addToSubMenu = new()
        {
            Text = resourceLoader.GetString("MenuFlyout_AddTo_Text") ?? "Add to",
            Icon = new FontIcon { Glyph = "\uE710" },
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

                SetMenuItemPlaylistId(menuItem, playlist.Id);
                SetMenuItemServiceWeakRef(menuItem, new WeakReference<IPlaylistMenuService>(service));

                SetTrackId(menuItem, trackId);
                SetAlbumId(menuItem, albumId);
                SetArtistId(menuItem, artistId);

                RoutedEventHandler handler = MenuItemStaticClickHandlerAsync;
                menuItem.Click += handler;
                SetMenuItemClickHandler(menuItem, handler);

                if (flatten)
                    menuFlyout.Items.Add(menuItem);
                else
                    addToSubMenu.Items.Add(menuItem);
            }

            if (!flatten && addToSubMenu.Items.Count > 0)
                addToSubMenu.Items.Add(new MenuFlyoutSeparator { Tag = "AddToPlaylistInnerSeparator" });
        }


        MenuFlyoutItem newPlaylistItem = new()
        {
            Text = resourceLoader.GetString("MenuFlyout_NewPlaylist_Text") ?? "New playlist...",
            Icon = new FontIcon { Glyph = "\uE710" },
            Tag = "NewPlaylistItem"
        };

        SetMenuItemPlaylistId(newPlaylistItem, -1);
        SetMenuItemServiceWeakRef(newPlaylistItem, new WeakReference<IPlaylistMenuService>(service));

        SetTrackId(newPlaylistItem, trackId);
        SetAlbumId(newPlaylistItem, albumId);
        SetArtistId(newPlaylistItem, artistId);

        RoutedEventHandler newHandler = NewPlaylistStaticClickHandlerAsync;
        newPlaylistItem.Click += newHandler;
        SetMenuItemClickHandler(newPlaylistItem, newHandler);


        MenuFlyoutItem addToCurrentListeningItem = new()
        {
            Text = resourceLoader.GetString("MenuFlyout_CurrentListening_Text") ?? "Current Listening...",
            Icon = new FontIcon { Glyph = "\uE7F6" },
            Tag = "CurrentListeningItem"
        };

        SetMenuItemPlaylistId(addToCurrentListeningItem, -1);
        SetMenuItemServiceWeakRef(addToCurrentListeningItem, new WeakReference<IPlaylistMenuService>(service));

        SetTrackId(addToCurrentListeningItem, trackId);
        SetAlbumId(addToCurrentListeningItem, albumId);
        SetArtistId(addToCurrentListeningItem, artistId);

        RoutedEventHandler addToCurrentListeningHandler = AddToCurrentListeningStaticClickHandlerAsync;
        addToCurrentListeningItem.Click += addToCurrentListeningHandler;
        SetMenuItemClickHandler(addToCurrentListeningItem, addToCurrentListeningHandler);



        if (flatten)
        {
            if (playlists.Any())
                menuFlyout.Items.Add(new MenuFlyoutSeparator { Tag = "AddToPlaylistInnerSeparator" });

            menuFlyout.Items.Add(newPlaylistItem);
            menuFlyout.Items.Add(addToCurrentListeningItem);
        }
        else
        {
            addToSubMenu.Items.Add(newPlaylistItem);
            addToSubMenu.Items.Add(addToCurrentListeningItem);
            menuFlyout.Items.Add(addToSubMenu);
        }
    }

    private static async void MenuItemStaticClickHandlerAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem mi)
            return;

        object? wr = GetMenuItemServiceWeakRef(mi);
        long playlistId = GetMenuItemPlaylistId(mi);
        long trackId = GetTrackId(mi);
        long albumId = GetAlbumId(mi);
        long artistId = GetArtistId(mi);

        if (wr is WeakReference<IPlaylistMenuService> weak && weak.TryGetTarget(out IPlaylistMenuService? service))
        {
            try
            {
                if (playlistId > 0)
                {
                    if (trackId > 0)
                        await service.AddTrackToPlaylistAsync(playlistId, trackId);
                    else if (albumId > 0)
                        await service.AddAlbumToPlaylistAsync(playlistId, albumId);
                    else if (artistId > 0)
                        await service.AddArtistToPlaylistAsync(playlistId, artistId);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    private static async void NewPlaylistStaticClickHandlerAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem mi)
            return;

        object? wr = GetMenuItemServiceWeakRef(mi);
        long trackId = GetTrackId(mi);
        long albumId = GetAlbumId(mi);
        long artistId = GetArtistId(mi);

        if (wr is WeakReference<IPlaylistMenuService> weak && weak.TryGetTarget(out IPlaylistMenuService? service))
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot != null)
                {
                    string? playlistName = await ShowCreatePlaylistDialogAsync(App.MainWindow.Content.XamlRoot);
                    if (!string.IsNullOrWhiteSpace(playlistName))
                    {
                        if (trackId > 0)
                            await service.CreateNewPlaylistWithTrackAsync(playlistName, trackId);
                        else if (albumId > 0)
                            await service.CreateNewPlaylistWithAlbumAsync(playlistName, albumId);
                        else if (artistId > 0)
                            await service.CreateNewPlaylistWithArtistAsync(playlistName, artistId);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }


    private static async void AddToCurrentListeningStaticClickHandlerAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem mi)
            return;

        object? wr = GetMenuItemServiceWeakRef(mi);
        long trackId = GetTrackId(mi);
        long albumId = GetAlbumId(mi);
        long artistId = GetArtistId(mi);

        if (wr is WeakReference<IPlaylistMenuService> weak && weak.TryGetTarget(out IPlaylistMenuService? service))
        {
            try
            {
                if (trackId > 0)
                    await service.AddTrackToCurrentListeningAsync(trackId);
                else if (albumId > 0)
                    await service.AddAlbumToCurrentListeningAsync(albumId);
                else if (artistId > 0)
                    await service.AddArtistToCurrentListeningAsync(artistId);
            }
            catch
            {
                // ignore
            }
        }
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
