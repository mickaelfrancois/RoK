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

    public int LyricsSidecarsFailed { get; init; }
}

/// <summary>
/// Progress emitted by a <see cref="LibraryMetadataRefresher"/> run. <paramref name="Phase"/>
/// is the current stage ("Tracks" or "Albums"), <paramref name="Processed"/> of
/// <paramref name="Total"/> items done.
/// </summary>
public sealed record LibraryMetadataRefreshProgress(string Phase, int Processed, int Total);

/// <summary>
/// Re-reads the audio tag of every track already in the database and refreshes the
/// extended metadata columns introduced by the import-enrichment work, plus the album
/// MusicBrainz id, and replays the embedded-lyrics sidecar extraction. Iterates over
/// database tracks only (never discovers new files).
///
/// Write policy (<see cref="MetadataWritePolicy.FillFromTag"/>): a value is written only
/// when the tag actually provides one; an existing database value is never blanked, but a
/// present-and-different tag value does overwrite (the tag is authoritative). Album-level
/// fields are aggregated across all of the album's tracks (first non-empty value wins) so a
/// gap on one track does not leave the album empty. When <c>apply</c> is <see langword="false"/>
/// the run is a dry-run that only counts what would change.
/// </summary>
public class LibraryMetadataRefresher(
    ITrackRepository trackRepository,
    IAlbumRepository albumRepository,
    ITagService tagService,
    EmbeddedLyricsImporter embeddedLyricsImporter,
    IFileSystem fileSystem,
    ILogger<LibraryMetadataRefresher> logger)
{
    public async Task<LibraryMetadataRefreshReport> RefreshAsync(
        bool apply,
        IProgress<LibraryMetadataRefreshProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<long, AlbumEntity> albums = (await albumRepository.GetAllAsync(RepositoryConnectionKind.Background))
            .ToDictionary(album => album.Id);

        Dictionary<long, TrackFile> albumTagAggregates = [];

        int scanned = 0;
        int missing = 0;
        int tracksUpdated = 0;
        int lyricsCreated = 0;
        int lyricsFailed = 0;

        List<TrackEntity> tracks = (await trackRepository.GetAllAsync(RepositoryConnectionKind.Background)).ToList();
        int total = tracks.Count;
        int reportEvery = Math.Max(1, total / 100);

        foreach (TrackEntity track in tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;

            if (progress is not null && (scanned % reportEvery == 0 || scanned == total))
                progress.Report(new LibraryMetadataRefreshProgress("Tracks", scanned, total));

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

            if (TagMetadataMapper.ApplyTrackMetadata(track, file, MetadataWritePolicy.FillFromTag))
            {
                tracksUpdated++;

                if (apply)
                    await trackRepository.UpdateAsync(track, RepositoryConnectionKind.Background);
            }

            if (track.AlbumId is long albumId && albums.ContainsKey(albumId))
                AccumulateAlbumTags(GetOrCreateAggregate(albumTagAggregates, albumId), file);

            if (apply)
            {
                if (await embeddedLyricsImporter.ExtractAsync(file, cancellationToken))
                    lyricsCreated++;
                else if (embeddedLyricsImporter.WouldWriteSidecar(file))
                    lyricsFailed++;
            }
            else if (embeddedLyricsImporter.WouldWriteSidecar(file))
            {
                lyricsCreated++;
            }
        }

        int albumsUpdated = await RefreshAlbumsAsync(albums, albumTagAggregates, apply, progress);

        return new LibraryMetadataRefreshReport
        {
            Applied = apply,
            TracksScanned = scanned,
            FilesMissing = missing,
            TracksUpdated = tracksUpdated,
            AlbumsUpdated = albumsUpdated,
            LyricsSidecarsCreated = lyricsCreated,
            LyricsSidecarsFailed = lyricsFailed
        };
    }

    private async Task<int> RefreshAlbumsAsync(
        Dictionary<long, AlbumEntity> albums,
        Dictionary<long, TrackFile> aggregates,
        bool apply,
        IProgress<LibraryMetadataRefreshProgress>? progress)
    {
        int updated = 0;
        int processed = 0;
        int total = aggregates.Count;
        int reportEvery = Math.Max(1, total / 100);

        foreach ((long albumId, TrackFile aggregate) in aggregates)
        {
            processed++;

            if (progress is not null && (processed % reportEvery == 0 || processed == total))
                progress.Report(new LibraryMetadataRefreshProgress("Albums", processed, total));

            if (!albums.TryGetValue(albumId, out AlbumEntity? album))
                continue;

            if (!TagMetadataMapper.ApplyAlbumMetadata(album, aggregate, MetadataWritePolicy.FillFromTag))
                continue;

            updated++;

            if (apply)
                await albumRepository.UpdateAsync(album, RepositoryConnectionKind.Background);
        }

        return updated;
    }

    private static TrackFile GetOrCreateAggregate(Dictionary<long, TrackFile> aggregates, long albumId)
    {
        if (!aggregates.TryGetValue(albumId, out TrackFile? aggregate))
        {
            aggregate = new TrackFile();
            aggregates[albumId] = aggregate;
        }

        return aggregate;
    }

    /// <summary>
    /// Folds one track's album-level tag values into the album aggregate, keeping the first
    /// non-empty value seen for each field (so a later track never overwrites an earlier value).
    /// </summary>
    private static void AccumulateAlbumTags(TrackFile aggregate, TrackFile file)
    {
        aggregate.DiscCount ??= file.DiscCount;
        aggregate.ReplayGainAlbumGain ??= file.ReplayGainAlbumGain;
        aggregate.ReplayGainAlbumPeak ??= file.ReplayGainAlbumPeak;

        if (string.IsNullOrEmpty(aggregate.MusicBrainzReleaseType))
            aggregate.MusicBrainzReleaseType = file.MusicBrainzReleaseType;

        if (string.IsNullOrEmpty(aggregate.MusicBrainzReleaseCountry))
            aggregate.MusicBrainzReleaseCountry = file.MusicBrainzReleaseCountry;

        if (string.IsNullOrEmpty(aggregate.MusicbrainzAlbumID))
            aggregate.MusicbrainzAlbumID = file.MusicbrainzAlbumID;
    }
}
