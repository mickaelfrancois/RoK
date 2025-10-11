using BenchmarkDotNet.Attributes;

namespace Melodia.PerfTests;

[MemoryDiagnoser]
public class GetFoldersByCreatDate
{
    readonly string _path = "d:\\Musique";

    [Benchmark]
    public List<string> GetFoldersByCreatDate_GetDirectories()
    {
        return Directory.GetDirectories(_path, "*", SearchOption.AllDirectories)
                     .Where(di => !di.Contains("@Artist", StringComparison.OrdinalIgnoreCase))
                     .Select(path => new DirectoryInfo(path))
                    .OrderByDescending(di => di.CreationTime)
                    .Select(c => c.FullName)
                    .ToList();
    }

    [Benchmark]
    public List<string> GetFoldersByCreatDate_EnumerateDirectories()
    {
        return Directory.EnumerateDirectories(_path, "*", SearchOption.AllDirectories)
                    .Where(di => !di.Contains("@Artist", StringComparison.OrdinalIgnoreCase))
                    .Select(path => new DirectoryInfo(path))
                    .OrderByDescending(di => di.CreationTime)
                    .Select(c => c.FullName)
                    .ToList();
    }
}
