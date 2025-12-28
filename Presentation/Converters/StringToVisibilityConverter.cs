namespace Rok.Converters;

public partial class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string? text = value?.ToString();

        bool isVisible = !string.IsNullOrWhiteSpace(text);

        if (InvertVisibility)
            isVisible = !isVisible;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }


    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return "";
    }


    public bool InvertVisibility { get; set; }
}
