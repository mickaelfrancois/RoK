using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
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
        Guard.NotNullOrEmpty(path);

        RepositoryArtistPath = Path.Combine(path, KArtistFolderName);

        EnsureFolderExists(RepositoryArtistPath);
    }


    public string GetArtistFolder(string artistName)
    {
        Guard.NotNullOrEmpty(artistName);

        return Path.Combine(RepositoryArtistPath, artistName.ToFileName());
    }


    public string GetPictureFile(string artistName)
    {
        Guard.NotNullOrEmpty(artistName);

        return Path.Combine(RepositoryArtistPath, artistName.ToFileName(), KArtistFileName);
    }


    public bool PictureFileExists(string artistName)
    {
        Guard.NotNullOrEmpty(artistName);

        return _fileSystem.FileExists(GetPictureFile(artistName));
    }


    public ArtistFolderMoveOutcome CheckFolderMove(string oldName, string newName, IReadOnlyCollection<string> otherArtistNames)
    {
        Guard.NotNullOrEmpty(oldName);
        Guard.NotNullOrEmpty(newName);
        ArgumentNullException.ThrowIfNull(otherArtistNames);

        string newFolderName = newName.ToFileName();

        if (string.IsNullOrWhiteSpace(newFolderName))
            return ArtistFolderMoveOutcome.Blocked;

        string oldFolderName = oldName.ToFileName();

        if (string.IsNullOrWhiteSpace(oldFolderName))
            return ArtistFolderMoveOutcome.NothingToDo;

        string source = Path.Combine(RepositoryArtistPath, oldFolderName);

        if (!Directory.Exists(source))
            return ArtistFolderMoveOutcome.NothingToDo;

        if (string.Equals(oldFolderName, newFolderName, StringComparison.Ordinal))
            return ArtistFolderMoveOutcome.NothingToDo;

        if (string.Equals(oldFolderName, newFolderName, StringComparison.OrdinalIgnoreCase))
            return ArtistFolderMoveOutcome.Move;

        string target = Path.Combine(RepositoryArtistPath, newFolderName);

        if (!Directory.Exists(target))
            return ArtistFolderMoveOutcome.Move;

        if (otherArtistNames.Any(name => IsFolderOf(name, newFolderName)))
            return ArtistFolderMoveOutcome.Blocked;

        return IsEmpty(target) ? ArtistFolderMoveOutcome.ReplaceEmptyTarget : ArtistFolderMoveOutcome.Merge;
    }


    public ArtistFolderMoveOutcome MoveFolder(string oldName, string newName)
    {
        ArtistFolderMoveOutcome outcome = CheckFolderMove(oldName, newName, []);

        if (outcome is ArtistFolderMoveOutcome.NothingToDo or ArtistFolderMoveOutcome.Blocked)
            return outcome;

        string source = Path.Combine(RepositoryArtistPath, oldName.ToFileName());
        string target = Path.Combine(RepositoryArtistPath, newName.ToFileName());

        switch (outcome)
        {
            case ArtistFolderMoveOutcome.Merge:
                MergeDirectory(source, target);
                Directory.Delete(source, true);
                break;

            case ArtistFolderMoveOutcome.ReplaceEmptyTarget:
                Directory.Delete(target);
                Directory.Move(source, target);
                break;

            default:
                MoveDirectory(source, target);
                break;
        }

        return outcome;
    }


    private static void MoveDirectory(string source, string target)
    {
        if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(source, target);
            return;
        }

        // Case-only rename: NTFS is case-insensitive, so go through a temporary name.
        string temporary = $"{source}.rename-{Guid.NewGuid():N}";

        Directory.Move(source, temporary);
        Directory.Move(temporary, target);
    }


    private static void MergeDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source))
            File.Move(file, Path.Combine(target, Path.GetFileName(file)), true);

        foreach (string directory in Directory.GetDirectories(source))
            MergeDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }


    private static bool IsEmpty(string folder)
    {
        return !Directory.EnumerateFileSystemEntries(folder).Any();
    }


    private static bool IsFolderOf(string artistName, string folderName)
    {
        return string.Equals(artistName.ToFileName(), folderName, StringComparison.OrdinalIgnoreCase);
    }


    private void EnsureFolderExists(string path)
    {
        _fileSystem.DirectoryCreate(path);
    }
}