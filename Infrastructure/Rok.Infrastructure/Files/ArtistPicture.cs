using Rok.Application.Interfaces;
using Rok.Shared.Extensions;

namespace Rok.Infrastructure.Files;

public class ArtistPicture : IArtistPicture
{
    private const string KArtistFolderName = "@Artists";
    public const string KArtistFileName = "artist.jpg";


    public string RepositoryArtistPath { get; private set; } = string.Empty;

    private readonly IFileSystem _fileSystem;


    public ArtistPicture(IFileSystem fileSystem, IAppOptions options)
    {
        _fileSystem = fileSystem;
        SetRepositoryArtistPath(options.CachePath);
    }

    public void SetRepositoryArtistPath(string path)
    {
        Guard.Against.NullOrEmpty(path);

        RepositoryArtistPath = Path.Combine(path, KArtistFolderName);

        EnsureFolderExists(RepositoryArtistPath);
    }


    public string GetArtistFolder(string artistName)
    {
        Guard.Against.NullOrEmpty(artistName);

        return Path.Combine(RepositoryArtistPath, artistName.ToFileName());
    }


    public string GetPictureFile(string artistName)
    {
        Guard.Against.NullOrEmpty(artistName);

        return Path.Combine(RepositoryArtistPath, artistName.ToFileName(), KArtistFileName);
    }


    public bool PictureFileExists(string artistName)
    {
        Guard.Against.NullOrEmpty(artistName);

        return _fileSystem.FileExists(GetPictureFile(artistName));
    }


    private void EnsureFolderExists(string path)
    {
        _fileSystem.DirectoryCreate(path);
    }
}
