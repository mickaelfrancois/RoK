using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Files;

public class AlbumPicture(IFileSystem _fileSystem) : IAlbumPicture
{
    public const string KCompilationFolderName = "Compilations";

    private readonly string[] _files = { "cover.jpg", "cover.png", "folder.jpg" };

    public bool PictureFileExists(string albumPath)
    {
        Guard.Against.NullOrEmpty(albumPath);

        return GetPictureFile(albumPath) != string.Empty;
    }

    public string GetPictureFile(string albumPath)
    {
        Guard.Against.NullOrEmpty(albumPath);

        for (int i = 0; i < _files.Length; i++)
        {
            string path = Path.Join(albumPath, _files[i]);

            if (_fileSystem.FileExists(path))
                return path;
        }

        return string.Empty;
    }
}
