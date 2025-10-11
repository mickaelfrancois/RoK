namespace Rok.Application.Tag;

public class TrackFile
{
    public string UID { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string Album { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public int? TrackNumber { get; set; }

    public bool IsLive { get; set; }

    public bool IsCompilation { get; set; }

    public string IsRemix { get; set; } = string.Empty;

    public int? Year { get; set; }

    public TimeSpan Duration { get; set; }

    public int Bitrate { get; set; }

    public DateTimeOffset FileDateModified { get; set; }

    public DateTimeOffset FileDateCreated { get; set; }

    public long Size { get; set; }

    public string FullPath { get; set; } = string.Empty;

    public bool IsVideo
    {
        get
        {
            string ext = global::System.IO.Path.GetExtension(FullPath);
            return ext == ".avi" || ext == ".mp4";
        }
    }

    public bool IsFlac
    {
        get
        {
            string ext = global::System.IO.Path.GetExtension(FullPath);
            return ext == ".flac";
        }
    }

    public string MusicbrainzArtistID { get; set; } = string.Empty;

    public string MusicbrainzAlbumID { get; set; } = string.Empty;

    public string MusicbrainzTrackID { get; set; } = string.Empty;

    public string Lyrics { get; set; } = string.Empty;
}
