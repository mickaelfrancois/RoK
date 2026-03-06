using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Shapes;

namespace Rok.Commons;

public sealed class SocialNetworkAppBarButton : AppBarButton
{
    private readonly Path _iconPath;

    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.Register(nameof(Url), typeof(string), typeof(SocialNetworkAppBarButton),
            new PropertyMetadata(null, OnUrlChanged));

    public static readonly DependencyProperty IconFillProperty =
        DependencyProperty.Register(nameof(IconFill), typeof(Brush), typeof(SocialNetworkAppBarButton),
            new PropertyMetadata(null, OnIconFillChanged));

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(string), typeof(SocialNetworkAppBarButton),
            new PropertyMetadata(null, OnIconDataChanged));

    public string? Url
    {
        get => (string?)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public Brush? IconFill
    {
        get => (Brush?)GetValue(IconFillProperty);
        set => SetValue(IconFillProperty, value);
    }

    public string? IconData
    {
        get => (string?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public SocialNetworkAppBarButton()
    {
        _iconPath = new Path();

        Canvas canvas = new() { Width = 24, Height = 24 };
        canvas.Children.Add(_iconPath);

        Viewbox viewbox = new() { Width = 20, Height = 20, Stretch = Stretch.Uniform, Child = canvas };
        Content = viewbox;
    }

    private static void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SocialNetworkAppBarButton button = (SocialNetworkAppBarButton)d;
        string? url = e.NewValue as string;
        button.CommandParameter = url;
        button.Visibility = string.IsNullOrEmpty(url) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnIconFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SocialNetworkAppBarButton)d)._iconPath.Fill = (Brush?)e.NewValue;

    private static void OnIconDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SocialNetworkAppBarButton button = (SocialNetworkAppBarButton)d;

        if (e.NewValue is not string data)
        {
            button._iconPath.Data = null;
            return;
        }

        Path tempPath = (Path)XamlReader.Load(
            $"<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Data=\"{data}\"/>");

        Geometry geometry = tempPath.Data;
        tempPath.Data = null;

        button._iconPath.Data = geometry;
    }
}