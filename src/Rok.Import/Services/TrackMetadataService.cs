using Microsoft.Extensions.Logging;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Shared.Extensions;

namespace Rok.Import.Services;

public class TrackMetadataService(TrackImport importTrack, ILogger<TrackMetadataService> logger)
{
    public async Task<bool> ShouldUpdateMetadataAsync(TrackFile file, TrackEntity? track)
    {
        if (track == null)
            return true;

        if (!AreTrackAndFileEquals(track, file))
            return true;

        DateTime trackDateTimeUtc = track.FileDate.ToUniversalTime().TruncateToMinutes();
        DateTime fileDateUtcTrunc = file.FileDateModified.UtcDateTime.TruncateToMinutes();

        if (trackDateTimeUtc == fileDateUtcTrunc)
            return false;

        if (fileDateUtcTrunc > trackDateTimeUtc)
            await UpdateTrackFileDateAsync(track, file.FileDateModified.DateTime);
        else
            logger.LogTrace("Database file date is newer for track id {Id} (db:{Db} file:{File}) - no update performed", track.Id, trackDateTimeUtc, fileDateUtcTrunc);

        return false;
    }

    public static bool AreTrackAndFileEquals(TrackEntity track, TrackFile trackFile)
    {
        if (trackFile.Artist.IsDifferent(track.ArtistName))
            return false;

        if (trackFile.Album.IsDifferent(track.AlbumName))
            return false;

        if (trackFile.Genre.IsDifferent(track.GenreName))
            return false;

        if (trackFile.Title.IsDifferent(track.Title))
            return false;

        if (trackFile.Size != track.Size)
            return false;

        if (trackFile.TrackNumber != track.TrackNumber)
            return false;

        return true;
    }

    public static void EnsureTrackTimestamps(TrackEntity track, TrackFile file)
    {
        if (track.Id == 0)
        {
            track.MusicFile = Path.GetFullPath(file.FullPath);
            track.CreatDate = DateTime.Now;
            track.EditDate = null;
        }
        else
        {
            track.EditDate = DateTime.Now;
        }
    }

    public static void FillTrackEntity(TrackEntity track, TrackFile file, long? artistId, long? albumId, long? genreId)
    {
        track.ArtistId = artistId;
        track.AlbumId = albumId;
        track.GenreId = genreId;
        track.Title = file.Title;
        track.Size = file.Size;
        track.Bitrate = file.Bitrate;
        track.TrackNumber = file.TrackNumber;
        track.Duration = (long)Math.Round(file.Duration.TotalSeconds, 0);
        track.FileDate = file.FileDateModified.DateTime;
    }

    private async Task UpdateTrackFileDateAsync(TrackEntity track, DateTime fileDate)
    {
        try
        {
            await importTrack.UpdateTrackFileDateAsync(track.Id, fileDate).ConfigureAwait(false);

            logger.LogInformation("Updated file date for track id {Id} to {Date} (file '{File}')", track.Id, fileDate, track.MusicFile);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update file date for track id {Id} (file '{File}')", track.Id, track.MusicFile);
        }
    }
}