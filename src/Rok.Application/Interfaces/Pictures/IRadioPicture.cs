namespace Rok.Application.Interfaces.Pictures;

public interface IRadioPicture
{
    void SetRepositoryRadioPath(string path);

    string GetPictureFile(long stationId);

    bool PictureFileExists(long stationId);

    Task DeletePictureFileAsync(long stationId, CancellationToken cancellationToken = default);
}
