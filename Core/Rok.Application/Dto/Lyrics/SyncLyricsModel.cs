using System.Collections.ObjectModel;

namespace Rok.Application.Dto.Lyrics;

public class SyncLyricsModel
{
    public List<TimeSpan> Time { get; private set; } = [];

    public ObservableCollection<LyricLine> Lyrics { get; set; } = [];
}
