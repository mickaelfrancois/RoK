using Microsoft.UI;


namespace Rok.Converters;

public sealed partial class BoolToColorConverter : IValueConverter
{
    public SolidColorBrush TrueBrush { get; set; } = new SolidColorBrush(Colors.Green);
    public SolidColorBrush FalseBrush { get; set; } = new SolidColorBrush(Colors.Red);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool flag = value is bool b && b;

        return flag
            ? (TrueBrush ?? new SolidColorBrush(Colors.Green))
            : (FalseBrush ?? new SolidColorBrush(Colors.Red));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
