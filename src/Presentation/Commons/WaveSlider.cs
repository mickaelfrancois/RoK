using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Rok.Commons;

/// <summary>
/// A <see cref="Slider"/> that draws its elapsed portion as a wave instead of a solid bar,
/// leaving the remaining portion as a flat line, and gives the thumb a white backing on hover.
/// </summary>
/// <remarks>
/// The wave is injected into the stock template rather than replacing it, so the slider keeps
/// every built-in behaviour (seeking, thumb tooltip, visual states) and still benefits from
/// future SDK template fixes. When an expected template part is missing the control silently
/// falls back to rendering as a plain slider.
/// </remarks>
public sealed class WaveSlider : Slider
{
    private const string DecreaseRectPartName = "HorizontalDecreaseRect";
    private const string TrackRectPartName = "HorizontalTrackRect";
    private const string ThumbPartName = "HorizontalThumb";

    private const double SampleStep = 1.5;

    private static readonly Brush DefaultThumbHoverFillBrush = new SolidColorBrush(Colors.White);

    public static readonly DependencyProperty WaveAmplitudeProperty =
        DependencyProperty.Register(nameof(WaveAmplitude), typeof(double), typeof(WaveSlider), new PropertyMetadata(3.0, OnWaveShapeChanged));

    public static readonly DependencyProperty WaveLengthProperty =
        DependencyProperty.Register(nameof(WaveLength), typeof(double), typeof(WaveSlider), new PropertyMetadata(14.0, OnWaveShapeChanged));

    public static readonly DependencyProperty WaveThicknessProperty =
        DependencyProperty.Register(nameof(WaveThickness), typeof(double), typeof(WaveSlider), new PropertyMetadata(3.0, OnWaveShapeChanged));

    public static readonly DependencyProperty ThumbHoverRingBrushProperty =
        DependencyProperty.Register(nameof(ThumbHoverRingBrush), typeof(Brush), typeof(WaveSlider), new PropertyMetadata(null));

    public static readonly DependencyProperty ThumbHoverFillBrushProperty =
        DependencyProperty.Register(nameof(ThumbHoverFillBrush), typeof(Brush), typeof(WaveSlider), new PropertyMetadata(null));

    public static readonly DependencyProperty ThumbHoverInflateProperty =
        DependencyProperty.Register(nameof(ThumbHoverInflate), typeof(double), typeof(WaveSlider), new PropertyMetadata(2.0));

    public static readonly DependencyProperty ThumbHoverRingThicknessProperty =
        DependencyProperty.Register(nameof(ThumbHoverRingThickness), typeof(double), typeof(WaveSlider), new PropertyMetadata(3.0));

    private readonly RectangleGeometry _waveClip = new();
    private readonly RectangleGeometry _trackClip = new();

    private Path? _wave;
    private Border? _thumbRing;
    private Border? _thumbFill;
    private FrameworkElement? _decreaseRect;
    private FrameworkElement? _trackRect;
    private FrameworkElement? _thumb;
    private double _renderedWaveWidth = -1;

    /// <summary>Peak height of the wave, in pixels, measured from the track axis.</summary>
    public double WaveAmplitude
    {
        get => (double)GetValue(WaveAmplitudeProperty);
        set => SetValue(WaveAmplitudeProperty, value);
    }

    /// <summary>Horizontal distance, in pixels, between two crests.</summary>
    public double WaveLength
    {
        get => (double)GetValue(WaveLengthProperty);
        set => SetValue(WaveLengthProperty, value);
    }

    /// <summary>Stroke thickness of the wave, in pixels.</summary>
    public double WaveThickness
    {
        get => (double)GetValue(WaveThicknessProperty);
        set => SetValue(WaveThicknessProperty, value);
    }

    /// <summary>Ring drawn around the thumb on hover. Defaults to <see cref="Control.Foreground"/>.</summary>
    public Brush? ThumbHoverRingBrush
    {
        get => (Brush?)GetValue(ThumbHoverRingBrushProperty);
        set => SetValue(ThumbHoverRingBrushProperty, value);
    }

    /// <summary>Fill covering the thumb on hover. Defaults to white.</summary>
    public Brush? ThumbHoverFillBrush
    {
        get => (Brush?)GetValue(ThumbHoverFillBrushProperty);
        set => SetValue(ThumbHoverFillBrushProperty, value);
    }

    /// <summary>How far, in pixels, the hover ring extends past the thumb on every side.</summary>
    public double ThumbHoverInflate
    {
        get => (double)GetValue(ThumbHoverInflateProperty);
        set => SetValue(ThumbHoverInflateProperty, value);
    }

    /// <summary>Width, in pixels, of the visible ring band. The fill is inset by this much.</summary>
    public double ThumbHoverRingThickness
    {
        get => (double)GetValue(ThumbHoverRingThicknessProperty);
        set => SetValue(ThumbHoverRingThicknessProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        DetachParts();

        base.OnApplyTemplate();

        if (Orientation != Orientation.Horizontal)
            return;

        _decreaseRect = GetTemplateChild(DecreaseRectPartName) as FrameworkElement;
        _trackRect = GetTemplateChild(TrackRectPartName) as FrameworkElement;

        if (_decreaseRect?.Parent is not Panel track)
        {
            _decreaseRect = null;
            _trackRect = null;
            return;
        }

        // The framework already sizes this rectangle to the elapsed width, so it is kept for its
        // layout maths and hidden in favour of the wave.
        _decreaseRect.Opacity = 0;
        _decreaseRect.SizeChanged += OnElapsedWidthChanged;

        _wave = CreateWavePath();
        track.Children.Add(_wave);

        if (_trackRect is not null)
        {
            _trackRect.Clip = _trackClip;
            _trackRect.SizeChanged += OnElapsedWidthChanged;
        }

        AttachThumbBacking(track);
        UpdateWave();
    }

    private Path CreateWavePath()
    {
        Path path = new()
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            StrokeThickness = WaveThickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Height = WaveBandHeight,
            Clip = _waveClip
        };

        path.SetBinding(Shape.StrokeProperty, new Binding
        {
            Path = new PropertyPath(nameof(Foreground)),
            Source = this
        });

        // Spanning every row and column keeps the wave off the grid's auto-sizing, exactly as the
        // thumb does, so adding it cannot change the slider's height or the thumb's travel.
        Grid.SetRow(path, 0);
        Grid.SetRowSpan(path, 3);
        Grid.SetColumn(path, 0);
        Grid.SetColumnSpan(path, 3);

        return path;
    }

    /// <summary>
    /// Adds the two hover layers around the thumb: a ring behind it and a fill covering it, both
    /// revealed while the pointer is over the slider.
    /// </summary>
    private void AttachThumbBacking(Panel track)
    {
        _thumb = GetTemplateChild(ThumbPartName) as FrameworkElement;

        if (_thumb is null)
            return;

        int thumbIndex = track.Children.IndexOf(_thumb);

        if (thumbIndex < 0)
        {
            _thumb = null;
            return;
        }

        _thumbRing = CreateThumbLayer(_thumb);
        _thumbFill = CreateThumbLayer(_thumb);

        if (ThumbHoverRingBrush is not null)
            _thumbRing.Background = ThumbHoverRingBrush;
        else
            _thumbRing.SetBinding(Border.BackgroundProperty, new Binding { Path = new PropertyPath(nameof(Foreground)), Source = this });

        _thumbFill.Background = ThumbHoverFillBrush ?? DefaultThumbHoverFillBrush;

        // The ring goes behind the thumb and the fill covers it, so the thumb's own colours are
        // swapped on hover without depending on how its template is built internally.
        track.Children.Insert(thumbIndex, _thumbRing);
        track.Children.Insert(thumbIndex + 2, _thumbFill);

        _thumb.SizeChanged += OnThumbSizeChanged;
        UpdateThumbBackingSize();

        PointerEntered += OnPointerEnteredSlider;
        PointerExited += OnPointerExitedSlider;
        PointerCaptureLost += OnPointerCaptureLostSlider;
    }

    private static Border CreateThumbLayer(FrameworkElement thumb)
    {
        Border layer = new()
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0
        };

        // The thumb lives in column 1, whose offset the framework drives, so anything placed in
        // that column follows the thumb horizontally for free.
        Grid.SetRow(layer, Grid.GetRow(thumb));
        Grid.SetRowSpan(layer, Grid.GetRowSpan(thumb));
        Grid.SetColumn(layer, Grid.GetColumn(thumb));

        return layer;
    }

    private void OnPointerEnteredSlider(object sender, PointerRoutedEventArgs args) => SetThumbHighlighted(true);

    private void OnPointerExitedSlider(object sender, PointerRoutedEventArgs args) => SetThumbHighlighted(false);

    private void OnPointerCaptureLostSlider(object sender, PointerRoutedEventArgs args) => SetThumbHighlighted(false);

    private void OnThumbSizeChanged(object sender, SizeChangedEventArgs args) => UpdateThumbBackingSize();

    private void UpdateThumbBackingSize()
    {
        if (_thumb is null)
            return;

        double outerWidth = _thumb.ActualWidth + (2 * ThumbHoverInflate);
        double outerHeight = _thumb.ActualHeight + (2 * ThumbHoverInflate);
        double band = 2 * ThumbHoverRingThickness;

        Resize(_thumbRing, outerWidth, outerHeight);
        Resize(_thumbFill, Math.Max(0, outerWidth - band), Math.Max(0, outerHeight - band));
    }

    private static void Resize(Border? layer, double width, double height)
    {
        if (layer is null)
            return;

        layer.Width = width;
        layer.Height = height;
        layer.CornerRadius = new CornerRadius(width / 2);
    }

    private void SetThumbHighlighted(bool highlighted)
    {
        double opacity = highlighted ? 1 : 0;

        if (_thumbRing is not null)
            _thumbRing.Opacity = opacity;

        if (_thumbFill is not null)
            _thumbFill.Opacity = opacity;
    }

    private double WaveBandHeight => (2 * WaveAmplitude) + WaveThickness;

    private static void OnWaveShapeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        WaveSlider slider = (WaveSlider)sender;

        if (slider._wave is null)
            return;

        slider._wave.StrokeThickness = slider.WaveThickness;
        slider._wave.Height = slider.WaveBandHeight;
        slider._renderedWaveWidth = -1;

        slider.UpdateWave();
    }

    private void OnElapsedWidthChanged(object sender, SizeChangedEventArgs args) => UpdateWave();

    private void UpdateWave()
    {
        if (_wave is null || _decreaseRect is null)
            return;

        double elapsedWidth = _decreaseRect.ActualWidth;
        double bandHeight = WaveBandHeight;

        double fullWidth = _trackRect?.ActualWidth ?? elapsedWidth;

        if (fullWidth > _renderedWaveWidth)
        {
            _wave.Data = BuildWave(fullWidth, bandHeight / 2);
            _renderedWaveWidth = fullWidth;
        }

        _waveClip.Rect = new Rect(0, 0, elapsedWidth, bandHeight);

        if (_trackRect is not null)
            _trackClip.Rect = new Rect(elapsedWidth, 0, Math.Max(0, fullWidth - elapsedWidth), _trackRect.ActualHeight);
    }

    private Geometry BuildWave(double width, double axis)
    {
        PolyLineSegment segment = new();

        for (double x = SampleStep; x <= width; x += SampleStep)
            segment.Points.Add(new Point(x, axis - (WaveAmplitude * Math.Sin(2 * Math.PI * x / WaveLength))));

        PathFigure figure = new()
        {
            StartPoint = new Point(0, axis),
            IsClosed = false,
            IsFilled = false
        };

        figure.Segments.Add(segment);

        PathGeometry geometry = new();
        geometry.Figures.Add(figure);

        return geometry;
    }

    private void DetachParts()
    {
        PointerEntered -= OnPointerEnteredSlider;
        PointerExited -= OnPointerExitedSlider;
        PointerCaptureLost -= OnPointerCaptureLostSlider;

        if (_thumb is not null)
        {
            _thumb.SizeChanged -= OnThumbSizeChanged;
            _thumb = null;
        }

        if (_thumbRing?.Parent is Panel ringParent)
            ringParent.Children.Remove(_thumbRing);

        if (_thumbFill?.Parent is Panel fillParent)
            fillParent.Children.Remove(_thumbFill);

        _thumbRing = null;
        _thumbFill = null;

        if (_decreaseRect is not null)
        {
            _decreaseRect.SizeChanged -= OnElapsedWidthChanged;
            _decreaseRect.Opacity = 1;
        }

        if (_trackRect is not null)
        {
            _trackRect.SizeChanged -= OnElapsedWidthChanged;
            _trackRect.Clip = null;
        }

        if (_wave?.Parent is Panel track)
            track.Children.Remove(_wave);

        _decreaseRect = null;
        _trackRect = null;
        _wave = null;
        _renderedWaveWidth = -1;
    }
}