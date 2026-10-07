using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Services.Accessibility;

namespace Rok.Commons;

public sealed partial class KeyVisualBox : UserControl
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(
        nameof(Shortcut),
        typeof(KeyboardShortcut),
        typeof(KeyVisualBox),
        new PropertyMetadata(null, OnShortcutChanged));

    public KeyboardShortcut? Shortcut
    {
        get => (KeyboardShortcut?)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    public KeyVisualBox()
    {
        InitializeComponent();
    }

    private static void OnShortcutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyVisualBox box && e.NewValue is KeyboardShortcut shortcut)
        {
            box.KeysHost.ItemsSource = KeyboardShortcutFormatter.Parts(shortcut);
        }
    }
}