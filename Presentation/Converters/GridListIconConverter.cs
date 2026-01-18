using Microsoft.UI.Xaml.Controls;

namespace Rok.Converters;

public partial class GridListIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isGridView && isGridView)
        {
            return new SymbolIcon(Symbol.List);
        }

        return new SymbolIcon(Symbol.ViewAll);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}