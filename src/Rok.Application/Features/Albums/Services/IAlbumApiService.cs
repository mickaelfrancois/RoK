using Rok.Application.Dto;
using Rok.Application.Interfaces.Pictures;

namespace Rok.Application.Features.Albums.Services;

public interface IAlbumApiService
{
    Task<AlbumApiUpdateResult> GetAndUpdateAlbumDataAsync(AlbumDto album, IAlbumPictureService pictureService, CancellationToken cancellationToken = default);
}