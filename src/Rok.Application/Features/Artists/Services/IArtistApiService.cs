using Rok.Application.Dto;
using Rok.Application.Interfaces.Pictures;

namespace Rok.Application.Features.Artists.Services;

public interface IArtistApiService
{
    Task<ArtistApiUpdateResult> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture);
}
