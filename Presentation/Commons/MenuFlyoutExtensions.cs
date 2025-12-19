using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Playlists.PlaylistMenu;
using System.Runtime.CompilerServices;

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
            new PropertyMetadata(0L));

    // store per-menuitem click handler so we can detach before removal
    private static readonly DependencyProperty MenuItemClickHandlerProperty =
        DependencyProperty.RegisterAttached(
            "MenuItemClickHandler",
            typeof(RoutedEventHandler),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(null));

    // attached property to store playlist id on MenuFlyoutItem (avoid closures)
    private static readonly DependencyProperty MenuItemPlaylistIdProperty =
        DependencyProperty.RegisterAttached(
            "MenuItemPlaylistId",
            typeof(long),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(0L));

    // attached property to store weak reference to service on MenuFlyoutItem
    private static readonly DependencyProperty MenuItemServiceWeakRefProperty =
        DependencyProperty.RegisterAttached(
            "MenuItemServiceWeakRef",
            typeof(object),
            typeof(MenuFlyoutExtensions),
            new PropertyMetadata(null));

    // map service -> list of menu weakrefs, use ConditionalWeakTable to avoid keeping services alive
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
            await UpdatePlaylistMenuItems(menuFlyout, newService).ConfigureAwait(false);
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
                // subscribe static handler once
                service.PlaylistsChanged += Service_PlaylistsChanged;
            }

            // add weak ref if not present
            if (!list.Any(wr => wr.TryGetTarget(out MenuFlyout? mf) && ReferenceEquals(mf, menuFlyout)))
            {
                list.Add(new WeakReference<MenuFlyout>(menuFlyout));
            }
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
                    // remove key and unsubscribe
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
            // create snapshot
            list = list.ToList();
        }

        foreach (WeakReference<MenuFlyout> wr in list)
        {
            if (wr.TryGetTarget(out MenuFlyout? mf))
            {
                if (mf.DispatcherQueue.HasThreadAccess)
                {
                    _ = UpdatePlaylistMenuItems(mf, service);
                }
                else
                {
                    mf.DispatcherQueue.TryEnqueue(() => _ = UpdatePlaylistMenuItems(mf, service));
                }
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
                    try { mi.Click -= handler; } catch { }
                    SetMenuItemClickHandler(mi, null);
                }

                try { SetMenuItemServiceWeakRef(mi, null); } catch { }
                try { SetMenuItemPlaylistId(mi, 0); } catch { }
            }

            try { existingAddToItem.Items.Clear(); } catch { }
            try { if (menuFlyout.Items.Contains(existingAddToItem)) menuFlyout.Items.Remove(existingAddToItem); } catch { }
        }
    }

    private static async Task UpdatePlaylistMenuItems(MenuFlyout menuFlyout, IPlaylistMenuService service)
    {
        ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

        CleanupExistingSubmenuClickHandlers(menuFlyout);

        IEnumerable<PlaylistMenuItem> playlists = await service.GetPlaylistMenuItemsAsync();

        if (!menuFlyout.Items.OfType<MenuFlyoutSeparator>().Any())
            menuFlyout.Items.Add(new MenuFlyoutSeparator());

        MenuFlyoutSubItem addToSubMenu = new()
        {
            Text = resourceLoader.GetString("MenuFlyout_AddTo_Text") ?? "Add to",
            Icon = new FontIcon { Glyph = "\uE710" },
            Tag = "AddToPlaylistSubMenu"
        };

        long trackId = GetTrackId(menuFlyout);

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

                // attach static handler
                RoutedEventHandler handler = MenuItemStaticClickHandler;
                menuItem.Click += handler;
                SetMenuItemClickHandler(menuItem, handler);

                addToSubMenu.Items.Add(menuItem);
            }

            if (addToSubMenu.Items.Count > 0)
                addToSubMenu.Items.Add(new MenuFlyoutSeparator());
        }

        MenuFlyoutItem newPlaylistItem = new()
        {
            Text = resourceLoader.GetString("MenuFlyout_NewPlaylist_Text") ?? "New playlist...",
            Icon = new FontIcon { Glyph = "\uE710" },
            Tag = "NewPlaylistItem"
        };

        SetMenuItemPlaylistId(newPlaylistItem, -1);
        SetMenuItemServiceWeakRef(newPlaylistItem, new WeakReference<IPlaylistMenuService>(service));
        newPlaylistItem.SetValue(MenuItemPlaylistIdProperty, trackId);

        RoutedEventHandler newHandler = NewPlaylistStaticClickHandler;
        newPlaylistItem.Click += newHandler;
        SetMenuItemClickHandler(newPlaylistItem, newHandler);

        addToSubMenu.Items.Add(newPlaylistItem);
        menuFlyout.Items.Add(addToSubMenu);
    }

    private static async void MenuItemStaticClickHandler(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem mi)
            return;

        object? wr = GetMenuItemServiceWeakRef(mi);
        long playlistId = GetMenuItemPlaylistId(mi);
        long trackId = GetMenuItemPlaylistId(mi);

        if (wr is WeakReference<IPlaylistMenuService> weak && weak.TryGetTarget(out IPlaylistMenuService? service))
        {
            try
            {
                if (playlistId > 0)
                    await service.AddTrackToPlaylistAsync(playlistId, trackId);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static async void NewPlaylistStaticClickHandler(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem mi)
            return;

        object? wr = GetMenuItemServiceWeakRef(mi);
        long trackId = GetMenuItemPlaylistId(mi);

        if (wr is WeakReference<IPlaylistMenuService> weak && weak.TryGetTarget(out IPlaylistMenuService? service))
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot != null)
                {
                    string? playlistName = await ShowCreatePlaylistDialogAsync(App.MainWindow.Content.XamlRoot);
                    if (!string.IsNullOrWhiteSpace(playlistName))
                    {
                        await service.CreateNewPlaylistWithTrackAsync(playlistName, trackId);
                    }
                }
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
