using System.IO;
using Rok.Shared;

namespace Rok.ViewModels.Start;

public enum FolderValidationResult
{
    Valid,
    AccessDenied,
    NoAudioFiles
}

public sealed record FolderScanResult(FolderValidationResult Status, IReadOnlyDictionary<string, int> UnsupportedCounts);

public static class FolderValidator
{
    private static readonly IReadOnlyDictionary<string, int> NoCounts = new Dictionary<string, int>();

    public static async Task<FolderValidationResult> ValidateAsync(string folderPath)
    {
        FolderScanResult scan = await ScanAsync(folderPath);

        return scan.Status;
    }

    public static Task<FolderScanResult> ScanAsync(string folderPath) =>
        Task.Run(() =>
        {
            try
            {
                EnumerationOptions options = new()
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false
                };

                Dictionary<string, int> unsupportedCounts = new(StringComparer.Ordinal);

                foreach (string file in Directory.EnumerateFiles(folderPath, "*.*", options))
                {
                    string extension = Path.GetExtension(file);

                    if (AudioFormats.Supported.Contains(extension))
                        return new FolderScanResult(FolderValidationResult.Valid, NoCounts);

                    if (AudioFormats.KnownUnsupported.Contains(extension))
                    {
                        string key = extension.ToLowerInvariant();
                        unsupportedCounts[key] = unsupportedCounts.GetValueOrDefault(key) + 1;
                    }
                }

                return new FolderScanResult(FolderValidationResult.NoAudioFiles, unsupportedCounts);
            }
            catch (UnauthorizedAccessException)
            {
                return new FolderScanResult(FolderValidationResult.AccessDenied, NoCounts);
            }
            catch (IOException)
            {
                return new FolderScanResult(FolderValidationResult.AccessDenied, NoCounts);
            }
        });
}