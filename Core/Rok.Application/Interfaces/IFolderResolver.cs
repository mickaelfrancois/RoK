namespace Rok.Application.Interfaces;

public interface IFolderResolver
{
    Task<string?> ResolveLibraryTokenAsync(string token, CancellationToken cancellationToken = default);
}
