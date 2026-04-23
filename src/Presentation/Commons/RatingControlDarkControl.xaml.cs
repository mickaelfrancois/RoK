using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

public sealed partial class RatingControlDarkControl : UserControl
{
    public RatingControlDarkControl()
    {
        InitializeComponent();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(RatingControlDarkControl),
            new PropertyMetadata(0));

    public int InitialValue
    {
        get => (int)GetValue(InitialValueProperty);
        set => SetValue(InitialValueProperty, value);
    }
    public static readonly DependencyProperty InitialValueProperty =
        DependencyProperty.Register(nameof(InitialValue), typeof(int), typeof(RatingControlDarkControl),
            new PropertyMetadata(0));

    public int MaxRating
    {
        get => (int)GetValue(MaxRatingProperty);
        set => SetValue(MaxRatingProperty, value);
    }
    public static readonly DependencyProperty MaxRatingProperty =
        DependencyProperty.Register(nameof(MaxRating), typeof(int), typeof(RatingControlDarkControl),
            new PropertyMetadata(5));

    public bool IsClearEnabled
    {
        get => (bool)GetValue(IsClearEnabledProperty);
        set => SetValue(IsClearEnabledProperty, value);
    }
    public static readonly DependencyProperty IsClearEnabledProperty =
        DependencyProperty.Register(nameof(IsClearEnabled), typeof(bool), typeof(RatingControlDarkControl),
            new PropertyMetadata(false));
}