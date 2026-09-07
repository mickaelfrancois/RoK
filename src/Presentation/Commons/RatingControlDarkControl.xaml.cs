using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace Rok.Commons;

public sealed partial class RatingControlDarkControl : UserControl
{
    private static readonly Color SelectedFlashColor = Color.FromArgb(255, 255, 243, 176);

    private readonly ILogger<RatingControlDarkControl> _logger = App.ServiceProvider.GetRequiredService<ILogger<RatingControlDarkControl>>();

    private Storyboard? _runningGesture;
    private int _currentScore;
    private int? _pendingExternalScore;

    public RatingControlDarkControl()
    {
        InitializeComponent();

        _currentScore = Value;

        InnerRating.ValueChanged += OnInnerRatingValueChanged;
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(RatingControlDarkControl),
            new PropertyMetadata(0, OnValuePropertyChanged));

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

    /// <summary>
    /// Records a score pushed in by the binding. Such a value always reaches this property before it
    /// reaches the inner control, so the matching <see cref="RatingControl.ValueChanged"/> is an echo
    /// and must not play a gesture. A score set by the user travels the other way around.
    /// </summary>
    private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        RatingControlDarkControl control = (RatingControlDarkControl)d;
        int newScore = (int)e.NewValue;

        if (newScore == control._currentScore)
            return;

        control._pendingExternalScore = newScore;

        control._logger.LogDebug("Rating row {RowName} received score {Score} from the binding", control.Name, newScore);
    }

    private void OnInnerRatingValueChanged(RatingControl sender, object args)
    {
        int previousScore = _currentScore;
        int newScore = (int)sender.Value;

        _currentScore = newScore;

        bool isBindingEcho = _pendingExternalScore == newScore;
        _pendingExternalScore = null;

        if (isBindingEcho)
            return;

        ScoreAnimationPlan? plan = ScoreAnimationPlan.For(previousScore, newScore);

        _logger.LogDebug("Rating row {RowName} score {PreviousScore} to {NewScore}, gesture scale {GestureScale}",
            Name, previousScore, newScore, plan?.ScaleTo);

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