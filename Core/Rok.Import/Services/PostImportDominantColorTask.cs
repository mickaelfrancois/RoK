using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;
using Rok.Shared;

namespace Rok.Import.Services;

public class PostImportDominantColorTask(IDominantColorCalculator calculator,
    IAlbumRepository albumRepository,
    IAlbumPictureService albumPictureService,
    IArtistRepository artistRepository,
    IArtistPictureService artistPictureService,
    ILogger<PostImportDominantColorTask> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Post import dominant color task"))
        {
            await ProcessAlbumsAsync(cancellationToken);
            await ProcessArtistsAsync(cancellationToken);
        }
    }


    public async Task ProcessAlbumsAsync(CancellationToken cancellationToken)
    {
        int count = 0;
        IEnumerable<AlbumEntity> albums = await albumRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (AlbumEntity album in albums.Where(c => !c.PictureDominantColor.HasValue))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!albumPictureService.PictureExists(album.AlbumPath))
                continue;

            count++;

            string filePath = albumPictureService.GetPictureFilePath(album.AlbumPath);

            long? color = await calculator.CalculateAsync(filePath);
            if (color.HasValue)
                await albumRepository.UpdatePictureDominantColorAsync(album.Id, color, RepositoryConnectionKind.Background);
        }

        logger.LogInformation("Dominant color calculated for {Count} albums", count);
    }


    public async Task ProcessArtistsAsync(CancellationToken cancellationToken)
    {
        int count = 0;
        IEnumerable<ArtistEntity> artists = await artistRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (ArtistEntity artist in artists.Where(c => !c.PictureDominantColor.HasValue))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!artistPictureService.PictureExists(artist.Name))
                continue;

            count++;

            string filePath = artistPictureService.GetPictureFilePath(artist.Name);

            long? color = await calculator.CalculateAsync(filePath);
            if (color.HasValue)
                await artistRepository.UpdatePictureDominantColorAsync(artist.Id, color, RepositoryConnectionKind.Background);
        }

        logger.LogInformation("Dominant color calculated for {Count} artists", count);
    }
}