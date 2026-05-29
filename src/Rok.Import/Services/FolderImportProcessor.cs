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

        if (!RequiresImport(files))
            return;

        _trackFileProcessor.ReadMusicProperties(files);
        _trackFileProcessor.DetectCompilations(files);

        foreach (TrackFile file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await ProcessTrackFileAsync(file, statistics, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessTrackFileAsync(TrackFile file, ImportStatisticsDto statistics, CancellationToken cancellationToken)
    {
        TrackEntity? track = GetTrackByFile(file);

        if (!await _metadataService.ShouldUpdateMetadataAsync(file, track, cancellationToken).ConfigureAwait(false))
            return;

        GenreCacheItem? genre = await GetOrCreateGenreAsync(file.Genre, statistics).ConfigureAwait(false);
        ArtistCacheItem? artist = await GetOrCreateArtistAsync(file, genre?.Id, statistics).ConfigureAwait(false);
        AlbumCacheItem? album = await GetOrCreateAlbumAsync(file, artist?.Id, genre?.Id, statistics).ConfigureAwait(false);

        long? genreId = DetermineGenreId(artist, genre);

        track ??= new TrackEntity();

        _metadataService.EnsureTrackTimestamps(track, file);
        TrackMetadataService.FillTrackEntity(track, file, artist?.Id, album?.Id, genreId);

        UpdateStatisticsForTrack(track);

        await UpdateTrackAsync(track, statistics).ConfigureAwait(false);
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

    private bool RequiresImport(List<TrackFile> files)
    {
        foreach (TrackFile file in files)
        {
            TrackEntity? track = GetTrackByFile(file);

            if (track != null)
            {
                _trackingService.TrackRead(track.Id);

                if (track.FileDate.Ticks < file.FileDateModified.DateTime.Ticks)
                    return true;
            }
            else
                return true;
        }

        return false;
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
        ImportStatisticsDto statistics)
    {
        if (string.IsNullOrWhiteSpace(file.Album))
            return null;

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
}