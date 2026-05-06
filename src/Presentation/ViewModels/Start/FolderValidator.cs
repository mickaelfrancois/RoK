using System.IO;

namespace Rok.ViewModels.Start;

public enum FolderValidationResult
{
    Valid,
    AccessDenied,
    NoAudioFiles
}

public static class FolderValidator
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".flac"
    };

    public static Task<FolderValidationResult> ValidateAsync(string folderPath) =>
        Task.Run(() =>
        {
            try
            {
                EnumerationOptions options = new()
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false
                };

                bool hasAudio = Directory.EnumerateFiles(folderPath, "*.*", options)
                    .Any(f => ValidExtensions.Contains(Path.GetExtension(f)));

                return hasAudio ? FolderValidationResult.Valid : FolderValidationResult.NoAudioFiles;
            }
            catch (UnauthorizedAccessException)
            {
                return FolderValidationResult.AccessDenied;
            }
        });
}
