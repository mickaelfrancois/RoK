using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Shared.Extensions;

namespace Rok.Infrastructure.Files;

public class BackdropPicture
{
    private const string KArtistFolderName = "@Artists";

    public string RepositoryArtistPath { get; private set; } = string.Empty;

    private readonly ILogger<BackdropPicture> _logger;

    public BackdropPicture(IAppOptions options, ILogger<BackdropPicture> logger)
    {
        _logger = logger;
        SetRepositoryArtistPath(options.CachePath);
    }

    public void SetRepositoryArtistPath(string path)
    {
        Guard.Against.NullOrEmpty(path);

        RepositoryArtistPath = Path.Combine(path, KArtistFolderName);

        EnsureFolderExists(RepositoryArtistPath);
    }

    public string GetArtistPictureFolder(string artistName)
    {
        Guard.Against.NullOrEmpty(artistName);

        return Path.Combine(RepositoryArtistPath, artistName.ToFileName());
    }


    public List<string> GetBackdrops(string artistName)
    {
        Guard.Against.NullOrEmpty(artistName);

        string picturePath = GetArtistPictureFolder(artistName);

        if (!Directory.Exists(picturePath))
        {
            EnsureFolderExists(picturePath);
            return [];
        }

        List<string> backdrops = [];

        string[] files = Directory.GetFiles(picturePath, "backdrop*", SearchOption.TopDirectoryOnly);

        foreach (string file in files)
        {
            try
            {
                FileInfo fileInfo = new(file);

                if (fileInfo.Length > 0)
                    backdrops.Add(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while loading picture '{File}': {Message}.", file, ex.Message);
            }
        }

        backdrops.Shuffle();

        return backdrops;
    }


    public bool HasBackdrops(string artistName)
    {
        Guard.Against.NullOrEmpty(artistName, nameof(artistName));

        string picturePath = GetArtistPictureFolder(artistName);

        if (!Directory.Exists(picturePath))
        {
            EnsureFolderExists(picturePath);
            return false;
        }

        string[] files = Directory.GetFiles(picturePath, "backdrop*", SearchOption.TopDirectoryOnly);
        return files.Length != 0;
    }


    public string GetRandomGenericBackdrop()
    {
        int index = Random.Shared.Next(1, 12);

        return $"ms-appx:///Assets/Backdrop/wallpaper{index}.jpg";
    }

    private static void EnsureFolderExists(string path)
    {
        Directory.CreateDirectory(path);
    }
}
