using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Rok.Commons;

/// <summary>
/// A <see cref="Slider"/> that draws its elapsed portion as a wave instead of a solid bar,
/// leaving the remaining portion as a flat line.
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

    private const double SampleStep = 1.5;

    public static readonly DependencyProperty WaveAmplitudeProperty =
        DependencyProperty.Register(nameof(WaveAmplitude), typeof(double), typeof(WaveSlider), new PropertyMetadata(3.0, OnWaveShapeChanged));

    public static readonly DependencyProperty WaveLengthProperty =
        DependencyProperty.Register(nameof(WaveLength), typeof(double), typeof(WaveSlider), new PropertyMetadata(14.0, OnWaveShapeChanged));

    public static readonly DependencyProperty WaveThicknessProperty =
        DependencyProperty.Register(nameof(WaveThickness), typeof(double), typeof(WaveSlider), new PropertyMetadata(3.0, OnWaveShapeChanged));

    private readonly RectangleGeometry _waveClip = new();
    private readonly RectangleGeometry _trackClip = new();

    private Path? _wave;
    private FrameworkElement? _decreaseRect;
    private FrameworkElement? _trackRect;
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