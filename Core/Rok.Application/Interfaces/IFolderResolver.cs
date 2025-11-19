namespace Rok.Application.Interfaces;

public interface IFolderResolver
{
    Task<string?> GetDisplayNameFromTokenAsync(string token);

    Task<List<string>> GetPathFromTokenAsync(string token);
}
