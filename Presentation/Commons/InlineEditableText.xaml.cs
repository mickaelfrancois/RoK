using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Rok.Commons;

public sealed partial class InlineEditableText : UserControl
{
    public InlineEditableText()
    {
        this.InitializeComponent();
        IsEditing = false;
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(InlineEditableText), new PropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextStyleProperty =
        DependencyProperty.Register(nameof(TextStyle), typeof(Style), typeof(InlineEditableText), new PropertyMetadata(null));

    public Style TextStyle
    {
        get => (Style)GetValue(TextStyleProperty);
        set => SetValue(TextStyleProperty, value);
    }

    public static readonly DependencyProperty IsEditingProperty =
        DependencyProperty.Register(nameof(IsEditing), typeof(bool), typeof(InlineEditableText), new PropertyMetadata(false));

    public bool IsEditing
    {
        get => (bool)GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public static readonly DependencyProperty IsMouseOverPanelProperty =
        DependencyProperty.Register(nameof(IsMouseOverPanel), typeof(bool), typeof(InlineEditableText), new PropertyMetadata(false));

    public bool IsMouseOverPanel
    {
        get => (bool)GetValue(IsMouseOverPanelProperty);
        set => SetValue(IsMouseOverPanelProperty, value);
    }

    private void DisplayPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        IsMouseOverPanel = true;
    }

    private void DisplayPanel_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        IsMouseOverPanel = false;
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        IsEditing = true;
        EditBox.Focus(FocusState.Programmatic);
        EditBox.SelectAll();
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        IsEditing = false;
    }

    private void EditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            IsEditing = false;
    }
}