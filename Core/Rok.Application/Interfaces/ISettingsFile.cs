namespace Rok.Application.Interfaces;

public interface ISettingsFile
{
    bool Exists();

    Task<IAppOptions?> LoadAsync<T>() where T : IAppOptions;

    Task SaveAsync(IAppOptions options);

    Task RemoveInvalidLibraryTokensAsync(IAppOptions options);
}