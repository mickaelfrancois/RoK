using CleanArch.DevKit.Guards;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.Import.Services;

/// <summary>
/// Outcome of a <see cref="LibraryMetadataRefresher"/> run.
/// </summary>
public sealed record LibraryMetadataRefreshReport
{
    public bool Applied { get; init; }

    public int TracksScanned { get; init; }

    public int FilesMissing { get; init; }

    public int TracksUpdated { get; init; }

    public int AlbumsUpdated { get; init; }

    public int LyricsSidecarsCreated { get; init; }
}

/// <summary>
/// Re-reads the audio tag of every track already in the database and refreshes the
/// extended metadata columns introduced by the import-enrichment work, plus the album
/// MusicBrainz id. Iterates over database tracks (never discovers new files) and also
/// replays the embedded-lyrics sidecar extraction.
///
/// Write policy: a value is written only when the tag actually provides one
/// (non-null / non-empty / non-zero); an existing database value is never blanked.
/// When <c>apply</c> is <see langword="false"/> the run is a dry-run that only counts
/// what would change.
/// </summary>
public class LibraryMetadataRefresher(
    ITrackRepository trackRepository,
    IAlbumRepository albumRepository,
    ITagService tagService,
    EmbeddedLyricsImporter embeddedLyricsImporter,
    IFileSystem fileSystem,
    ILogger<LibraryMetadataRefresher> logger)
{
    public async Task<LibraryMetadataRefreshReport> RefreshAsync(bool apply, CancellationToken cancellationToken = default)
    {
        int scanned = 0;
        int missing = 0;
        int tracksUpdated = 0;
        int albumsUpdated = 0;
        int lyricsCreated = 0;

        HashSet<long> processedAlbums = [];

        IEnumerable<TrackEntity> tracks = await trackRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (TrackEntity track in tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;

            if (string.IsNullOrEmpty(track.MusicFile) || !fileSystem.FileExists(track.MusicFile))
            {
                missing++;
                continue;
            }

            TrackFile file = new() { FullPath = track.MusicFile };

            try
            {
                tagService.FillProperties(track.MusicFile, file);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read tags from '{File}' - skipped", track.MusicFile);
                continue;
            }

            if (ApplyTrackChanges(track, file))
            {
                tracksUpdated++;

                if (apply)
                    await trackRepository.UpdateAsync(track, RepositoryConnectionKind.Background);
            }

            if (track.AlbumId is long albumId && processedAlbums.Add(albumId) && await RefreshAlbumAsync(albumId, file, apply))
                albumsUpdated++;

            if (apply ? await embeddedLyricsImporter.ExtractAsync(file, cancellationToken) : embeddedLyricsImporter.WouldWriteSidecar(file))
                lyricsCreated++;
        }

        return new LibraryMetadataRefreshReport
        {
            Applied = apply,
            TracksScanned = scanned,
            FilesMissing = missing,
            TracksUpdated = tracksUpdated,
            AlbumsUpdated = albumsUpdated,
            LyricsSidecarsCreated = lyricsCreated
        };
    }

    private async Task<bool> RefreshAlbumAsync(long albumId, TrackFile file, bool apply)
    {
        AlbumEntity? album = await albumRepository.GetByIdAsync(albumId, RepositoryConnectionKind.Background);

        if (album is null)
            return false;

        bool changed = false;

        changed |= SetNullableInt(file.DiscCount, album.DiscCount, value => album.DiscCount = value);
        changed |= SetNullableDouble(file.ReplayGainAlbumGain, album.ReplayGainAlbumGain, value => album.ReplayGainAlbumGain = value);
        changed |= SetNullableDouble(file.ReplayGainAlbumPeak, album.ReplayGainAlbumPeak, value => album.ReplayGainAlbumPeak = value);
        changed |= SetText(file.MusicBrainzReleaseType, album.MusicBrainzReleaseType, value => album.MusicBrainzReleaseType = value);
        changed |= SetText(file.MusicBrainzReleaseCountry, album.MusicBrainzReleaseCountry, value => album.MusicBrainzReleaseCountry = value);
        changed |= SetText(file.MusicbrainzAlbumID, album.MusicBrainzID, value => album.MusicBrainzID = value);

        if (changed && apply)
            await albumRepository.UpdateAsync(album, RepositoryConnectionKind.Background);

        return changed;
    }

    private static bool ApplyTrackChanges(TrackEntity track, TrackFile file)
    {
        bool changed = false;

        changed |= SetNullableInt(file.Disc, track.Disc, value => track.Disc = value);
        changed |= SetNullableInt(file.Bpm, track.Bpm, value => track.Bpm = value);
        changed |= SetText(file.Composers, track.Composers, value => track.Composers = value);
        changed |= SetPositiveInt(file.SampleRate, track.SampleRate, value => track.SampleRate = value);
        changed |= SetPositiveInt(file.BitsPerSample, track.BitsPerSample, value => track.BitsPerSample = value);
        changed |= SetPositiveInt(file.Channels, track.Channels, value => track.Channels = value);
        changed |= SetNullableDouble(file.ReplayGainTrackGain, track.ReplayGainTrackGain, value => track.ReplayGainTrackGain = value);
        changed |= SetNullableDouble(file.ReplayGainTrackPeak, track.ReplayGainTrackPeak, value => track.ReplayGainTrackPeak = value);

        return changed;
    }

    private static bool SetNullableInt(int? tagValue, int? current, Action<int?> setter)
    {
        if (!tagValue.HasValue || tagValue == current)
            return false;

        setter(tagValue);
        return true;
    }

    private static bool SetPositiveInt(int tagValue, int current, Action<int> setter)
    {
        if (tagValue <= 0 || tagValue == current)
            return false;

        setter(tagValue);
        return true;
    }

    private static bool SetNullableDouble(double? tagValue, double? current, Action<double?> setter)
    {
        if (!tagValue.HasValue || tagValue == current)
            return false;

        setter(tagValue);
        return true;
    }

    private static bool SetText(string? tagValue, string? current, Action<string?> setter)
    {
        if (string.IsNullOrEmpty(tagValue) || tagValue == current)
            return false;

        setter(tagValue);
        return true;
    }
}
