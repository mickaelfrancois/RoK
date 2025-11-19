namespace Rok.Application.Interfaces;

public interface ISettingsFile
{
    bool Exists();

    IAppOptions? Load<T>() where T : IAppOptions;

    void Save(IAppOptions options);

    Task RemoveInvalidLibraryTokensAsync(IAppOptions options);
}