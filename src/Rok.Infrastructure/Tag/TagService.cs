using Microsoft.Extensions.Logging;
using Rok.Application.Tag;
using Rok.Shared.Extensions;

namespace Rok.Infrastructure.Tag;

public class TagService(ILogger<TagService> logger) : ITagService
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
        try
        {
            using TagLib.File tag = TagLib.File.Create(file);

            track.Title = tag.Tag.Title?.Trim() ?? "";
            track.Artist = (tag.Tag.FirstAlbumArtist?.Trim() ?? tag.Tag.FirstPerformer?.Trim() ?? "").NormalizeIndexedName();
            track.Album = (tag.Tag.Album?.Trim() ?? "").NormalizeIndexedName();
            track.Genre = (tag.Tag.FirstGenre?.Trim() ?? "").NormalizeIndexedName();
            track.Year = tag.Tag.Year > 0 ? (int)tag.Tag.Year : null;
            track.TrackNumber = (int)tag.Tag.Track;
            track.Duration = tag.Properties.Duration;
            track.Bitrate = tag.Properties.AudioBitrate * 1000;

            track.MusicbrainzAlbumID = tag.Tag.MusicBrainzReleaseId;
            track.MusicbrainzArtistID = tag.Tag.MusicBrainzArtistId;
            track.MusicbrainzTrackID = tag.Tag.MusicBrainzTrackId;

            track.Lyrics = tag.Tag.Lyrics;

            track.Disc = tag.Tag.Disc > 0 ? (int)tag.Tag.Disc : null;
            track.DiscCount = tag.Tag.DiscCount > 0 ? (int)tag.Tag.DiscCount : null;
            track.Bpm = tag.Tag.BeatsPerMinute > 0 ? (int)tag.Tag.BeatsPerMinute : null;
            track.Composers = tag.Tag.JoinedComposers?.Trim() ?? "";

            track.SampleRate = tag.Properties.AudioSampleRate;
            track.BitsPerSample = tag.Properties.BitsPerSample;
            track.Channels = tag.Properties.AudioChannels;

            track.ReplayGainTrackGain = double.IsNaN(tag.Tag.ReplayGainTrackGain) ? null : tag.Tag.ReplayGainTrackGain;
            track.ReplayGainTrackPeak = double.IsNaN(tag.Tag.ReplayGainTrackPeak) ? null : tag.Tag.ReplayGainTrackPeak;
            track.ReplayGainAlbumGain = double.IsNaN(tag.Tag.ReplayGainAlbumGain) ? null : tag.Tag.ReplayGainAlbumGain;
            track.ReplayGainAlbumPeak = double.IsNaN(tag.Tag.ReplayGainAlbumPeak) ? null : tag.Tag.ReplayGainAlbumPeak;

            track.MusicBrainzReleaseType = tag.Tag.MusicBrainzReleaseType?.Trim() ?? "";
            track.MusicBrainzReleaseCountry = tag.Tag.MusicBrainzReleaseCountry?.Trim() ?? "";
        }
        catch (TagLib.CorruptFileException ex)
        {
            logger.LogWarning(ex, "Corrupt tag data in file: {File}", file);
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            logger.LogWarning(ex, "Unsupported format for file: {File}", file);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error reading tags from file: {File}", file);
        }
    }


    public static async Task<bool> ExtractPictureToFileAsync(string inputFile, string outputFile)
    {
        using TagLib.File tag = TagLib.File.Create(inputFile);

        TagLib.IPicture? picture = tag.Tag.Pictures.FirstOrDefault(c => c.Type == TagLib.PictureType.FrontCover);

        if (picture != null)
        {
            await File.WriteAllBytesAsync(outputFile, picture.Data.Data);
            return true;
        }

        return false;
    }
}