using Microsoft.Extensions.Logging;
using MiF.Guard;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Transactions;

namespace Rok.Import;

public class CleanLibraryService(ITrackRepository trackRepository, IArtistRepository artistRepository, IAlbumRepository albumRepository, IGenreRepository genreRepository, ILogger<CleanLibraryService> logger) : ICleanLibrary
{
    private readonly ITrackRepository _trackRepository = Guard.Against.Null(trackRepository);
    private readonly IArtistRepository _artistRepository = Guard.Against.Null(artistRepository);
    private readonly IAlbumRepository _albumRepository = Guard.Against.Null(albumRepository);
    private readonly IGenreRepository _genreRepository = Guard.Against.Null(genreRepository);
    private readonly ILogger<CleanLibraryService> _logger = Guard.Against.Null(logger);


    public async Task CleanAsync(ConcurrentBag<long> trackIDReaded, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        await CleanTracksWithoutFileAsync(trackIDReaded, cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation("Cleaning tracks without file took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

        stopwatch.Reset();
        await CleanAlbumsWithoutTrackAsync();
        stopwatch.Stop();
        _logger.LogInformation("Cleaning albums without track took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

        stopwatch.Reset();
        await CleanArtistsWithoutTrackAsync();
        stopwatch.Stop();
        _logger.LogInformation("Cleaning artists without track took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

        stopwatch.Reset();
        await CleanGenresWithoutTrackAsync();
        stopwatch.Stop();
        _logger.LogInformation("Cleaning genres without track took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
    }

    private async Task CleanTracksWithoutFileAsync(ConcurrentBag<long> trackIDReaded, CancellationToken cancellationToken)
    {
        int count = 0;

        IEnumerable<TrackEntity> tracks = await _trackRepository.GetAllAsync(RepositoryConnectionKind.Background);

        HashSet<long> trackIDReadedSet = new(trackIDReaded);
        List<TrackEntity> tracksToDelete = tracks
            .Where(track => !trackIDReadedSet.Contains(track.Id) && !File.Exists(track.MusicFile))
            .ToList();

        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            foreach (TrackEntity track in tracksToDelete)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await _trackRepository.DeleteAsync(track, RepositoryConnectionKind.Background);
                count++;
            }

            scope.Complete();

            if (count > 0)
                _logger.LogTrace("{Count} tracks were deleted from database", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning tracks without file.");
            scope.Dispose();
            return;
        }
    }

    private async Task CleanAlbumsWithoutTrackAsync()
    {
        int count = await _albumRepository.DeleteAlbumsWithoutTracks(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} albums without track were deleted from database", count);
    }

    private async Task CleanArtistsWithoutTrackAsync()
    {
        int count = await _artistRepository.DeleteArtistsWithoutTracks(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} artists without track were deleted from database", count);
    }

    private async Task CleanGenresWithoutTrackAsync()
    {
        int count = await _genreRepository.DeleteGenresWithoutTracks(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} genres without track were deleted from database", count);
    }
}