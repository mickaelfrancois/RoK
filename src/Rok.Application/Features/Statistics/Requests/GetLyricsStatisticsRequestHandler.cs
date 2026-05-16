using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;

namespace Rok.Application.Features.Statistics.Requests;


public class GetLyricsStatisticsRequest : IRequest<LyricsStatisticsDto>
{
}

public class GetLyricsStatisticsRequestHandler(IFolderResolver _folderResolver, IAppOptions _options, ILogger<GetLyricsStatisticsRequestHandler> _logger) : IRequestHandler<GetLyricsStatisticsRequest, LyricsStatisticsDto>
{
    public async Task<LyricsStatisticsDto> Handle(GetLyricsStatisticsRequest request, CancellationToken cancellationToken)
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