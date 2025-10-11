using Rok.Application.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Rok.Infrastructure.Files;

public class SettingsFileService(string applicationPath) : ISettingsFile
{
    private readonly string _path = Path.Combine(applicationPath, "settings.json");

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public bool Exists()
    {
        return Path.Exists(_path);
    }


    public IAppOptions? Load<T>() where T : IAppOptions
    {
        if (Path.Exists(_path))
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), _jsonOptions);
        else
            return null;
    }


    public void Save(IAppOptions options)
    {
        string jsonString = JsonSerializer.Serialize(options, _jsonOptions);
        File.WriteAllText(_path, jsonString, Encoding.UTF8);
    }
}
