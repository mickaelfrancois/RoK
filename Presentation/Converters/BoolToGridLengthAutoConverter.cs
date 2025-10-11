namespace Rok.Converters;

public partial class BoolToGridLengthAutoConverter : IValueConverter
{
    public GridLength TrueValue { get; set; } = GridLength.Auto;
    public GridLength FalseValue { get; set; } = new GridLength(0);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        bool flag = value is bool b && b;
        if (invert) flag = !flag;

        return flag ? TrueValue : FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is GridLength gl)
        {
            bool result = gl.IsAuto || (gl.GridUnitType == GridUnitType.Pixel && gl.Value > 0);
            bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);

            return invert ? !result : result;
        }

        return false;
    }
}