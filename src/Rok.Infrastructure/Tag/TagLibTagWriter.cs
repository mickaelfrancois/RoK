using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Shared;

namespace Rok.Infrastructure.Tag;

/// <summary>
/// Writes tags into audio files through TagLibSharp, working on a temporary copy that replaces
/// the original only once it is verified. Pending writes are journaled so they can be undone at startup.
/// </summary>
public sealed class TagLibTagWriter : ITagWriter, ITagWriteRecovery
{
    private const string TemporaryExtension = ".roktmp";
    private const string BackupExtension = ".rokold";
    private const long FreeSpaceMargin = 1024 * 1024;

    private static readonly FrozenDictionary<string, ITagFormat> Formats = new Dictionary<string, ITagFormat>(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = new Mp3TagFormat(),
        [".flac"] = new FlacTagFormat(),
        [".m4a"] = new Mp4TagFormat()
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly TagWriteJournal _journal;
    private readonly ITagWriteFileOperations _files;
    private readonly ILogger<TagLibTagWriter> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Creates a writer.
    /// </summary>
    /// <param name="applicationLocalPath">Folder where the write journal is kept.</param>
    /// <param name="logger">Logger.</param>
    public TagLibTagWriter(string applicationLocalPath, ILogger<TagLibTagWriter> logger)
        : this(applicationLocalPath, logger, new DefaultTagWriteFileOperations())
    {
    }

    internal TagLibTagWriter(string applicationLocalPath, ILogger<TagLibTagWriter> logger, ITagWriteFileOperations files)
    {
        _journal = new TagWriteJournal(applicationLocalPath);
        _logger = logger;
        _files = files;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagWriteResult>> WriteAsync(IReadOnlyList<TagWriteRequest> requests, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(CancellationToken.None);

        try
        {
            List<TagWriteResult> results = new(requests.Count);

            foreach (TagWriteRequest request in requests)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    results.Add(Skipped(request.FilePath, ETagWriteReason.Cancelled));
                    continue;
                }

                results.Add(await Task.Run(() => WriteFile(request), CancellationToken.None));
            }

            return results;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagWriteCheck>> CheckAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            List<TagWriteCheck> checks = new(paths.Count);

            foreach (string path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                checks.Add(await Task.Run(() => CheckFile(path), CancellationToken.None));
            }

            return checks;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await Task.Run(RecoverEntries, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void RecoverEntries()
    {
        foreach (TagWriteJournalEntry entry in _journal.GetEntries())
        {
            if (TryRestore(entry))
            {
                TryRemoveFromJournal(entry);
                _logger.LogWarning("Recovered an interrupted tag write on {File}", entry.Original);
            }
            else
            {
                _logger.LogWarning("Could not recover an interrupted tag write on {File}, will retry at next start", entry.Original);
            }
        }
    }

    private TagWriteCheck CheckFile(string path)
    {
        try
        {
            ETagWriteReason reason = CheckPreconditions(path, out ITagFormat? format);

            if (reason != ETagWriteReason.None || format is null)
                return new TagWriteCheck(path, reason, null);

            TagFieldValues current = TagFieldReader.ReadFile(path, format.MimeType);
            FileInfo info = new(path);

            return new TagWriteCheck(path, ETagWriteReason.None, current.Artist)
            {
                Current = current,
                FileDateModified = new DateTimeOffset(info.LastWriteTimeUtc),
                Size = info.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tag write pre-check failed for {File}", path);

            return new TagWriteCheck(path, MapReason(ex), null);
        }
    }

    private TagWriteResult WriteFile(TagWriteRequest request)
    {
        string path = request.FilePath;
        TagWriteJournalEntry? entry = null;

        try
        {
            ETagWriteReason skipReason = CheckPreconditions(path, out ITagFormat? format);

            if (skipReason != ETagWriteReason.None || format is null)
                return Skipped(path, skipReason);

            TagChanges changes = request.Changes;

            if (!changes.HasChanges)
                return Succeeded(path);

            string id = Guid.NewGuid().ToString("N");
            entry = new TagWriteJournalEntry(path, $"{path}.{id}{TemporaryExtension}", $"{path}.{id}{BackupExtension}");

            _journal.Add(entry);
            _files.Copy(entry.Original, entry.Temp);

            format.Write(entry.Temp, changes);
            _files.AfterTagWrite(entry.Temp);

            if (!IsCopyValid(format, entry, changes))
            {
                _logger.LogWarning("Verification of the tag copy failed for {File}", path);
                CleanUp(entry);

                return Failed(path, ETagWriteReason.VerificationFailed);
            }

            bool clean = ReplaceOriginal(entry);
            TagWriteResult result = ReadBackAfterReplace(path, format, changes);

            if (clean)
                TryRemoveFromJournal(entry);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tag write failed for {File}", path);

            if (entry is not null)
                CleanUp(entry);

            return Failed(path, MapReason(ex));
        }
    }

    private static ETagWriteReason CheckPreconditions(string path, out ITagFormat? format)
    {
        format = null;

        if (!File.Exists(path))
            return ETagWriteReason.NotFound;

        if (!Formats.TryGetValue(Path.GetExtension(path), out ITagFormat? found))
            return ETagWriteReason.UnsupportedFormat;

        if (File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
            return ETagWriteReason.ReadOnly;

        if (FileHelpers.IsOnline(path))
            return ETagWriteReason.CloudPlaceholder;

        if (IsLocked(path))
            return ETagWriteReason.Locked;

        if (!HasEnoughFreeSpace(path))
            return ETagWriteReason.InsufficientDiskSpace;

        ETagWriteReason formatReason = found.CheckFile(path);

        if (formatReason != ETagWriteReason.None)
            return formatReason;

        format = found;

        return ETagWriteReason.None;
    }

    private static bool IsLocked(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.None);

            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static bool HasEnoughFreeSpace(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            DriveInfo drive = new(Path.GetPathRoot(fullPath)!);

            return drive.AvailableFreeSpace >= new FileInfo(fullPath).Length + FreeSpaceMargin;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsCopyValid(ITagFormat format, TagWriteJournalEntry entry, TagChanges changes)
    {
        TagFingerprint original = format.ReadFingerprint(entry.Original, changes);
        TagFingerprint copy = format.ReadFingerprint(entry.Temp, changes);

        return copy.Values == original.Values.WithChanges(changes)
            && copy.Duration == original.Duration
            && copy.Bitrate == original.Bitrate
            && copy.SampleRate == original.SampleRate
            && copy.AudioHash == original.AudioHash
            && copy.PreservedFields.SequenceEqual(original.PreservedFields);
    }

    private bool ReplaceOriginal(TagWriteJournalEntry entry)
    {
        try
        {
            _files.Replace(entry.Temp, entry.Original);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            _logger.LogWarning(ex, "File.Replace failed for {File}, falling back to copy and rename", entry.Original);
        }

        _files.Move(entry.Original, entry.Backup);
        _files.Move(entry.Temp, entry.Original);

        try
        {
            _files.Delete(entry.Backup);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the backup of {File}, it will be removed at next start", entry.Original);

            return false;
        }
    }

    private void CleanUp(TagWriteJournalEntry entry)
    {
        if (TryRestore(entry))
            TryRemoveFromJournal(entry);
    }

    private bool TryRestore(TagWriteJournalEntry entry)
    {
        try
        {
            if (!File.Exists(entry.Original) && File.Exists(entry.Backup))
                _files.Move(entry.Backup, entry.Original);

            if (File.Exists(entry.Temp))
                _files.Delete(entry.Temp);

            if (File.Exists(entry.Backup))
                _files.Delete(entry.Backup);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not clean up the temporary files of {File}", entry.Original);

            return false;
        }
    }

    private void TryRemoveFromJournal(TagWriteJournalEntry entry)
    {
        try
        {
            _journal.Remove(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not update the tag write journal for {File}", entry.Original);
        }
    }

    private static ETagWriteReason MapReason(Exception exception)
    {
        const int ErrorHandleDiskFull = 0x27;
        const int ErrorDiskFull = 0x70;

        if (exception is IOException io && ((io.HResult & 0xFFFF) is ErrorHandleDiskFull or ErrorDiskFull))
            return ETagWriteReason.InsufficientDiskSpace;

        return ETagWriteReason.IoError;
    }

    private TagWriteResult ReadBackAfterReplace(string path, ITagFormat format, TagChanges changes)
    {
        try
        {
            return Succeeded(path, format, changes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tags written on {File} but the read-back failed, the result carries no values", path);
        }

        try
        {
            return Succeeded(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tags written on {File} but its file information could not be read", path);

            return new TagWriteResult(path, ETagWriteOutcome.Succeeded, ETagWriteReason.None, null, null);
        }
    }

    private static TagWriteResult Succeeded(string path, ITagFormat format, TagChanges changes)
    {
        TagFieldValues values = TagFieldReader.ReadFile(path, format.MimeType);

        values = values with
        {
            Artist = changes.Artist is null ? "" : values.Artist,
            Album = changes.Album is null ? "" : values.Album,
            Genre = changes.Genre is null ? "" : values.Genre,
            Year = changes.Year is null ? null : values.Year,
            IsCompilation = changes.IsCompilation is not null && values.IsCompilation
        };

        return Succeeded(path) with { Values = values };
    }

    private static TagWriteResult Succeeded(string path)
    {
        FileInfo info = new(path);

        return new TagWriteResult(path, ETagWriteOutcome.Succeeded, ETagWriteReason.None, new DateTimeOffset(info.LastWriteTimeUtc), info.Length);
    }

    private static TagWriteResult Skipped(string path, ETagWriteReason reason)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Skipped, reason, null, null);
    }

    private static TagWriteResult Failed(string path, ETagWriteReason reason)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Failed, reason, null, null);
    }

}