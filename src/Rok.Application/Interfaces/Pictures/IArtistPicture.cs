namespace Rok.Application.Interfaces.Pictures;

public interface IArtistPicture
{
    void SetRepositoryArtistPath(string path);

    string GetArtistFolder(string artistName);

    string GetPictureFile(string artistName);

    bool PictureFileExists(string artistName);

    /// <summary>
    /// Tells what <see cref="MoveFolder"/> would do, without touching the disk.
    /// The folder holds the artist picture and the backdrops.
    /// </summary>
    /// <param name="oldName">Current artist name.</param>
    /// <param name="newName">New artist name.</param>
    /// <param name="otherArtistNames">Names of the other living artists, to detect a shared target folder.</param>
    ArtistFolderMoveOutcome CheckFolderMove(string oldName, string newName, IReadOnlyCollection<string> otherArtistNames);

    /// <summary>
    /// Moves the image folder of the old name to the folder of the new name.
    /// Never touches the <c>@Artists</c> root, never overwrites a folder of another living artist.
    /// </summary>
    /// <returns>The outcome applied. <see cref="ArtistFolderMoveOutcome.Blocked"/> means nothing was moved.</returns>
    ArtistFolderMoveOutcome MoveFolder(string oldName, string newName);
}