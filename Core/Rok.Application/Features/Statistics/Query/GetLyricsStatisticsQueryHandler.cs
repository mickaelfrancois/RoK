using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Application.Features.Statistics.Query;


public class GetLyricsStatisticsQuery : IQuery<LyricsStatisticsDto>
{
}

public class GetLyricsStatisticsQueryHandler(IFolderResolver _folderResolver, IAppOptions _options, ILogger<GetLyricsStatisticsQueryHandler> _logger) : IQueryHandler<GetLyricsStatisticsQuery, LyricsStatisticsDto>
{
    public async Task<LyricsStatisticsDto> HandleAsync(GetLyricsStatisticsQuery request, CancellationToken cancellationToken)
    {
        LyricsStatisticsDto statisticsDto = new();

        foreach (string token in _options.LibraryTokens)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                List<string> folderPaths = await _folderResolver.GetPathFromTokenAsync(token).ConfigureAwait(false);

                foreach (string folder in folderPaths)
                {
                    statisticsDto.TotalSyncLyrics += Directory.GetFiles(folder, "*.lrc", SearchOption.AllDirectories).Length;
                    statisticsDto.TotalRawLyrics += Directory.GetFiles(folder, "*.txt", SearchOption.AllDirectories).Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calculating lyrics statistics for token {Token}", token);
            }
        }

        return statisticsDto;
    }
}
