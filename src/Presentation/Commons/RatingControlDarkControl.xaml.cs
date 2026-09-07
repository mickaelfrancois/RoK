using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace Rok.Commons;

public sealed partial class RatingControlDarkControl : UserControl
{
    private static readonly Color SelectedFlashColor = Color.FromArgb(255, 255, 243, 176);

    private Storyboard? _runningGesture;
    private bool _isPointerOver;
    private int _currentScore;

    public RatingControlDarkControl()
    {
        InitializeComponent();

        _currentScore = Value;

        InnerRating.ValueChanged += OnInnerRatingValueChanged;
        InnerRating.PointerEntered += (_, _) => _isPointerOver = true;
        InnerRating.PointerExited += (_, _) => _isPointerOver = false;
        InnerRating.PointerCaptureLost += (_, _) => _isPointerOver = false;
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
            new PropertyMetadata(true));

    private bool IsScoreChangeUserDriven => _isPointerOver || InnerRating.FocusState != FocusState.Unfocused;

    private void OnInnerRatingValueChanged(RatingControl sender, object args)
    {
        int previousScore = _currentScore;
        _currentScore = (int)sender.Value;

        if (!IsScoreChangeUserDriven)
            return;

        ScoreAnimationPlan? plan = ScoreAnimationPlan.For(previousScore, _currentScore);

        if (plan is null)
            return;

        PlayGesture(plan);
    }

    private void PlayGesture(ScoreAnimationPlan plan)
    {
        _runningGesture?.Stop();

        TimeSpan halfDuration = TimeSpan.FromTicks(plan.Duration.Ticks / 2);

        Storyboard gesture = new();
        gesture.Children.Add(BuildTransformAnimation(nameof(CompositeTransform.ScaleX), plan.ScaleTo, halfDuration));
        gesture.Children.Add(BuildTransformAnimation(nameof(CompositeTransform.ScaleY), plan.ScaleTo, halfDuration));

        if (plan.WobbleDegrees != 0)
            gesture.Children.Add(BuildTransformAnimation(nameof(CompositeTransform.Rotation), plan.WobbleDegrees, halfDuration));

        if (plan.FlashSelectedColor && Resources["RatingControlSelectedForeground"] is SolidColorBrush selectedBrush)
            gesture.Children.Add(BuildFlashAnimation(selectedBrush, halfDuration));

        _runningGesture = gesture;
        gesture.Begin();
    }

    private DoubleAnimation BuildTransformAnimation(string property, double to, TimeSpan duration)
    {
        DoubleAnimation animation = new()
        {
            To = to,
            Duration = new Duration(duration),
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, AnimationTransform);
        Storyboard.SetTargetProperty(animation, property);

        return animation;
    }

    private static ColorAnimation BuildFlashAnimation(SolidColorBrush selectedBrush, TimeSpan duration)
    {
        ColorAnimation animation = new()
        {
            To = SelectedFlashColor,
            Duration = new Duration(duration),
            AutoReverse = true,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, selectedBrush);
        Storyboard.SetTargetProperty(animation, nameof(SolidColorBrush.Color));

        return animation;
    }
}