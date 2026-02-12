using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Files;

public class SettingsFileService(string applicationPath, IFolderResolver folderResolver, IFileSystem fileSystem) : ISettingsFile
{
    private readonly string _path = fileSystem.Combine(applicationPath, "settings.json");

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public bool Exists()
    {
        return fileSystem.FileExists(_path);
    }

    public async Task<IAppOptions?> LoadAsync<T>() where T : IAppOptions
    {
        if (!fileSystem.FileExists(_path))
            return null;

        try
        {
            string content = await fileSystem.ReadAllTextAsync(_path);

            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(IAppOptions options)
    {
        Guard.Against.Null(options, nameof(options));

        string jsonString = JsonSerializer.Serialize(options, _jsonOptions);

        await fileSystem.WriteAllTextAsync(_path, jsonString);
    }

    public async Task RemoveInvalidLibraryTokensAsync(IAppOptions options)
    {
        Guard.Against.Null(options, nameof(options));

        if (options.LibraryTokens is null || options.LibraryTokens.Count == 0)
            return;

        List<string> tokensToRemove = [];

        foreach (string token in options.LibraryTokens)
        {
            string? path = await folderResolver.GetDisplayNameFromTokenAsync(token);
            if (path is null)
                tokensToRemove.Add(token);
        }

        if (tokensToRemove.Count > 0)
        {
            options.LibraryTokens.RemoveAll(token => tokensToRemove.Contains(token));
        }
    }
}