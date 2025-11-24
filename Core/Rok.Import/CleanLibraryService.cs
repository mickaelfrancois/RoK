using Microsoft.Extensions.Logging;
using MiF.Guard;
using Rok.Application.Dto;
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


    public async Task CleanAsync(ConcurrentBag<long> trackIDReaded, ImportStatisticsDto statistics, CancellationToken cancellationToken)
    {
        statistics.TracksDeleted += await RemoveTracksNotInLibraryAsync(trackIDReaded, cancellationToken);
        statistics.AlbumsDeleted += await CleanAlbumsWithoutTrackAsync();
        statistics.ArtistsDeleted += await CleanArtistsWithoutTrackAsync();
        statistics.GenresDeleted += await CleanGenresWithoutTrackAsync();
    }

    private async Task<int> RemoveTracksNotInLibraryAsync(ConcurrentBag<long> trackIDReaded, CancellationToken cancellationToken)
    {
        int count = 0;

        Stopwatch stopwatch = Stopwatch.StartNew();

        IEnumerable<TrackEntity> tracks = await _trackRepository.GetAllAsync(RepositoryConnectionKind.Background);

        HashSet<long> trackIDReadedSet = new(trackIDReaded);
        List<TrackEntity> tracksToDelete = tracks
            .Where(track => !trackIDReadedSet.Contains(track.Id))
            .ToList();

        using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            foreach (TrackEntity track in tracksToDelete)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await _trackRepository.DeleteAsync(track, RepositoryConnectionKind.Background);
                count++;
            }

            scope.Complete();

            if (count > 0)
            {
                _logger.LogInformation("{Count} tracks were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);
                stopwatch.Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning tracks without file.");
            scope.Dispose();
        }

        return count;
    }

    private async Task<int> CleanAlbumsWithoutTrackAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        int count = await _albumRepository.DeleteAlbumsWithoutTracks(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} albums without track were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);

        return count;
    }

    private async Task<int> CleanArtistsWithoutTrackAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int count = await _artistRepository.DeleteArtistsWithoutTracks(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} artists without track were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);

        return count;
    }

    private async Task<int> CleanGenresWithoutTrackAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int count = await _genreRepository.DeleteGenresWithoutTracks(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} genres without track were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);

        return count;
    }
}