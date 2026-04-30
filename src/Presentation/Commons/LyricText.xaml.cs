using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Rok.Commons;

public sealed partial class LyricText : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(LyricText), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LyricColorProperty =
        DependencyProperty.Register(nameof(LyricColor), typeof(SolidColorBrush), typeof(LyricText),
            new PropertyMetadata(new SolidColorBrush(Colors.White)));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public SolidColorBrush LyricColor
    {
        get => (SolidColorBrush)GetValue(LyricColorProperty);
        set => SetValue(LyricColorProperty, value);
    }

    public LyricText()
    {
        InitializeComponent();
    }
}
