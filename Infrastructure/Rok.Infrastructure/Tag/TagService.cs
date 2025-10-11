using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag;

public class TagService : ITagService
{
    public void FillProperties(string file, TrackFile track)
    {
        FillBasicProperties(file, track);
        FillMusicProperties(file, track);
    }


    public void FillBasicProperties(string file, TrackFile track)
    {
        FileInfo fileInfo = new(file);

        track.FullPath = fileInfo.FullName;
        track.FileDateCreated = fileInfo.CreationTimeUtc;
        track.FileDateModified = fileInfo.LastWriteTimeUtc;
        track.Size = fileInfo.Length;
    }


    public void FillMusicProperties(string file, TrackFile track)
    {
        using TagLib.File tag = TagLib.File.Create(file);

        track.Title = tag.Tag.Title?.Trim() ?? "";
        track.Artist = tag.Tag.FirstAlbumArtist?.Trim() ?? tag.Tag.FirstPerformer?.Trim() ?? "";
        track.Album = tag.Tag.Album?.Trim() ?? "";
        track.Genre = tag.Tag.FirstGenre?.Trim() ?? "";
        track.Year = tag.Tag.Year > 0 ? (int)tag.Tag.Year : null;
        track.TrackNumber = (int)tag.Tag.Track;
        track.Duration = tag.Properties.Duration;
        track.Bitrate = tag.Properties.AudioBitrate * 1000;

        track.MusicbrainzAlbumID = tag.Tag.MusicBrainzDiscId;
        track.MusicbrainzArtistID = tag.Tag.MusicBrainzArtistId;
        track.MusicbrainzTrackID = tag.Tag.MusicBrainzTrackId;

        track.Lyrics = tag.Tag.Lyrics;
    }


    public Task<bool> SaveTagAsync(string file, TrackFile track)
    {
        throw new NotImplementedException();
    }


    public static async Task<bool> ExtractPictureToFileAsync(string inputFile, string outputFile)
    {
        using TagLib.File tag = TagLib.File.Create(inputFile);

        TagLib.IPicture? picture = tag.Tag.Pictures.Where(c => c.Type == TagLib.PictureType.FrontCover).FirstOrDefault();

        if (picture != null)
        {
            await File.WriteAllBytesAsync(outputFile, picture.Data.Data);
            return true;
        }

        return false;
    }
}