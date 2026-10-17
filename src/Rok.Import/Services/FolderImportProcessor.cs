using CleanArch.DevKit.Guards;
using CleanArch.DevKit.Messaging;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Messages;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Import.Models;

namespace Rok.Import.Services;

public class FolderImportProcessor(
    IAppOptions options,
    TrackImport importTrack,
    ArtistImport importArtist,
    AlbumImport importAlbum,
    GenreImport importGenre,
    FileSystemService fileSystemService,
    TrackFileProcessor trackFileProcessor,
    TrackMetadataService metadataService,
    EmbeddedLyricsImporter embeddedLyricsImporter,
    ImportTrackingService trackingService,
    ITagService tagService,
    ImportMessageThrottler messageThrottler,
    IMessenger messenger,
    ILogger<FolderImportProcessor> logger)
{
    private readonly IAppOptions _options = Guard.NotNull(options);
    private readonly TrackImport _importTrack = Guard.NotNull(importTrack);
    private readonly ArtistImport _importArtist = Guard.NotNull(importArtist);
    private readonly AlbumImport _importAlbum = Guard.NotNull(importAlbum);
    private readonly GenreImport _importGenre = Guard.NotNull(importGenre);
    private readonly FileSystemService _fileSystemService = Guard.NotNull(fileSystemService);
    private readonly TrackFileProcessor _trackFileProcessor = Guard.NotNull(trackFileProcessor);
    private readonly TrackMetadataService _metadataService = Guard.NotNull(metadataService);
    private readonly EmbeddedLyricsImporter _embeddedLyricsImporter = Guard.NotNull(embeddedLyricsImporter);
    private readonly ImportTrackingService _trackingService = Guard.NotNull(trackingService);
    private readonly ITagService _tagService = Guard.NotNull(tagService);
    private readonly ImportMessageThrottler _messageThrottler = Guard.NotNull(messageThrottler);
    private readonly ILogger<FolderImportProcessor> _logger = Guard.NotNull(logger);

    public async Task ImportFolderAsync(
        string musicFolder,
        ImportStatisticsDto statistics,
        CancellationToken cancellationToken)
    {
        List<TrackFile> files = _fileSystemService.GetMusicFiles(
            musicFolder,
            _tagService.FillBasicProperties);

        if (files.Count == 0)
            return;

        statistics.FilesRead += files.Count;

        if (!MarkReadAndCheckImport(files))
            return;

        _trackFileProcessor.ReadMusicProperties(files);
        PendingTagWriteOverlay.Apply(files, _importTrack);
        _trackFileProcessor.DetectCompilations(files);

        IReadOnlyDictionary<string, KnownFolderAlbum> knownAlbums = FolderAlbumIdentity.Resolve(files, _importTrack, _importAlbum);

        foreach (TrackFile file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await ProcessTrackFileAsync(file, knownAlbums, statistics, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessTrackFileAsync(
        TrackFile file,
        IReadOnlyDictionary<string, KnownFolderAlbum> knownAlbums,
        ImportStatisticsDto statistics,
        CancellationToken cancellationToken)
    {
        TrackEntity? track = GetTrackByFile(file);

        if (!await _metadataService.ShouldUpdateMetadataAsync(file, track, cancellationToken).ConfigureAwait(false))
            return;

        ArtistCacheItem? knownArtist = string.IsNullOrWhiteSpace(file.Artist) ? null : _importArtist.GetFromCache(file.Artist);
        bool artistImposesGenre = _options.ImportTrackWithArtistGenre && knownArtist?.GenreId.HasValue == true;

        GenreCacheItem? genre = artistImposesGenre
            ? null
            : await GetOrCreateGenreAsync(file.Genre, statistics).ConfigureAwait(false);
        ArtistCacheItem? artist = knownArtist ?? await GetOrCreateArtistAsync(file, genre?.Id, statistics).ConfigureAwait(false);

        long? genreId = DetermineGenreId(artist, genre);
        AlbumCacheItem? album = await GetOrCreateAlbumAsync(file, artist?.Id, genreId, knownAlbums, statistics).ConfigureAwait(false);

        track ??= new TrackEntity();

        _metadataService.EnsureTrackTimestamps(track, file);
        TrackMetadataService.FillTrackEntity(track, file, artist?.Id, album?.Id, genreId);

        UpdateStatisticsForTrack(track);

        await UpdateTrackAsync(track, statistics).ConfigureAwait(false);

        await _embeddedLyricsImporter.ExtractAsync(file, cancellationToken).ConfigureAwait(false);
    }

    private long? DetermineGenreId(ArtistCacheItem? artist, GenreCacheItem? genre)
    {
        return _options.ImportTrackWithArtistGenre && artist?.GenreId.HasValue == true
            ? artist.GenreId
            : genre?.Id;
    }

    private void UpdateStatisticsForTrack(TrackEntity track)
    {
        _trackingService.ArtistUpdated(track.ArtistId);
        _trackingService.GenreUpdated(track.GenreId);
        _trackingService.AlbumUpdated(track.AlbumId);
    }

    private async Task UpdateTrackAsync(TrackEntity track, ImportStatisticsDto statistics)
    {
        try
        {
            if (track.Id > 0)
            {
                await _importTrack.UpdateTrackAsync(track).ConfigureAwait(false);
                statistics.TracksUpdated++;
            }
            else
            {
                TrackEntity? newTrack = await _importTrack.CreateAsync(track).ConfigureAwait(false);
                if (newTrack == null)
                    return;

                statistics.TracksImported++;
                _trackingService.TrackRead(newTrack.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update track id {Id} (file '{File}')", track.Id, track.MusicFile);
        }
    }

    internal bool MarkReadAndCheckImport(IReadOnlyList<TrackFile> files)
    {
        bool requiresImport = false;

        foreach (TrackFile file in files)
        {
            TrackEntity? track = GetTrackByFile(file);

            if (track == null)
            {
                requiresImport = true;
                continue;
            }

            _trackingService.TrackRead(track.Id);

            if (track.FileDate.Ticks < file.FileDateModified.DateTime.Ticks)
                requiresImport = true;
        }

        return requiresImport;
    }

    private TrackEntity? GetTrackByFile(TrackFile file)
    {
        return _importTrack.GetFromCache(file.FullPath);
    }

    private async Task<GenreCacheItem?> GetOrCreateGenreAsync(string genreName, ImportStatisticsDto statistics)
    {
        if (string.IsNullOrWhiteSpace(genreName))
            return null;

        GenreCacheItem? genre = _importGenre.GetFromCache(genreName);
        if (genre != null)
            return genre;

        try
        {
            genre = await _importGenre.CreateAsync(genreName).ConfigureAwait(false);
            if (genre != null)
                statistics.GenresImported++;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An exception occurred while creating genre '{Genre}'", genreName);
            return null;
        }

        return genre;
    }

    private async Task<ArtistCacheItem?> GetOrCreateArtistAsync(
        TrackFile file,
        long? genreId,
        ImportStatisticsDto statistics)
    {
        if (string.IsNullOrWhiteSpace(file.Artist))
            return null;

        ArtistCacheItem? artist = _importArtist.GetFromCache(file.Artist);
        if (artist != null)
            return artist;

        try
        {
            artist = await _importArtist.CreateAsync(file, genreId).ConfigureAwait(false);
            if (artist != null)
            {
                statistics.ArtistsImported++;

                if (_messageThrottler.ShouldSendArtistMessage())
                {
                    string artistName = file.Artist ?? string.Empty;
                    messenger.Send(new ArtistImportedMessage(artistName));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An exception occurred while creating artist '{Artist}' from file '{File}'",
                file.Artist, file.FullPath);
            return null;
        }

        return artist;
    }

    private async Task<AlbumCacheItem?> GetOrCreateAlbumAsync(
        TrackFile file,
        long? artistId,
        long? genreId,
        IReadOnlyDictionary<string, KnownFolderAlbum> knownAlbums,
        ImportStatisticsDto statistics)
    {
        if (string.IsNullOrWhiteSpace(file.Album))
            return null;

        if (knownAlbums.TryGetValue(file.Album, out KnownFolderAlbum? known))
        {
            AlbumCacheItem? knownAlbum = _importAlbum.GetById(known.AlbumId);

            if (knownAlbum != null)
            {
                await ReconcileAlbumAsync(knownAlbum, file, artistId, genreId, known).ConfigureAwait(false);

                return knownAlbum;
            }
        }

        AlbumCacheItem? album = _importAlbum.GetFromCache(file.Album, file.IsCompilation, artistId);
        if (album != null)
            return album;

        try
        {
            album = await _importAlbum.CreateAsync(file, artistId, genreId).ConfigureAwait(false);

            if (album != null)
            {
                statistics.AlbumsImported++;

                if (_messageThrottler.ShouldSendAlbumMessage())
                {
                    string albumName = album.Name ?? string.Empty;
                    string artistName = file.Artist ?? string.Empty;
                    string albumPath = album.AlbumPath ?? string.Empty;

                    messenger.Send(new AlbumImportedMessage(albumName, artistName, albumPath));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An exception occurred while creating album '{Album}' from file '{File}'",
                file.Album, file.FullPath);
            return null;
        }

        return album;
    }

    private async Task ReconcileAlbumAsync(
        AlbumCacheItem album,
        TrackFile file,
        long? artistId,
        long? genreId,
        KnownFolderAlbum known)
    {
        (bool isCompilation, long? albumArtistId)? target = ComputeTarget(album, file, artistId, known);

        if (target == null)
            return;

        long? oldArtistId = album.ArtistId;
        bool oldIsCompilation = album.IsCompilation;

        try
        {
            await _importAlbum.UpdateCompilationAsync(album, target.Value.isCompilation, target.Value.albumArtistId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reconcile album id {Id} ('{Album}')", album.Id, album.Name);
            return;
        }

        _trackingService.ArtistUpdated(oldArtistId);
        _trackingService.ArtistUpdated(album.ArtistId);
        _trackingService.GenreUpdated(album.GenreId);
        _trackingService.GenreUpdated(genreId);
        _trackingService.AlbumUpdated(album.Id);

        _logger.LogInformation(
            "Album id {Id} ('{Album}') reconciled: compilation {OldIsCompilation} -> {IsCompilation}, artist {OldArtistId} -> {ArtistId}",
            album.Id, album.Name, oldIsCompilation, album.IsCompilation, oldArtistId, album.ArtistId);
    }

    private (bool isCompilation, long? albumArtistId)? ComputeTarget(
        AlbumCacheItem album,
        TrackFile file,
        long? artistId,
        KnownFolderAlbum known)
    {
        if (file.IsCompilation != known.WasCompilation)
        {
            if (file.IsCompilation)
                return album.IsCompilation ? null : (true, album.ArtistId);

            if (!album.IsCompilation)
                return AlignArtist(album, artistId);

            return HasOtherArtistOutsideFolder(album, artistId, known) ? null : (false, artistId);
        }

        if (!file.IsCompilation && !album.IsCompilation)
            return AlignArtist(album, artistId);

        return null;
    }

    private static (bool isCompilation, long? albumArtistId)? AlignArtist(AlbumCacheItem album, long? artistId)
    {
        return artistId.HasValue && artistId != album.ArtistId ? (false, artistId) : null;
    }

    private bool HasOtherArtistOutsideFolder(AlbumCacheItem album, long? artistId, KnownFolderAlbum known)
    {
        return _importTrack.GetCachedTracksOfAlbum(album.Id)
            .Any(track => !known.FolderTrackIds.Contains(track.Id) && track.ArtistId.HasValue && track.ArtistId != artistId);
    }
}