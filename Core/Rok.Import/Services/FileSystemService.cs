using Microsoft.Extensions.Logging;
using Rok.Application.Tag;
using Rok.Shared;

namespace Rok.Import.Services;

public class FileSystemService(ILogger<FileSystemService> logger)
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".flac"
    };

    private const int MinParallelDegree = 1;
    private const int MaxParallelDegree = 8;

    public List<string> GetFoldersByCreationDate(string path)
    {
        using PerfLogger perf = new(logger);

        return HandleDirectoryOperation(() =>
        {
            EnumerationOptions enumerationOptions = new()
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            };

            int maxDegree = Math.Clamp(Environment.ProcessorCount / 2, MinParallelDegree, MaxParallelDegree);

            return Directory.EnumerateDirectories(path, "*", enumerationOptions)
                        .AsParallel()
                        .WithDegreeOfParallelism(maxDegree)
                        .Where(di => !di.Contains("@Artist", StringComparison.OrdinalIgnoreCase))
                        .Select(dirPath => new DirectoryInfo(dirPath))
                        .OrderByDescending(di => di.CreationTime)
                        .Select(c => c.FullName)
                        .ToList();
        }, [], path);
    }

    public List<TrackFile> GetMusicFiles(string path, Action<string, TrackFile> fillBasicProperties)
    {
        List<TrackFile> files = [];

        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (!IsValidMusicFile(file))
                    continue;

                try
                {
                    TrackFile trackFile = new();
                    fillBasicProperties(file, trackFile);
                    files.Add(trackFile);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "An exception occurred while reading file properties '{File}'", file);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied to folder '{Path}'", path);
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogError(ex, "The folder was not found: '{Path}'", path);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "I/O exception while accessing folder: '{Path}'", path);
        }

        return files;
    }

    private bool IsValidMusicFile(string filePath)
    {
        if (!ValidExtensions.Contains(Path.GetExtension(filePath)))
            return false;

        if (FileHelpers.IsOnline(filePath))
            return false;

        return true;
    }

    private T HandleDirectoryOperation<T>(Func<T> operation, T defaultValue, string path)
    {
        try
        {
            return operation();
        }
        catch (AggregateException aggEx)
        {
            foreach (Exception ex in aggEx.InnerExceptions)
            {
                logger.LogError(aggEx, "An error occurred while accessing folders in {Path}: {ExceptionMessage}", path, ex.Message);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied to one or more folders in {Path}: {ExceptionMessage}", path, ex.Message);
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogError(ex, "The folder was not found: {Path}: {ExceptionMessage}", path, ex.Message);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "I/O exception while accessing folder: {Path}: {ExceptionMessage}", path, ex.Message);
        }

        return defaultValue;
    }
}