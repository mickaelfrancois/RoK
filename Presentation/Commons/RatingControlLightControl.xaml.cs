using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.UI;
using ICommand = System.Windows.Input.ICommand;

namespace Rok.Commons;

public sealed partial class RatingControlLightControl : UserControl
{
    public RatingControlLightControl()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyRatingToVisuals(); // assure l’état initial
    }

    private const int DefaultMaxRating = 5;

    public static readonly DependencyProperty MaxRatingProperty =
        DependencyProperty.Register(
            nameof(MaxRating),
            typeof(int),
            typeof(RatingControlLightControl),
            new PropertyMetadata(DefaultMaxRating, OnMaxRatingChanged));

    public int MaxRating
    {
        get => (int)GetValue(MaxRatingProperty);
        set => SetValue(MaxRatingProperty, value < 0 ? 0 : value);
    }

    public static readonly DependencyProperty RatingValueProperty =
        DependencyProperty.Register(
            nameof(RatingValue),
            typeof(int),
            typeof(RatingControlLightControl),
            new PropertyMetadata(0, OnRatingValueChanged));

    public int RatingValue
    {
        get => (int)GetValue(RatingValueProperty);
        set
        {
            int clamped = Math.Clamp(value, 0, MaxRating);
            SetValue(RatingValueProperty, clamped);
        }
    }

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(RatingControlLightControl),
            new PropertyMetadata(null));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter),
            typeof(object),
            typeof(RatingControlLightControl),
            new PropertyMetadata(null));

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty StarSelectedColorProperty =
        DependencyProperty.Register(nameof(StarSelectedColor), typeof(Color), typeof(RatingControlLightControl),
            new PropertyMetadata(Colors.Yellow));

    public Color StarSelectedColor
    {
        get => (Color)GetValue(StarSelectedColorProperty);
        set => SetValue(StarSelectedColorProperty, value);
    }

    public static readonly DependencyProperty StarEmptyColorProperty =
        DependencyProperty.Register(nameof(StarEmptyColor), typeof(Color), typeof(RatingControlLightControl),
            new PropertyMetadata(Colors.Transparent));

    public Color StarEmptyColor
    {
        get => (Color)GetValue(StarEmptyColorProperty);
        set => SetValue(StarEmptyColorProperty, value);
    }

    public static readonly DependencyProperty StarPointerOverColorProperty =
        DependencyProperty.Register(nameof(StarPointerOverColor), typeof(Color), typeof(RatingControlLightControl),
            new PropertyMetadata(Colors.Gold));

    public Color StarPointerOverColor
    {
        get => (Color)GetValue(StarPointerOverColorProperty);
        set => SetValue(StarPointerOverColorProperty, value);
    }

    public static readonly DependencyProperty StarCheckedPointerOverColorProperty =
        DependencyProperty.Register(nameof(StarCheckedPointerOverColor), typeof(Color), typeof(RatingControlLightControl),
            new PropertyMetadata(Colors.White));

    public Color StarCheckedPointerOverColor
    {
        get => (Color)GetValue(StarCheckedPointerOverColorProperty);
        set => SetValue(StarCheckedPointerOverColorProperty, value);
    }

    public static readonly DependencyProperty StarStrokeColorProperty =
        DependencyProperty.Register(nameof(StarStrokeColor), typeof(Color), typeof(RatingControlLightControl),
            new PropertyMetadata(Colors.Gray));

    public Color StarStrokeColor
    {
        get => (Color)GetValue(StarStrokeColorProperty);
        set => SetValue(StarStrokeColorProperty, value);
    }

    public static readonly DependencyProperty StarSizeProperty =
        DependencyProperty.Register(nameof(StarSize), typeof(double), typeof(RatingControlLightControl),
            new PropertyMetadata(24d));

    public double StarSize
    {
        get => (double)GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value <= 0 ? 1 : value);
    }

    public static readonly DependencyProperty StarSpacingProperty =
        DependencyProperty.Register(nameof(StarSpacing), typeof(double), typeof(RatingControlLightControl),
            new PropertyMetadata(4d));

    public double StarSpacing
    {
        get => (double)GetValue(StarSpacingProperty);
        set => SetValue(StarSpacingProperty, value < 0 ? 0 : value);
    }

    private static void OnMaxRatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        RatingControlLightControl control = (RatingControlLightControl)d;
        if (control.RatingValue > control.MaxRating)
            control.RatingValue = control.MaxRating;
        control.RefreshStars();
    }

    private static void OnRatingValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        RatingControlLightControl control = (RatingControlLightControl)d;
        control.ApplyRatingToVisuals();
    }

    private void RatingButtonClickEventHandler(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb)
            return;

        if (!int.TryParse(tb.Tag?.ToString(), out int clickedValue))
            return;

        RatingValue = clickedValue == RatingValue ? 0 : clickedValue;

        if (Command is { } cmd)
        {
            object param = CommandParameter ?? RatingValue;
            if (cmd.CanExecute(param))
                cmd.Execute(param);
        }
    }

    private void ApplyRatingToVisuals()
    {
        if (Content is not Panel panel)
            return;

        List<(ToggleButton Button, int Value)> buttons = panel.Children.OfType<ToggleButton>()
            .Select(b => (Button: b, Value: GetTagAsInt(b)))
            .OrderBy(t => t.Value)
            .ToList();

        foreach ((ToggleButton button, int value) in buttons)
            button.IsChecked = value <= RatingValue;
    }

    private void RefreshStars() => ApplyRatingToVisuals();

    private static int GetTagAsInt(ToggleButton b) =>
        int.TryParse(b.Tag?.ToString(), out int v) ? v : 0;
}