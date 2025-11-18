using System.Collections.Concurrent;

namespace Rok.Application.Interfaces;

public interface ICleanLibrary
{
    Task CleanAsync(ConcurrentBag<long> trackIDReaded, ImportStatisticsDto statistics, CancellationToken cancellationToken);
}
