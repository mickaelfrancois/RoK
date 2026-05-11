using Microsoft.Extensions.Logging;
using MiF.Mediator.Interfaces;
using MiF.Result;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Query;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces.Pictures;
using Rok.Shared;

namespace Rok.Import.Services;

public class PostImportApiEnrichmentTask(
    ArtistImport artistImport,
    AlbumImport albumImport,
    IArtistApiService artistApiService,
    IAlbumApiService albumApiService,
    IArtistPictureService artistPictureService,
    IAlbumPictureService albumPictureService,
    IBackdropPicture backdropPicture,
    IMediator mediator,
    ILogger<PostImportApiEnrichmentTask> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using PerfLogger perf = new PerfLogger(logger).Parameters("Post import API enrichment task");
        await EnrichArtistsAsync(cancellationToken);
        await EnrichAlbumsAsync(cancellationToken);
    }

    public async Task EnrichArtistsAsync(CancellationToken cancellationToken)
    {
        int enriched = 0;

        foreach (long artistId in artistImport.NewlyCreatedIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                Result<ArtistDto> result = await mediator.SendMessageAsync(new GetArtistByIdQuery(artistId), cancellationToken);

                if (!result.IsSuccess)
                {
                    logger.LogWarning("Artist {Id} not found for API enrichment.", artistId);
                    continue;
                }

                await artistApiService.GetAndUpdateArtistDataAsync(result.Value!, artistPictureService, backdropPicture);
                enriched++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enrich artist {Id}.", artistId);
            }
        }

        logger.LogInformation("API enrichment: {Count} artists enriched.", enriched);
    }


    public async Task EnrichAlbumsAsync(CancellationToken cancellationToken)
    {
        int enriched = 0;

        foreach (long albumId in albumImport.NewlyCreatedIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                Result<AlbumDto> result = await mediator.SendMessageAsync(new GetAlbumByIdQuery(albumId), cancellationToken);

                if (!result.IsSuccess)
                {
                    logger.LogWarning("Album {Id} not found for API enrichment.", albumId);
                    continue;
                }

                await albumApiService.GetAndUpdateAlbumDataAsync(result.Value!, albumPictureService);
                enriched++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enrich album {Id}.", albumId);
            }
        }

        logger.LogInformation("API enrichment: {Count} albums enriched.", enriched);
    }
}
