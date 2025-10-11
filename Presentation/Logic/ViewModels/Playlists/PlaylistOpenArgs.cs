namespace Rok.Logic.ViewModels.Playlists;

public class PlaylistOpenArgs
{
    public long? PlaylistId { get; set; }

    public PlaylistOpenArgs()
    {
    }

    public PlaylistOpenArgs(long playlistId)
    {
        PlaylistId = playlistId;
    }
}
