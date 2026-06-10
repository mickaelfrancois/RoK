using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ICommand = System.Windows.Input.ICommand;

namespace Rok.Commons;

public sealed partial class PictureControl : UserControl
{
    public static readonly DependencyProperty CoverProperty =
        DependencyProperty.Register(nameof(Cover), typeof(ImageSource), typeof(PictureControl), new PropertyMetadata(null, OnCoverChanged));

    public static readonly DependencyProperty PlayButtonVisibilityProperty =
        DependencyProperty.Register(nameof(PlayButtonVisibility), typeof(Visibility), typeof(PictureControl),
            new PropertyMetadata(Visibility.Collapsed, OnPlayButtonVisibilityChanged));

    public static readonly DependencyProperty AddPlaylistButtonVisibilityProperty =
        DependencyProperty.Register(nameof(AddPlaylistButtonVisibility), typeof(Visibility), typeof(PictureControl),
            new PropertyMetadata(Visibility.Collapsed, OnAddPlaylistButtonVisibilityChanged));

    public static readonly DependencyProperty UseArtistFallbackProperty =
        DependencyProperty.Register(nameof(UseArtistFallback), typeof(bool), typeof(PictureControl), new PropertyMetadata(false));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(PictureControl), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(PictureControl), new PropertyMetadata(null));

    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.Register(nameof(Flyout), typeof(Microsoft.UI.Xaml.Controls.Flyout), typeof(PictureControl), new PropertyMetadata(null));

    public PictureControl()
    {
        InitializeComponent();
    }


    public ImageSource Cover
    {
        get => (ImageSource)GetValue(CoverProperty);
        set => SetValue(CoverProperty, value);
    }


    public bool UseArtistFallback
    {
        get => (bool)GetValue(UseArtistFallbackProperty);
        set => SetValue(UseArtistFallbackProperty, value);
    }


    private static void OnCoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PictureControl pc || pc.placeholderRoot is null || pc.img1 is null)
            return;

        // Drive opacity directly (not via VisualState) so it is reliable regardless of template/decoding timing.
        // No cover -> show the themed placeholder; a real cover is revealed on ImageOpened.
        // Also covers virtualized-list recycling (cover -> none) and failed image decodes.
        pc.placeholderRoot.Opacity = 1;
        pc.img1.Opacity = e.NewValue is null ? 0 : 1;
    }


    private void Cover_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }


    private void Cover_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", false);
    }


    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public Microsoft.UI.Xaml.Controls.Flyout Flyout
    {
        get => (Microsoft.UI.Xaml.Controls.Flyout)GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    public Visibility PlayButtonVisibility
    {
        get => (Visibility)GetValue(PlayButtonVisibilityProperty);
        set => SetValue(PlayButtonVisibilityProperty, value);
    }

    public Visibility AddPlaylistButtonVisibility
    {
        get => (Visibility)GetValue(AddPlaylistButtonVisibilityProperty);
        set => SetValue(AddPlaylistButtonVisibilityProperty, value);
    }

    private static void OnPlayButtonVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PictureControl pc && pc.btPlay is not null)
            pc.btPlay.Visibility = (Visibility)e.NewValue;
    }

    private static void OnAddPlaylistButtonVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PictureControl pc && pc.btAddPlaylist is not null)
            pc.btAddPlaylist.Visibility = (Visibility)e.NewValue;
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // Apply DP values once the template is realized (covers art-less items, which never raise ImageOpened).
        btPlay.Visibility = PlayButtonVisibility;
        btAddPlaylist.Visibility = AddPlaylistButtonVisibility;
    }

    private void ImgOpened(object sender, RoutedEventArgs e)
    {
        // A real image decoded: reveal it and hide the placeholder. Direct sets are timing-safe
        // (covers the case where the cover was assigned before the template was realized).
        img1.Opacity = 1;
        placeholderRoot.Opacity = 0;

        CalculateScale(sender);
    }

    private void ImgSizeChanged(object sender, SizeChangedEventArgs e)
    {
        CalculateScale(sender);
    }

    private static void CalculateScale(object sender)
    {
        Image? img = sender as Image;
        if (img?.RenderTransform is ScaleTransform scale)
        {
            scale.CenterX = img.ActualWidth / 2;
            scale.CenterY = img.ActualHeight / 2;
        }
    }
}