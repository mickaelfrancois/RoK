using Microsoft.Extensions.Logging;
using MiF.Guard;
using MiF.SimpleMessenger;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Messages;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Import.Models;
using Rok.Shared;
using Rok.Shared.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Rok.Import;

public class ImportService : IImport
{
    public bool UpdateInProgress { get; private set; }

    private readonly IFolderResolver _folderResolver;
    private readonly ITagService _tagService;
    private readonly ILogger<ImportService> _logger;
    private readonly ArtistImport _importArtist;
    private readonly AlbumImport _importAlbum;
    private readonly GenreImport _importGenre;
    private readonly TrackImport _importTrack;
    private readonly IAppOptions _options;
    private readonly ICleanLibrary _cleanLibraryService;
    private readonly CountryCache _countryCache;
    private readonly Statistics _statistics;
    private readonly ITelemetryClient _telemetryClient;
    private CancellationTokenSource? _cancellationToken;

    public ImportStatisticsDto Statistics { get; private set; } = new();

    private readonly ConcurrentBag<long> _trackIDReaded = new();
    private readonly ConcurrentBag<long> _artistsUpdated = new();
    private readonly ConcurrentBag<long> _genresUpdated = new();
    private readonly ConcurrentBag<long> _albumsUpdated = new();


    public ImportService(IFolderResolver folderResolver, CountryCache countryCache, IAppOptions options, Statistics statistics, TrackImport importTrack, GenreImport importGenre, AlbumImport importAlbum, ArtistImport importArtist, ITagService tagService, ICleanLibrary cleanLibraryService, ITelemetryClient telemetryClient, ILogger<ImportService> logger)
    {
        _folderResolver = Guard.Against.Null(folderResolver);
        _countryCache = Guard.Against.Null(countryCache);
        _options = Guard.Against.Null(options);
        _statistics = Guard.Against.Null(statistics);
        _importTrack = Guard.Against.Null(importTrack);
        _importGenre = Guard.Against.Null(importGenre);
        _importAlbum = Guard.Against.Null(importAlbum);
        _importArtist = Guard.Against.Null(importArtist);
        _tagService = Guard.Against.Null(tagService);
        _cleanLibraryService = Guard.Against.Null(cleanLibraryService);
        _telemetryClient = telemetryClient;
        _logger = Guard.Against.Null(logger);
    }


    public void StartAsync(int delayInSeconds)
    {
        Guard.Against.Negative(delayInSeconds);

        if (_options.LibraryTokens is null)
        {
            _logger.LogWarning("No library tokens configured, import process will not start.");
            return;
        }

        if (UpdateInProgress)
            return;

        _cancellationToken = new CancellationTokenSource();
        UpdateInProgress = true;

        Messenger.Send(new LibraryRefreshMessage() { ProcessState = LibraryRefreshMessage.EState.Running });
        Stopwatch stopwatch = Stopwatch.StartNew();

        Task.Run(async () =>
        {
            if (delayInSeconds > 0)
                await Task.Delay(delayInSeconds);

            await ImportAsync(_cancellationToken.Token);

        }, _cancellationToken.Token)
        .ContinueWith(async c =>
        {
            if (c.IsFaulted)
            {
                _logger.LogCritical(c.Exception, "An exception occurred while refreshing library: {Message}", c.Exception.Message);
                await _telemetryClient.CaptureExceptionAsync(c.Exception);
            }

            _logger.LogTrace("Files read: {FilesRead}, Tracks imported: {TracksImported}, Albums imported: {AlbumImported}, Artists imported: {ArtistImported}, Genres imported: {GenreImported}. End of refresh library in {ElapsedMilliseconds} ms.", Statistics.FilesRead, Statistics.TracksImported, Statistics.AlbumsImported, Statistics.ArtistsImported, Statistics.GenresImported, stopwatch.ElapsedMilliseconds);

            UpdateInProgress = false;
            Messenger.Send(new LibraryRefreshMessage() { ProcessState = LibraryRefreshMessage.EState.Stop, Statistics = Statistics });

            _cancellationToken.Dispose();
        });
    }




    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        bool errorOccurred = false;

        Statistics = new();
        _trackIDReaded.Clear();
        _albumsUpdated.Clear();
        _artistsUpdated.Clear();
        _genresUpdated.Clear();

        await LoadCachesAsync();

        List<string> pathsToImport = await GetLibraryImportPathsAsync(cancellationToken);

        foreach (string path in pathsToImport)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters(path))
            {
                errorOccurred |= await ImportAsync(path, cancellationToken);
            }
        }

        await UpdateStatisticsAsync();

        if (!errorOccurred)
            await CleanDataAsync(cancellationToken);

        await SendMetricsAsync().ConfigureAwait(false);
    }


    private async Task<List<string>> GetLibraryImportPathsAsync(CancellationToken cancellationToken)
    {
        List<string> pathsToImport = new();

        foreach (string token in _options.LibraryTokens)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            List<string> folderPaths = await _folderResolver.GetPathFromTokenAsync(token).ConfigureAwait(false);

            foreach (string folderPath in folderPaths)
            {
                if (!pathsToImport.Any(p => string.Equals(p, folderPath, StringComparison.OrdinalIgnoreCase)))
                    pathsToImport.Add(folderPath);
            }
        }

        return pathsToImport;
    }


    private async Task UpdateStatisticsAsync()
    {
        Messenger.Send(new LibraryRefreshMessage() { ProcessState = LibraryRefreshMessage.EState.UpdateData });

        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Update statistics"))
        {
            await _statistics.UpdateArtistsAsync(_artistsUpdated);
            await _statistics.UpdateAlbumsAsync(_albumsUpdated);
            await _statistics.UpdateGenresAsync(_genresUpdated);
        }
    }


    private async Task SendMetricsAsync()
    {
        await _telemetryClient.CaptureEventAsync("stats", new Dictionary<string, object>
        {
            ["tracks"] = _importTrack.CountInCache,
            ["artists"] = _importArtist.CountInCache,
            ["genres"] = _importGenre.CountInCache,
            ["albums"] = _importAlbum.CountInCache
        });
    }


    private async Task CleanDataAsync(CancellationToken cancellationToken)
    {
        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Clean data"))
        {
            Messenger.Send(new LibraryRefreshMessage() { ProcessState = LibraryRefreshMessage.EState.CleanData });

            await _cleanLibraryService.CleanAsync(_trackIDReaded, Statistics, cancellationToken);
        }
    }


    private async Task LoadCachesAsync()
    {
        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("LoadCaches"))
        {
            await _countryCache.LoadCacheAsync();
            await _importTrack.LoadCacheAsync();
            await _importAlbum.LoadCacheAsync();
            await _importArtist.LoadCacheAsync();
            await _importGenre.LoadCacheAsync();
        }
    }


    private async Task<bool> ImportAsync(string path, CancellationToken cancellationToken)
    {
        bool errorOccurred = false;
        int folderRead = 0;
        int lastProgression = 0;

        try
        {
            // Root folder
            await ImportFolderAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            errorOccurred = true;
            _logger.LogCritical(ex, "An exception occurred while importing music of '{Path}'.", path);

            _ = _telemetryClient.CaptureExceptionAsync(ex);
        }

        List<string> subFolders = GetFoldersByCreatDate(path);
        foreach (string subFolder in subFolders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await ImportFolderAsync(subFolder, cancellationToken);

                folderRead++;
                int progression = folderRead * 100 / subFolders.Count;
                if (lastProgression != progression)
                {
                    Messenger.Send(new LibraryRefreshMessage() { ProcessState = LibraryRefreshMessage.EState.Unchanged, ProcessMessage = $"{progression}%" });
                    lastProgression = progression;
                }
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                _logger.LogCritical(ex, "An exception occurred while importing music of '{Path}'.", path);

                _ = _telemetryClient.CaptureExceptionAsync(ex);
            }
        }

        return errorOccurred;
    }


    private async Task ImportFolderAsync(string musicFolder, CancellationToken cancellationToken)
    {
        List<TrackFile> files = GetFiles(musicFolder);

        if (files.Count == 0)
            return;

        Statistics.FilesRead += files.Count;

        if (!AreFilesNewer(files))
            return;

        ReadMusicProperties(files);
        IsCompilation(files);

        foreach (TrackFile file in files)
        {
            ArtistCacheItem? artist;
            AlbumCacheItem? album;
            GenreCacheItem? genre;
            long? genreId;

            if (cancellationToken.IsCancellationRequested)
                return;

            TrackEntity? track = GetTrackByFile(file);
            if (!await ShouldUpdateMetadataAsync(file, track).ConfigureAwait(false))
                continue;

            genre = await GetOrCreateGenreAsync(file.Genre).ConfigureAwait(false);
            artist = await GetOrCreateArtistAsync(file, genre?.Id).ConfigureAwait(false);
            album = await GetOrCreateAlbumAsync(file, artist?.Id, genre?.Id).ConfigureAwait(false);

            if (_options.ImportTrackWithArtistGenre && artist?.GenreId.HasValue == true)
                genreId = artist?.GenreId;
            else
                genreId = genre?.Id;

            track ??= new TrackEntity();

            EnsureTrackTimestamps(track, file);
            FillTrackEntity(track, file, artist?.Id, album?.Id, genreId);

            UpdateArtistStatistics(track.ArtistId);
            UpdateGenreStatistics(track.GenreId);
            UpdateAlbumStatistics(track.AlbumId);

            await UpdateTracksAsync(track).ConfigureAwait(false);
        }
    }


    private async Task UpdateTracksAsync(TrackEntity track)
    {
        try
        {
            if (track.Id > 0)
            {
                await _importTrack.UpdateTrackAsync(track).ConfigureAwait(false);
                Statistics.TracksUpdated++;
            }
            else
            {
                TrackEntity? newTrack = await _importTrack.CreateAsync(track).ConfigureAwait(false);
                if (newTrack == null)
                    return;

                Statistics.TracksImported++;
                _trackIDReaded.Add(newTrack.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update track id {Id} (file '{File}')", track.Id, track.MusicFile);
        }
    }


    private void UpdateArtistStatistics(long? artistId)
    {
        if (artistId == null)
            return;

        _artistsUpdated.Add(artistId.Value);
    }


    private void UpdateGenreStatistics(long? genreId)
    {
        if (genreId == null)
            return;

        _genresUpdated.Add(genreId.Value);
    }


    private void UpdateAlbumStatistics(long? albumId)
    {
        if (albumId == null)
            return;

        _albumsUpdated.Add(albumId.Value);
    }


    private async Task<bool> ShouldUpdateMetadataAsync(TrackFile file, TrackEntity? track)
    {
        if (track == null)
            return true;

        if (!AreTrackAndID3Equals(track, file))
            return true;

        DateTime trackDateTimeUtc = track.FileDate.ToUniversalTime().TruncateToMinutes();
        DateTime fileDateUtcTrunc = file.FileDateModified.UtcDateTime.TruncateToMinutes();

        if (trackDateTimeUtc == fileDateUtcTrunc)
            return false;

        if (fileDateUtcTrunc > trackDateTimeUtc)
            await UpdateTrackFileDateAsync(track, file.FileDateModified.DateTime);
        else
            _logger.LogTrace("Database file date is newer for track id {Id} (db:{Db} file:{File}) - no update performed", track.Id, trackDateTimeUtc, fileDateUtcTrunc);

        return false;
    }


    private async Task UpdateTrackFileDateAsync(TrackEntity track, DateTime fileDate)
    {
        try
        {
            await _importTrack.UpdateTrackFileDateAsync(track.Id, fileDate).ConfigureAwait(false);
            _logger.LogInformation("Updated file date for track id {Id} to {Date} (file '{File}')", track.Id, fileDate, track.MusicFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update file date for track id {Id} (file '{File}')", track.Id, track.MusicFile);
        }
    }


    private List<string> GetFoldersByCreatDate(string path)
    {
        using (PerfLogger perf = new(_logger))
        {
            try
            {
                EnumerationOptions enumerationOptions = new()
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true
                };

                int maxDegree = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

                return Directory.EnumerateDirectories(path, "*", enumerationOptions)
                            .AsParallel()
                            .WithDegreeOfParallelism(maxDegree)
                            .Where(di => !di.Contains("@Artist", StringComparison.OrdinalIgnoreCase))
                            .Select(path => new DirectoryInfo(path))
                            .OrderByDescending(di => di.CreationTime)
                            .Select(c => c.FullName)
                            .ToList();
            }
            catch (AggregateException aggEx)
            {
                foreach (Exception ex in aggEx.InnerExceptions)
                {
                    _logger.LogError(aggEx, "An error occurred while accessing folders in {Path}: {ExceptionMessage}", path, ex.Message);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to one or more folders in {Path}: {ExceptionMessage}", path, ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, "The folder was not found: {Path}: {ExceptionMessage}", path, ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O exception while accessing folder: {Path}: {ExceptionMessage}", path, ex.Message);
            }

            return [];
        }
    }


    private List<TrackFile> GetFiles(string path)
    {
        HashSet<string> validExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac" };
        List<TrackFile> files = [];

        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (!validExtensions.Contains(Path.GetExtension(file)))
                    continue;

                if (FileHelpers.IsOnline(file))
                    continue;

                try
                {
                    TrackFile trackFile = new();
                    _tagService.FillBasicProperties(file, trackFile);

                    files.Add(trackFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "An exception occurred while reading file properties '{File}'", file);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to folder '{Path}'", path);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "The folder was not found: '{Path}'", path);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O exception while accessing folder: '{Path}'", path);
        }

        return files;
    }


    private bool AreFilesNewer(List<TrackFile> files)
    {
        foreach (TrackFile file in files)
        {
            TrackEntity? track = GetTrackByFile(file);

            if (track != null)
            {
                _trackIDReaded.Add(track.Id);

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


    private void IsCompilation(List<TrackFile> files)
    {
        IEnumerable<IGrouping<string, TrackFile>> albumGroups = files.GroupBy(file => file.Album, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, TrackFile> albumGroup in albumGroups)
        {
            bool isCompilation = albumGroup
                .Select(file => file.Artist)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1;

            if (isCompilation)
                _logger.LogInformation("Album '{Album}' is marked as a compilation.", albumGroup.Key);

            foreach (TrackFile file in albumGroup)
            {
                file.IsCompilation = isCompilation;
            }
        }
    }


    private static bool AreTrackAndID3Equals(TrackEntity track, TrackFile trackID3)
    {
        if (trackID3.Artist.IsDifferent(track.ArtistName))
            return false;

        if (trackID3.Album.IsDifferent(track.AlbumName))
            return false;

        if (trackID3.Genre.IsDifferent(track.GenreName))
            return false;

        if (trackID3.Title.IsDifferent(track.Title))
            return false;

        if (trackID3.Size != track.Size)
            return false;

        if (trackID3.TrackNumber != track.TrackNumber)
            return false;

        return true;
    }


    private void ReadMusicProperties(List<TrackFile> files)
    {
        foreach (TrackFile file in files)
        {
            try
            {
                _tagService.FillMusicProperties(file.FullPath, file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while reading music properties '{File}'", file.FullPath);
            }
        }
    }


    private async Task<ArtistCacheItem?> GetOrCreateArtistAsync(TrackFile file, long? genreId)
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
                Statistics.ArtistsImported++;

                string artistName = file.Artist ?? string.Empty;

                Messenger.Send(new ArtistImportedMessage(artistName));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An exception occurred while creating artist '{Artist}' from file '{File}'", file.Artist, file.FullPath);
            return null;
        }

        return artist;
    }


    private async Task<GenreCacheItem?> GetOrCreateGenreAsync(string genreName)
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
                Statistics.GenresImported++;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An exception occurred while creating genre '{Genre}'", genreName);
            return null;
        }

        return genre;
    }


    private async Task<AlbumCacheItem?> GetOrCreateAlbumAsync(TrackFile file, long? artistId, long? genreId)
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
                Statistics.AlbumsImported++;

                string albumName = album.Name ?? string.Empty;
                string artistName = file.Artist ?? string.Empty;
                string albumPath = album.AlbumPath ?? string.Empty;

                Messenger.Send(new AlbumImportedMessage(albumName, artistName, albumPath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An exception occurred while creating album '{Album}' from file '{File}'", file.Album, file.FullPath);
            return null;
        }

        return album;
    }


    private static void EnsureTrackTimestamps(TrackEntity track, TrackFile file)
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


    private static void FillTrackEntity(TrackEntity track, TrackFile file, long? artistId, long? albumId, long? genreId)
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
}
