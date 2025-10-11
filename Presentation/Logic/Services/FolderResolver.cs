using System.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace Rok.Logic.Services;

public class FolderResolver(ILogger<FolderResolver> logger) : IFolderResolver
{
    public async Task<string?> ResolveLibraryTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
            {
                StorageFolder folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token).AsTask(cancellationToken).ConfigureAwait(false);
                return folder?.Path;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while resolving path token: {Token}", token);
        }

        return null;
    }
}
