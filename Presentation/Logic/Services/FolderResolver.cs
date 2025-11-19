using Windows.Storage;
using Windows.Storage.AccessCache;

namespace Rok.Logic.Services;

public class FolderResolver(ILogger<FolderResolver> logger) : IFolderResolver
{
    public async Task<string?> GetDisplayNameFromTokenAsync(string token)
    {
        StorageFolder? folder = await GetFolderFromTokenAsync(token);

        return folder?.DisplayName ?? folder?.Path;
    }


    public async Task<List<string>> GetPathFromTokenAsync(string token)
    {
        StorageFolder? folder = await GetFolderFromTokenAsync(token);

        if (folder is null)
            return [];

        bool isLibraryFolder = IsLibraryFolder(folder);

        if (isLibraryFolder)
        {
            return await GetPathFromMusicLibraryAsync();
        }
        else
            return [folder.Path];
    }


    private async Task<StorageFolder?> GetFolderFromTokenAsync(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
                return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while resolving folder from token: {Token}", token);
        }

        return null;
    }


    private bool IsLibraryFolder(StorageFolder folder)
    {
        try
        {
            StorageFolder musicLibrary = KnownFolders.MusicLibrary;

            return folder.FolderRelativeId.StartsWith(musicLibrary.FolderRelativeId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while checking if folder is library folder: {Folder}", folder.Path);
        }

        return false;
    }


    private async Task<List<string>> GetPathFromMusicLibraryAsync()
    {
        List<string> paths = [];

        try
        {
            StorageLibrary musicFolder = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Music);
            paths.AddRange(musicFolder.Folders.Select(f => f.Path));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while getting paths from library folder");
        }

        return paths;
    }
}
