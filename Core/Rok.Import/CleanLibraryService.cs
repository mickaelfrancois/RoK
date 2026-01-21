using System.Diagnostics;
using System.Transactions;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;

namespace Rok.Import;

public class CleanLibraryService(ITrackRepository _trackRepository, IArtistRepository _artistRepository, IAlbumRepository _albumRepository, IGenreRepository _genreRepository, ILogger<CleanLibraryService> _logger) : ICleanLibrary
{
    public async Task CleanAsync(IEnumerable<long> trackIDReaded, ImportStatisticsDto statistics, CancellationToken cancellationToken)
    {
        statistics.TracksDeleted += await RemoveTracksNotInLibraryAsync(trackIDReaded, cancellationToken);
        statistics.AlbumsDeleted += await CleanAlbumsWithoutTrackAsync();
        statistics.ArtistsDeleted += await CleanArtistsWithoutTrackAsync();
        statistics.GenresDeleted += await CleanGenresWithoutTrackAsync();
    }

    private async Task<int> RemoveTracksNotInLibraryAsync(IEnumerable<long> trackIDReaded, CancellationToken cancellationToken)
    {
        int count = 0;

        Stopwatch stopwatch = Stopwatch.StartNew();

        IEnumerable<TrackEntity> tracks = await _trackRepository.GetAllAsync(RepositoryConnectionKind.Background);

        HashSet<long> trackIDReadedSet = new(trackIDReaded);
        List<TrackEntity> tracksToDelete = tracks
            .Where(track => !trackIDReadedSet.Contains(track.Id))
            .ToList();

        if (tracksToDelete.Count == 0)
            return 0;

        TransactionOptions options = new()
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            Timeout = TimeSpan.FromMinutes(5)
        };

        using TransactionScope scope = new(TransactionScopeOption.Required, options, TransactionScopeAsyncFlowOption.Enabled);


        try
        {
            foreach (TrackEntity track in tracksToDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await _trackRepository.DeleteAsync(track, RepositoryConnectionKind.Background);
                count++;
            }

            scope.Complete();
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Track cleaning was cancelled: {Message}", ex.Message);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning tracks without file.");
            return 0;
        }

        stopwatch.Stop();

        if (count > 0)
            _logger.LogInformation("{Count} tracks were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);

        return count;
    }

    private async Task<int> CleanAlbumsWithoutTrackAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        int count = await _albumRepository.DeleteOrphansAsync(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} albums without track were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);

        return count;
    }

    private async Task<int> CleanArtistsWithoutTrackAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int count = await _artistRepository.DeleteOrphansAsync(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} artists without track were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);

        return count;
    }

    private async Task<int> CleanGenresWithoutTrackAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int count = await _genreRepository.DeleteOrphansAsync(RepositoryConnectionKind.Background);

        if (count > 0)
            _logger.LogTrace("{Count} genres without track were deleted from database in {ElapsedMilliseconds} ms", count, stopwatch.ElapsedMilliseconds);

        return count;
    }
}