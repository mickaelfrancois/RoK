using Microsoft.UI.Xaml.Controls;
using Rok.Application.Player;

namespace Rok.Converters;

public partial class PlaybackStateToButtonIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is EPlaybackState)
        {
            EPlaybackState state = (EPlaybackState)value;

            if (state == EPlaybackState.Playing)
                return Symbol.Pause;
            else
                return Symbol.Play;
        }

        return Symbol.Play;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
