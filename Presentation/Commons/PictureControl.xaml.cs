using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ICommand = System.Windows.Input.ICommand;

namespace Rok.Commons;

public sealed partial class PictureControl : UserControl
{
    public static readonly DependencyProperty CoverProperty =
        DependencyProperty.Register(nameof(Cover), typeof(ImageSource), typeof(PictureControl), new PropertyMetadata(null));

    public static readonly DependencyProperty PlayButtonVisibilityProperty =
        DependencyProperty.Register(nameof(PlayButtonVisibility), typeof(Visibility), typeof(PictureControl),
            new PropertyMetadata(Visibility.Collapsed, OnPlayButtonVisibilityChanged));

    public static readonly DependencyProperty AddPlaylistButtonVisibilityProperty =
        DependencyProperty.Register(nameof(AddPlaylistButtonVisibility), typeof(Visibility), typeof(PictureControl),
            new PropertyMetadata(Visibility.Collapsed, OnAddPlaylistButtonVisibilityChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(PictureControl), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(PictureControl), new PropertyMetadata(null));

    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.Register(nameof(Flyout), typeof(Microsoft.UI.Xaml.Controls.Flyout), typeof(PictureControl), new PropertyMetadata(null));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(PictureControl), new PropertyMetadata(string.Empty));


    public PictureControl()
    {
        InitializeComponent();
        Icon = "\uE81D";
    }


    public ImageSource Cover
    {
        get => (ImageSource)GetValue(CoverProperty);
        set => SetValue(CoverProperty, value);
    }


    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
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

    private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        // gestion d'erreur si besoin
    }

    private void img_Loaded(object sender, RoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "imgLoaded", true);

        // appliquer les valeurs DP (au cas où elles ont été définies avant l'initialisation visuelle)
        btPlay.Visibility = PlayButtonVisibility;
        btAddPlaylist.Visibility = AddPlaylistButtonVisibility;
    }
}
