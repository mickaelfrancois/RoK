using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;

namespace Rok.Infrastructure.Files;

public class RadioPicture : IRadioPicture
{
    private const string KRadioFolderName = "@Radios";
    private const string KExtension = ".png";

    public string RepositoryRadioPath { get; private set; } = string.Empty;

    private readonly IFileSystem _fileSystem;

    public RadioPicture(IFileSystem fileSystem, IAppOptions options)
    {
        _fileSystem = fileSystem;
        SetRepositoryRadioPath(options.CachePath);
    }

    public void SetRepositoryRadioPath(string path)
    {
        Guard.NotNullOrEmpty(path);

        RepositoryRadioPath = Path.Combine(path, KRadioFolderName);

        _fileSystem.DirectoryCreate(RepositoryRadioPath);
    }

    public string GetPictureFile(long stationId)
    {
        return Path.Combine(RepositoryRadioPath, $"{stationId}{KExtension}");
    }

    public bool PictureFileExists(long stationId)
    {
        return _fileSystem.FileExists(GetPictureFile(stationId));
    }

    public Task DeletePictureFileAsync(long stationId, CancellationToken cancellationToken = default)
    {
        string path = GetPictureFile(stationId);

        if (!_fileSystem.FileExists(path))
            return Task.CompletedTask;

        return _fileSystem.DeleteFileAsync(path, cancellationToken);
    }
}
