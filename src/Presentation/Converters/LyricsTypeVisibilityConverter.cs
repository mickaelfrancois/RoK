using Rok.Application.Dto.Lyrics;

namespace Rok.Converters;

public partial class LyricsTypeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ELyricsType lyricsType || parameter is not string target)
            return Visibility.Collapsed;

        return target switch
        {
            "Synchronized" => lyricsType == ELyricsType.Synchronized ? Visibility.Visible : Visibility.Collapsed,
            "Plain"        => lyricsType == ELyricsType.Plain        ? Visibility.Visible : Visibility.Collapsed,
            _              => Visibility.Collapsed
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
