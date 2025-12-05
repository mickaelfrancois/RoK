using Microsoft.Extensions.Logging;
using MiF.Guard;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Import.Services;
using Rok.Shared;
using System.Diagnostics;

namespace Rok.Import;

public class ImportService(
    IFolderResolver folderResolver,
    CountryCache countryCache,
    IAppOptions options,
    Statistics statistics,
    TrackImport importTrack,
    GenreImport importGenre,
    AlbumImport importAlbum,
    ArtistImport importArtist,
    ICleanLibrary cleanLibraryService,
    ITelemetryClient telemetryClient,
    ImportProgressService progressService,
    ImportTrackingService trackingService,
    FileSystemService fileSystemService,
    FolderImportProcessor folderProcessor,
    ILogger<ImportService> logger) : IImport
{
    public bool UpdateInProgress { get; private set; }
    public ImportStatisticsDto Statistics { get; private set; } = new();

    private readonly IFolderResolver _folderResolver = Guard.Against.Null(folderResolver);
    private readonly ILogger<ImportService> _logger = Guard.Against.Null(logger);
    private readonly IAppOptions _options = Guard.Against.Null(options);
    private readonly ICleanLibrary _cleanLibraryService = Guard.Against.Null(cleanLibraryService);
    private readonly CountryCache _countryCache = Guard.Against.Null(countryCache);
    private readonly Statistics _statistics = Guard.Against.Null(statistics);
    private readonly TrackImport _importTrack = Guard.Against.Null(importTrack);
    private readonly AlbumImport _importAlbum = Guard.Against.Null(importAlbum);
    private readonly ArtistImport _importArtist = Guard.Against.Null(importArtist);
    private readonly GenreImport _importGenre = Guard.Against.Null(importGenre);
    private readonly ImportProgressService _progressService = Guard.Against.Null(progressService);
    private readonly ImportTrackingService _trackingService = Guard.Against.Null(trackingService);
    private readonly FileSystemService _fileSystemService = Guard.Against.Null(fileSystemService);
    private readonly FolderImportProcessor _folderProcessor = Guard.Against.Null(folderProcessor);

    private CancellationTokenSource? _cancellationToken;

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

        _progressService.ReportRunning();
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
                _logger.LogCritical(c.Exception, "An exception occurred while refreshing library: {Message}",
                    c.Exception.Message);
                await telemetryClient.CaptureExceptionAsync(c.Exception);
            }

            _logger.LogTrace("Files read: {FilesRead}, Tracks imported: {TracksImported}, Albums imported: {AlbumImported}, Artists imported: {ArtistImported}, Genres imported: {GenreImported}. End of refresh library in {ElapsedMilliseconds} ms.",
                Statistics.FilesRead, Statistics.TracksImported, Statistics.AlbumsImported,
                Statistics.ArtistsImported, Statistics.GenresImported, stopwatch.ElapsedMilliseconds);

            UpdateInProgress = false;
            _progressService.ReportStopped(Statistics);

            _cancellationToken.Dispose();
        });
    }

    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        bool errorOccurred = false;

        InitializeImport();

        await LoadCachesAsync();

        List<string> pathsToImport = await GetLibraryImportPathsAsync(cancellationToken);

        foreach (string path in pathsToImport
)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters(path))
            {
                errorOccurred |= await ImportPathAsync(path, cancellationToken);
            }
        }

        await UpdateStatisticsAsync();

        if (!errorOccurred)
            await CleanDataAsync(cancellationToken);

        await SendMetricsAsync().ConfigureAwait(false);
    }

    private void InitializeImport()
    {
        Statistics = new();
        _trackingService.Clear();
    }

    private async Task<List<string>> GetLibraryImportPathsAsync(CancellationToken cancellationToken)
    {
        List<string> pathsToImport = new();

        foreach (string token in _options.LibraryTokens)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            List<string> folderPaths = await _folderResolver.GetPathFromTokenAsync(token).ConfigureAwait(false);

            pathsToImport = pathsToImport.Union(folderPaths, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return pathsToImport;
    }

    private async Task<bool> ImportPathAsync(string path, CancellationToken cancellationToken)
    {
        bool errorOccurred = false;
        int folderRead = 0;
        int lastProgression = 0;

        try
        {
            await _folderProcessor.ImportFolderAsync(path, Statistics, cancellationToken);
        }
        catch (Exception ex)
        {
            errorOccurred = true;
            _logger.LogCritical(ex, "An exception occurred while importing music of '{Path}'.", path);
            _ = telemetryClient.CaptureExceptionAsync(ex);
        }

        List<string> subFolders = _fileSystemService.GetFoldersByCreationDate(path);

        foreach (string subFolder in subFolders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await _folderProcessor.ImportFolderAsync(subFolder, Statistics, cancellationToken);

                folderRead++;
                int progression = folderRead * 100 / subFolders.Count;

                if (lastProgression != progression)
                {
                    _progressService.ReportProgress(progression);
                    lastProgression = progression;
                }
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                _logger.LogCritical(ex, "An exception occurred while importing music of '{Path}'.", path);
                _ = telemetryClient.CaptureExceptionAsync(ex);
            }
        }

        return errorOccurred;
    }

    private async Task UpdateStatisticsAsync()
    {
        _progressService.ReportUpdateData();

        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Update statistics"))
        {
            await _statistics.UpdateArtistsAsync(_trackingService.GetUpdatedArtists());
            await _statistics.UpdateAlbumsAsync(_trackingService.GetUpdatedAlbums());
            await _statistics.UpdateGenresAsync(_trackingService.GetUpdatedGenres());
        }
    }

    private async Task SendMetricsAsync()
    {
        await telemetryClient.CaptureEventAsync("stats", new Dictionary<string, object>
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
            _progressService.ReportCleanData();

            await _cleanLibraryService.CleanAsync(_trackingService.GetTrackedIds(), Statistics, cancellationToken);
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
}
