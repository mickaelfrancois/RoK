using Microsoft.UI.Xaml.Controls;
using Rok.Application.Player;

namespace Rok.Converters;

public partial class RepeatModeToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ERepeatMode.One)
            return Symbol.RepeatOne;

        return Symbol.RepeatAll;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}