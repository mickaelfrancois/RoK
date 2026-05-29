namespace Rok.Application.Interfaces.Pictures;

public interface IRadioPictureService
{
    bool PictureExists(long stationId);

    string GetPictureFilePath(long stationId);

    Task DeletePictureAsync(long stationId, CancellationToken cancellationToken = default);
}
