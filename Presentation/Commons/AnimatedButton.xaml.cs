using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rok.Commons;

public sealed partial class AnimatedButton : UserControl, IDisposable
{
    public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(string), typeof(AnimatedButton), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(SolidColorBrush), typeof(AnimatedButton), null);

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register("CommandParameter", typeof(object), typeof(AnimatedButton), null);

    public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(System.Windows.Input.ICommand), typeof(AnimatedButton), null);

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public SolidColorBrush Color
    {
        get => (SolidColorBrush)base.GetValue(ColorProperty);
        set => base.SetValue(ColorProperty, value);
    }

    public System.Windows.Input.ICommand Command
    {
        get => (System.Windows.Input.ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private bool _pendingExecute;


    public AnimatedButton()
    {
        InitializeComponent();

        Loaded += AnimatedButton_Loaded;
        Unloaded += AnimatedButton_Unloaded;
        button.PointerEntered += Button_PointerEntered;
        button.PointerExited += Button_PointerExited;
        button.PointerReleased += Button_PointerReleased;
        button.PointerPressed += Button_PointerPressed;
    }

    private void AnimatedButton_Loaded(object sender, RoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", false);
    }

    private void AnimatedButton_Unloaded(object sender, RoutedEventArgs e)
    {
        _pendingExecute = false;
    }


    private void Button_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Pressed", true);
        e.Handled = true;

        if (!_pendingExecute)
        {
            _pendingExecute = true;
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (!_pendingExecute) return;
                _pendingExecute = false;

                if (Command is { } cmd && cmd.CanExecute(CommandParameter))
                    cmd.Execute(CommandParameter);

                VisualStateManager.GoToState(this, "PointerOver", false);
            });
        }
    }

    private void Button_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    public void Dispose()
    {
        Loaded -= AnimatedButton_Loaded;
        Unloaded -= AnimatedButton_Unloaded;
        button.PointerEntered -= Button_PointerEntered;
        button.PointerExited -= Button_PointerExited;
        button.PointerPressed -= Button_PointerPressed;
        button.PointerReleased -= Button_PointerReleased;
    }
}
