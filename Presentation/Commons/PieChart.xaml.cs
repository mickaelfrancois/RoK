using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Rok.Application.Features.Statistics;
using Windows.Foundation;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;


namespace Rok.Commons;

public sealed partial class PieChart : UserControl
{
    public PieChart()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => Redraw();
    }

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable<NamedCount>), typeof(PieChart),
            new PropertyMetadata(null, (d, e) => ((PieChart)d).Redraw()));


    public IEnumerable<NamedCount>? Items
    {
        get => (IEnumerable<NamedCount>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PieChart),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static readonly Brush[] Palette = new Brush[]
    {
        new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50)), // green
        new SolidColorBrush(Color.FromArgb(0xFF, 0x21, 0x96, 0xF3)), // blue
        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xC1, 0x07)), // amber
        new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x57, 0x22)), // deep orange
        new SolidColorBrush(Color.FromArgb(0xFF, 0x9C, 0x27, 0xB0)), // purple
        new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x96, 0x88)), // teal
        new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0x3A, 0x2C)), // red
        new SolidColorBrush(Color.FromArgb(0xFF, 0x7C, 0x4D, 0xFF)), // indigo-ish
    };

    private void Redraw()
    {
        Canvas? canvas = PartCanvas;
        StackPanel? legend = PartLegend;

        if (canvas is null || legend is null)
            return;

        canvas.Children.Clear();
        legend.Children.Clear();

        List<NamedCount> items = (Items ?? Enumerable.Empty<NamedCount>()).Where(i => i != null && i.Count > 0).OrderByDescending(c => c.Count).ToList();
        double total = items.Sum(i => (double)i.Count);

        double width = canvas.ActualWidth > 0 ? canvas.ActualWidth : canvas.Width;
        double height = canvas.ActualHeight > 0 ? canvas.ActualHeight : canvas.Height;
        double size = Math.Min(width, height);
        double cx = size / 2.0;
        double cy = size / 2.0;
        double radius = size / 2.0;

        if (total <= 0 || !items.Any())
        {
            // draw empty circle
            Ellipse empty = new()
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)),
                Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(empty, 0);
            Canvas.SetTop(empty, 0);
            canvas.Children.Add(empty);
            legend.Children.Add(new TextBlock { Text = "No data", Foreground = new SolidColorBrush(Colors.Gray) });
            return;
        }

        double startAngle = -90.0; // start at top

        for (int i = 0; i < items.Count; i++)
        {
            NamedCount it = items[i];
            double sweep = it.Count / total * 360.0;
            double endAngle = startAngle + sweep;

            // compute percent early for tooltip and legend
            double percent = it.Count / total * 100.0;

            // compute points
            Point startPoint = PointOnCircle(cx, cy, radius, startAngle);
            Point endPoint = PointOnCircle(cx, cy, radius, endAngle);

            bool isLargeArc = sweep > 180.0;

            // build geometry: move to center, line to startPoint, arc to endPoint, line back to center (closed)
            PathFigure pf = new() { StartPoint = new Point(cx, cy), IsClosed = true };
            pf.Segments.Add(new LineSegment { Point = startPoint });
            pf.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Clockwise,
                RotationAngle = 0
            });
            pf.Segments.Add(new LineSegment { Point = new Point(cx, cy) });

            PathGeometry pg = new();
            pg.Figures.Add(pf);

            Path path = new()
            {
                Data = pg,
                Fill = Palette[i % Palette.Length],
                Stroke = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x00, 0x00)),
                StrokeThickness = 0.5
            };

            // set tooltip showing type, count and percent
            string tipText = $"{it.Name} — {it.Count} ({percent:0.#}%)";
            ToolTipService.SetToolTip(path, tipText);

            canvas.Children.Add(path);

            // legend row
            StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
            Rectangle rect = new() { Width = 14, Height = 14, Fill = Palette[i % Palette.Length] };
            TextBlock txt = new() { Text = $"{it.Name} ({it.Count}) — {percent:0.#}%", VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(rect);
            row.Children.Add(txt);
            legend.Children.Add(row);

            startAngle = endAngle;
        }
    }

    private static Point PointOnCircle(double cx, double cy, double r, double angleDegrees)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        double x = cx + (r * Math.Cos(rad));
        double y = cy + (r * Math.Sin(rad));

        return new Point(x, y);
    }
}