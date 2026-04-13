using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Dto;
using Rok.Domain.Enums;
using Rok.ViewModels.Player;
using Windows.Graphics;
using Windows.UI.Text;

namespace Rok.Commons.Equalizer;

internal sealed class EqualizerWindow : Window
{
    private const int WindowWidth = 640;
    private const int WindowHeight = 580;
    private readonly ResourceLoader _resourceLoader;

    internal EqualizerWindow(EqualizerViewModel viewModel, ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader;

        Title = resourceLoader.GetString("EqualizeWindowsTitle");
        BuildContent(viewModel);
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
        CenterOnMainWindow();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsResizable = false;

#if WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            SystemBackdrop = new MicaBackdrop();
#endif

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.ico"));
    }

    private void BuildContent(EqualizerViewModel viewModel)
    {
        Grid root = new() { Padding = new Thickness(16, 0, 16, 16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock description = new()
        {
            Text = _resourceLoader.GetString("EqualizeWindowsDescription"),
            FontSize = 12,
            Opacity = 0.7,
            Margin = new Thickness(0, 8, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };

        Grid.SetRow(description, 0);
        root.Children.Add(description);

        Grid chipsBar = BuildPresetChipsBar(viewModel);
        Grid.SetRow(chipsBar, 1);
        root.Children.Add(chipsBar);

        EqualizerControl equalizerControl = new() { ViewModel = viewModel };
        Grid.SetRow(equalizerControl, 2);
        root.Children.Add(equalizerControl);

        Button resetButton = new()
        {
            Content = _resourceLoader.GetString("EqualizeWindowsResetButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        resetButton.Click += (_, _) => viewModel.Reset();
        Grid.SetRow(resetButton, 3);
        root.Children.Add(resetButton);

        Border savePanel = BuildSavePanel(viewModel);
        Grid.SetRow(savePanel, 4);
        root.Children.Add(savePanel);

        Content = root;
    }

    private Grid BuildPresetChipsBar(EqualizerViewModel viewModel)
    {
        StackPanel stack = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        Dictionary<string, Button> chipByKey = new();

        foreach (EqualizerBuiltinPreset preset in EqualizerBuiltinPresets.All)
        {
            Button chip = new()
            {
                Content = _resourceLoader.GetString($"EqualizerBuiltin_{preset.Key}"),
                Padding = new Thickness(14, 5, 14, 5),
                CornerRadius = new CornerRadius(16),
                MinWidth = 0
            };
            chip.Click += (_, _) => viewModel.ApplyBuiltinPreset(preset);
            chipByKey[preset.Key] = chip;
            stack.Children.Add(chip);
        }

        UpdateChipStyles(chipByKey, viewModel.ActiveBuiltinPreset?.Key);

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EqualizerViewModel.ActiveBuiltinPreset))
                UpdateChipStyles(chipByKey, viewModel.ActiveBuiltinPreset?.Key);
        };

        ScrollViewer sv = new()
        {
            Content = stack,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled
        };

        Button leftBtn = new()
        {
            Content = "\uE76B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            Width = 30,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };
        leftBtn.Click += (_, _) => sv.ChangeView(sv.HorizontalOffset - 200, null, null);

        Button rightBtn = new()
        {
            Content = "\uE76C",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            Width = 30,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };
        rightBtn.Click += (_, _) => sv.ChangeView(sv.HorizontalOffset + 200, null, null);

        Grid container = new() { Margin = new Thickness(0, 0, 0, 12) };
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(leftBtn, 0);
        Grid.SetColumn(sv, 1);
        Grid.SetColumn(rightBtn, 2);

        container.Children.Add(leftBtn);
        container.Children.Add(sv);
        container.Children.Add(rightBtn);

        return container;
    }

    private static void UpdateChipStyles(Dictionary<string, Button> chipByKey, string? activeKey)
    {
        Style accentStyle = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];

        foreach ((string key, Button chip) in chipByKey)
            chip.Style = key == activeKey ? accentStyle : null;
    }

    private static void UpdateScopeSaveStyles(Dictionary<EqualizerScope, Button> scopeBtnByScope, EqualizerScope activeScope)
    {
        Style accentStyle = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];

        foreach ((EqualizerScope scope, Button btn) in scopeBtnByScope)
            btn.Style = scope == activeScope ? accentStyle : null;
    }

    private Border BuildSavePanel(EqualizerViewModel viewModel)
    {
        TextBlock title = new()
        {
            Text = _resourceLoader.GetString("EqualizerSavePanelTitle"),
            FontSize = 13,
            FontWeight = new Windows.UI.Text.FontWeight(600),
            Margin = new Thickness(0, 0, 0, 4)
        };

        TextBlock activePresetInfo = new()
        {
            Text = GetActivePresetLabel(viewModel.ActivePresetScope),
            FontSize = 11,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 10)
        };

        Button trackBtn = new() { Content = _resourceLoader.GetString("EqualizerSaveScopeTrack"), Margin = new Thickness(0, 0, 8, 0), IsEnabled = viewModel.CanSaveForTrack };
        Button albumBtn = new() { Content = _resourceLoader.GetString("EqualizerSaveScopeAlbum"), Margin = new Thickness(0, 0, 8, 0), IsEnabled = viewModel.CanSaveForAlbum };
        Button artistBtn = new() { Content = _resourceLoader.GetString("EqualizerSaveScopeArtist"), Margin = new Thickness(0, 0, 8, 0), IsEnabled = viewModel.CanSaveForArtist };
        Button genreBtn = new() { Content = _resourceLoader.GetString("EqualizerSaveScopeGenre"), Margin = new Thickness(0, 0, 8, 0), IsEnabled = viewModel.CanSaveForGenre };

        Dictionary<EqualizerScope, Button> scopeBtnByScope = new();
        scopeBtnByScope[EqualizerScope.Track] = trackBtn;
        scopeBtnByScope[EqualizerScope.Album] = albumBtn;
        scopeBtnByScope[EqualizerScope.Artist] = artistBtn;
        scopeBtnByScope[EqualizerScope.Genre] = genreBtn;

        trackBtn.Click += async (_, _) => await viewModel.SavePresetAsync(EqualizerScope.Track);
        albumBtn.Click += async (_, _) => await viewModel.SavePresetAsync(EqualizerScope.Album);
        artistBtn.Click += async (_, _) => await viewModel.SavePresetAsync(EqualizerScope.Artist);
        genreBtn.Click += async (_, _) => await viewModel.SavePresetAsync(EqualizerScope.Genre);

        Button removeBtn = new()
        {
            Content = _resourceLoader.GetString("EqualizerRemovePresetButton"),
            Margin = new Thickness(8, 0, 0, 0),
            IsEnabled = viewModel.CanRemoveActivePreset
        };
        removeBtn.Click += async (_, _) => await viewModel.RemovePresetAsync();

        UpdateScopeSaveStyles(scopeBtnByScope, viewModel.ActivePresetScope);

        StackPanel buttons = new() { Orientation = Orientation.Horizontal };
        buttons.Children.Add(trackBtn);
        buttons.Children.Add(albumBtn);
        buttons.Children.Add(artistBtn);
        buttons.Children.Add(genreBtn);
        buttons.Children.Add(removeBtn);

        viewModel.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(EqualizerViewModel.CanSaveForTrack): trackBtn.IsEnabled = viewModel.CanSaveForTrack; break;
                case nameof(EqualizerViewModel.CanSaveForAlbum): albumBtn.IsEnabled = viewModel.CanSaveForAlbum; break;
                case nameof(EqualizerViewModel.CanSaveForArtist): artistBtn.IsEnabled = viewModel.CanSaveForArtist; break;
                case nameof(EqualizerViewModel.CanSaveForGenre): genreBtn.IsEnabled = viewModel.CanSaveForGenre; break;
                case nameof(EqualizerViewModel.ActivePresetScope):
                    activePresetInfo.Text = GetActivePresetLabel(viewModel.ActivePresetScope);
                    UpdateScopeSaveStyles(scopeBtnByScope, viewModel.ActivePresetScope);
                    break;
                case nameof(EqualizerViewModel.CanRemoveActivePreset): removeBtn.IsEnabled = viewModel.CanRemoveActivePreset; break;
            }
        };

        StackPanel panel = new() { Spacing = 4 };
        panel.Children.Add(title);
        panel.Children.Add(activePresetInfo);
        panel.Children.Add(buttons);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 8, 0, 0),
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = panel
        };
    }

    private string GetActivePresetLabel(EqualizerScope scope) => scope switch
    {
        EqualizerScope.Track => _resourceLoader.GetString("EqualizerActivePresetTrack"),
        EqualizerScope.Album => _resourceLoader.GetString("EqualizerActivePresetAlbum"),
        EqualizerScope.Artist => _resourceLoader.GetString("EqualizerActivePresetArtist"),
        EqualizerScope.Genre => _resourceLoader.GetString("EqualizerActivePresetGenre"),
        _ => _resourceLoader.GetString("EqualizerActivePresetDefault"),
    };

    private void CenterOnMainWindow()
    {
        PointInt32 mainPos = App.MainWindow.AppWindow.Position;
        SizeInt32 mainSize = App.MainWindow.AppWindow.Size;

        AppWindow.Move(new PointInt32(
            mainPos.X + ((mainSize.Width - WindowWidth) / 2),
            mainPos.Y + ((mainSize.Height - WindowHeight) / 2)));
    }
}
