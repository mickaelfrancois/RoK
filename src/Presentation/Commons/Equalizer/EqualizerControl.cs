using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Rok.ViewModels.Player;
using Windows.Foundation;
using Windows.UI;

namespace Rok.Commons.Equalizer;

public sealed class EqualizerControl : UserControl
{
    private const int BandCount = 10;
    private const double SliderHeight = 260.0;
    private const double ThumbPadding = 16.0;
    private const double YAxisWidth = 46.0;
    private const double BandMin = -15.0;
    private const double BandMax = 15.0;

    private static readonly double[] DbLabels = { 15, 10, 5, 0, -5, -10, -15 };
    private static readonly string[] Frequencies = { "32Hz", "64Hz", "125Hz", "250Hz", "500Hz", "1kHz", "2kHz", "4kHz", "8kHz", "16kHz" };

    private EqualizerViewModel? _viewModel;
    private readonly Slider[] _sliders = new Slider[BandCount];
    private readonly bool[] _suppressValueChanged = new bool[BandCount];
    private readonly Line[] _gridLines = new Line[7];
    private Path? _fillPath;
    private Path? _linePath;
    private Canvas? _curveCanvas;

    public EqualizerViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            if (_viewModel != null)
                BindViewModel();
        }
    }

    public EqualizerControl()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        Grid mainGrid = new();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(YAxisWidth) });

        Grid overlayGrid = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(overlayGrid, 0);
        Grid.SetColumn(overlayGrid, 0);

        _curveCanvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = SliderHeight,
            IsHitTestVisible = false
        };
        _curveCanvas.SizeChanged += (_, _) => UpdateCurve();

        for (int i = 0; i < DbLabels.Length; i++)
        {
            bool isZero = DbLabels[i] == 0;
            Line line = new()
            {
                Stroke = new SolidColorBrush(Color.FromArgb(isZero ? (byte)100 : (byte)35, 128, 128, 128)),
                StrokeThickness = isZero ? 1.5 : 1,
                IsHitTestVisible = false
            };
            if (!isZero)
                line.StrokeDashArray = new DoubleCollection { 3, 4 };
            _gridLines[i] = line;
            _curveCanvas.Children.Add(line);
        }

        _fillPath = new Path
        {
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops = new GradientStopCollection
                {
                    new() { Color = Color.FromArgb(120, 0, 200, 175), Offset = 0 },
                    new() { Color = Color.FromArgb(20,  0, 180, 155), Offset = 1 }
                }
            },
            IsHitTestVisible = false
        };

        _linePath = new Path
        {
            Stroke = new SolidColorBrush(Color.FromArgb(230, 0, 210, 185)),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };

        _curveCanvas.Children.Add(_fillPath);
        _curveCanvas.Children.Add(_linePath);

        Grid slidersGrid = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = SliderHeight
        };

        for (int i = 0; i < BandCount; i++)
            slidersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < BandCount; i++)
        {
            Slider slider = new()
            {
                Orientation = Orientation.Vertical,
                Minimum = BandMin,
                Maximum = BandMax,
                Value = 0,
                Height = SliderHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsThumbToolTipEnabled = true
            };

            int idx = i;

            slider.ValueChanged += (_, _) => OnSliderChanged(idx);
            _sliders[i] = slider;

            Grid.SetColumn(slider, i);
            slidersGrid.Children.Add(slider);
        }

        overlayGrid.Children.Add(_curveCanvas);
        overlayGrid.Children.Add(slidersGrid);

        Canvas yAxis = BuildYAxis();
        Grid.SetRow(yAxis, 0);
        Grid.SetColumn(yAxis, 1);

        Grid labelsGrid = BuildFrequencyLabels();
        Grid.SetRow(labelsGrid, 2);
        Grid.SetColumn(labelsGrid, 0);

        mainGrid.Children.Add(overlayGrid);
        mainGrid.Children.Add(yAxis);
        mainGrid.Children.Add(labelsGrid);

        Content = mainGrid;
    }

    private void BindViewModel()
    {
        if (_viewModel == null)
            return;

        for (int i = 0; i < BandCount; i++)
        {
            _suppressValueChanged[i] = true;
            _sliders[i].Value = GetBandValue(i);
            _suppressValueChanged[i] = false;
        }

        _viewModel.PropertyChanged += (_, args) =>
        {
            for (int i = 0; i < BandCount; i++)
            {
                if (args.PropertyName != GetPropertyName(i))
                    continue;

                _suppressValueChanged[i] = true;
                _sliders[i].Value = GetBandValue(i);
                _suppressValueChanged[i] = false;

                UpdateCurve();

                return;
            }
        };

        UpdateCurve();
    }

    private void OnSliderChanged(int bandIndex)
    {
        if (_suppressValueChanged[bandIndex] || _viewModel == null)
            return;

        float value = (float)_sliders[bandIndex].Value;

        switch (bandIndex)
        {
            case 0: _viewModel.Band32Hz = value; break;
            case 1: _viewModel.Band64Hz = value; break;
            case 2: _viewModel.Band125Hz = value; break;
            case 3: _viewModel.Band250Hz = value; break;
            case 4: _viewModel.Band500Hz = value; break;
            case 5: _viewModel.Band1kHz = value; break;
            case 6: _viewModel.Band2kHz = value; break;
            case 7: _viewModel.Band4kHz = value; break;
            case 8: _viewModel.Band8kHz = value; break;
            case 9: _viewModel.Band16kHz = value; break;
        }

        UpdateCurve();
    }

    private void UpdateCurve()
    {
        if (_curveCanvas is null || _fillPath is null || _linePath is null)
            return;

        double w = _curveCanvas.ActualWidth;
        double h = _curveCanvas.ActualHeight;

        if (w <= 0 || h <= 0)
            return;

        for (int i = 0; i < DbLabels.Length; i++)
        {
            double y = DbToY(DbLabels[i], h);
            _gridLines[i].X1 = 0;
            _gridLines[i].X2 = w;
            _gridLines[i].Y1 = y;
            _gridLines[i].Y2 = y;
        }

        double colW = w / BandCount;
        Point[] pts = new Point[BandCount];

        for (int i = 0; i < BandCount; i++)
            pts[i] = new Point(colW * (i + 0.5), DbToY(_sliders[i].Value, h));

        _linePath.Data = BuildLinePath(pts);
        _fillPath.Data = BuildFillPath(pts, DbToY(0, h));
    }

    private static double DbToY(double db, double height)
    {
        double ratio = (BandMax - db) / (BandMax - BandMin);
        return ThumbPadding + (ratio * (height - (2 * ThumbPadding)));
    }

    private static PathGeometry BuildLinePath(Point[] pts)
    {
        double[] tangents = ComputeMonotoneTangents(pts);
        PathGeometry geo = new();
        PathFigure fig = new() { StartPoint = pts[0], IsFilled = false, IsClosed = false };
        PolyBezierSegment seg = new();

        for (int i = 0; i < pts.Length - 1; i++)
        {
            double h = pts[i + 1].X - pts[i].X;
            seg.Points.Add(new Point(pts[i].X + (h / 3.0), pts[i].Y + (h / 3.0 * tangents[i])));
            seg.Points.Add(new Point(pts[i + 1].X - (h / 3.0), pts[i + 1].Y - (h / 3.0 * tangents[i + 1])));
            seg.Points.Add(pts[i + 1]);
        }

        fig.Segments.Add(seg);
        geo.Figures.Add(fig);

        return geo;
    }

    private static PathGeometry BuildFillPath(Point[] pts, double zeroY)
    {
        double[] tangents = ComputeMonotoneTangents(pts);
        PathGeometry geo = new();
        PathFigure fig = new()
        {
            StartPoint = new Point(pts[0].X, zeroY),
            IsFilled = true,
            IsClosed = true
        };

        fig.Segments.Add(new LineSegment { Point = pts[0] });

        PolyBezierSegment seg = new();

        for (int i = 0; i < pts.Length - 1; i++)
        {
            double h = pts[i + 1].X - pts[i].X;
            seg.Points.Add(new Point(pts[i].X + (h / 3.0), pts[i].Y + (h / 3.0 * tangents[i])));
            seg.Points.Add(new Point(pts[i + 1].X - (h / 3.0), pts[i + 1].Y - (h / 3.0 * tangents[i + 1])));
            seg.Points.Add(pts[i + 1]);
        }

        fig.Segments.Add(seg);
        fig.Segments.Add(new LineSegment { Point = new Point(pts[^1].X, zeroY) });
        geo.Figures.Add(fig);

        return geo;
    }

    private static double[] ComputeMonotoneTangents(Point[] pts)
    {
        int n = pts.Length;
        double[] m = new double[n];
        double[] delta = new double[n - 1];

        for (int i = 0; i < n - 1; i++)
            delta[i] = (pts[i + 1].Y - pts[i].Y) / (pts[i + 1].X - pts[i].X);

        m[0] = delta[0];
        for (int i = 1; i < n - 1; i++)
            m[i] = (delta[i - 1] + delta[i]) / 2.0;

        m[n - 1] = delta[n - 2];

        for (int i = 0; i < n - 1; i++)
        {
            if (Math.Abs(delta[i]) < 1e-10)
            {
                m[i] = 0;
                m[i + 1] = 0;
            }
            else
            {
                double alpha = m[i] / delta[i];
                double beta = m[i + 1] / delta[i];
                double rho = (alpha * alpha) + (beta * beta);

                if (rho > 9)
                {
                    double tau = 3.0 / Math.Sqrt(rho);
                    m[i] = tau * alpha * delta[i];
                    m[i + 1] = tau * beta * delta[i];
                }
            }
        }

        return m;
    }

    private static Canvas BuildYAxis()
    {
        Canvas canvas = new() { Width = YAxisWidth, Height = SliderHeight };

        foreach (double db in DbLabels)
        {
            double y = DbToY(db, SliderHeight);

            string label = db.EqualsZero() ? "0" : db > 0 ? $"+{(int)db}" : $"{(int)db}";

            TextBlock text = new()
            {
                Text = label,
                FontSize = 10,
                Opacity = db.EqualsZero() ? 1.0 : 0.6
            };

            Canvas.SetLeft(text, 8);
            Canvas.SetTop(text, y - 7);
            canvas.Children.Add(text);

            Line tick = new()
            {
                X1 = 0,
                X2 = 5,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
                StrokeThickness = 1
            };

            canvas.Children.Add(tick);
        }
        return canvas;
    }

    private static Grid BuildFrequencyLabels()
    {
        Grid grid = new();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < BandCount; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Rectangle axisLine = new()
        {
            Height = 1,
            Fill = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(axisLine, 0);
        Grid.SetColumnSpan(axisLine, BandCount);
        grid.Children.Add(axisLine);

        for (int i = 0; i < BandCount; i++)
        {
            Rectangle tick = new()
            {
                Width = 1,
                Height = 5,
                Fill = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 0, 0)
            };
            Grid.SetRow(tick, 0);
            Grid.SetColumn(tick, i);
            grid.Children.Add(tick);

            TextBlock label = new()
            {
                Text = Frequencies[i],
                FontSize = 10,
                Opacity = 0.65,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            Grid.SetRow(label, 1);
            Grid.SetColumn(label, i);
            grid.Children.Add(label);
        }

        return grid;
    }

    private float GetBandValue(int index) => index switch
    {
        0 => _viewModel!.Band32Hz,
        1 => _viewModel!.Band64Hz,
        2 => _viewModel!.Band125Hz,
        3 => _viewModel!.Band250Hz,
        4 => _viewModel!.Band500Hz,
        5 => _viewModel!.Band1kHz,
        6 => _viewModel!.Band2kHz,
        7 => _viewModel!.Band4kHz,
        8 => _viewModel!.Band8kHz,
        9 => _viewModel!.Band16kHz,
        _ => 0
    };

    private static string GetPropertyName(int index) => index switch
    {
        0 => nameof(EqualizerViewModel.Band32Hz),
        1 => nameof(EqualizerViewModel.Band64Hz),
        2 => nameof(EqualizerViewModel.Band125Hz),
        3 => nameof(EqualizerViewModel.Band250Hz),
        4 => nameof(EqualizerViewModel.Band500Hz),
        5 => nameof(EqualizerViewModel.Band1kHz),
        6 => nameof(EqualizerViewModel.Band2kHz),
        7 => nameof(EqualizerViewModel.Band4kHz),
        8 => nameof(EqualizerViewModel.Band8kHz),
        9 => nameof(EqualizerViewModel.Band16kHz),
        _ => string.Empty
    };
}