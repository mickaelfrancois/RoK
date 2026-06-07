using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Rok.ViewModels.Common;

namespace Rok.Commons;

public sealed partial class ListeningStatsPanel : UserControl
{
    public ListeningStatsPanel()
    {
        this.InitializeComponent();
    }

    public static readonly DependencyProperty StatsProperty =
        DependencyProperty.Register(nameof(Stats), typeof(ListeningStatsViewModel), typeof(ListeningStatsPanel), new PropertyMetadata(null));

    public ListeningStatsViewModel Stats
    {
        get => (ListeningStatsViewModel)GetValue(StatsProperty);
        set => SetValue(StatsProperty, value);
    }

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(ListeningStatsPanel), new PropertyMetadata(null));

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public static readonly DependencyProperty ProgressionUnitTextProperty =
        DependencyProperty.Register(nameof(ProgressionUnitText), typeof(string), typeof(ListeningStatsPanel), new PropertyMetadata(string.Empty));

    public string ProgressionUnitText
    {
        get => (string)GetValue(ProgressionUnitTextProperty);
        set => SetValue(ProgressionUnitTextProperty, value);
    }
}
