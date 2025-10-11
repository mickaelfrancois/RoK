namespace Rok.Converters;

public partial class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Windows.UI.Color color ? new SolidColorBrush(color) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is SolidColorBrush brush ? brush.Color : DependencyProperty.UnsetValue;
}