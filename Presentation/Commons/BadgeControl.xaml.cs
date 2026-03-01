using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

public sealed partial class BadgeControl : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(BadgeControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeBackgroundProperty =
        DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(BadgeControl), new PropertyMetadata(null));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush BadgeBackground
    {
        get => (Brush)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    public BadgeControl()
    {
        InitializeComponent();
    }
}