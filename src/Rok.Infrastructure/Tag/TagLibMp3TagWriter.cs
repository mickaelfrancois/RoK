using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Rok.Application.Tag;
using Rok.Shared;

namespace Rok.Infrastructure.Tag;

/// <summary>
/// Writes ID3 tags into MP3 files through TagLibSharp, working on a temporary copy that replaces
/// the original only once it is verified. Pending writes are journaled so they can be undone at startup.
/// </summary>
public sealed class TagLibMp3TagWriter : ITagWriter, ITagWriteRecovery
{
    private const string Mp3MimeType = "taglib/mp3";
    private const string Mp3Extension = ".mp3";
    private const string TemporaryExtension = ".roktmp";
    private const string BackupExtension = ".rokold";
    private const long FreeSpaceMargin = 1024 * 1024;
    private const int Id3v22MajorVersion = 2;
    private const string TitleFrameId = "TIT2";

    private static readonly TagLib.TagTypes[] SecondaryTagTypes = [TagLib.TagTypes.Id3v1, TagLib.TagTypes.Ape];

    private readonly TagWriteJournal _journal;
    private readonly ITagWriteFileOperations _files;
    private readonly ILogger<TagLibMp3TagWriter> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Creates a writer.
    /// </summary>
    /// <param name="applicationLocalPath">Folder where the write journal is kept.</param>
    /// <param name="logger">Logger.</param>
    public TagLibMp3TagWriter(string applicationLocalPath, ILogger<TagLibMp3TagWriter> logger)
        : this(applicationLocalPath, logger, new DefaultTagWriteFileOperations())
    {
    }

    internal TagLibMp3TagWriter(string applicationLocalPath, ILogger<TagLibMp3TagWriter> logger, ITagWriteFileOperations files)
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

    private TagWriteResult WriteFile(TagWriteRequest request)
    {
        string path = request.FilePath;
        TagWriteJournalEntry? entry = null;

        try
        {
            ETagWriteReason skipReason = CheckPreconditions(path);

            if (skipReason != ETagWriteReason.None)
                return Skipped(path, skipReason);

            string? title = request.Changes.Title;

            if (title is null)
                return Succeeded(path);

            string id = Guid.NewGuid().ToString("N");
            entry = new TagWriteJournalEntry(path, $"{path}.{id}{TemporaryExtension}", $"{path}.{id}{BackupExtension}");

            _journal.Add(entry);
            _files.Copy(entry.Original, entry.Temp);

            WriteTitle(entry.Temp, title);

            if (!IsCopyValid(entry, title))
            {
                _logger.LogWarning("Verification of the tag copy failed for {File}", path);
                CleanUp(entry);

                return Failed(path, ETagWriteReason.VerificationFailed);
            }

            bool clean = ReplaceOriginal(entry);
            TagWriteResult result = Succeeded(path);

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

    private static ETagWriteReason CheckPreconditions(string path)
    {
        if (!File.Exists(path))
            return ETagWriteReason.NotFound;

        if (!string.Equals(Path.GetExtension(path), Mp3Extension, StringComparison.OrdinalIgnoreCase))
            return ETagWriteReason.UnsupportedFormat;

        if (File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
            return ETagWriteReason.ReadOnly;

        if (FileHelpers.IsOnline(path))
            return ETagWriteReason.CloudPlaceholder;

        if (IsLocked(path))
            return ETagWriteReason.Locked;

        if (!HasEnoughFreeSpace(path))
            return ETagWriteReason.InsufficientDiskSpace;

        if (HasId3v22Tag(path))
            return ETagWriteReason.UnsupportedTagVersion;

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

    private static bool HasId3v22Tag(string path)
    {
        using TagLib.File file = TagLib.File.Create(path, Mp3MimeType, TagLib.ReadStyle.None);

        return file.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag { Version: Id3v22MajorVersion };
    }

    private static void WriteTitle(string path, string title)
    {
        using TagLib.File file = TagLib.File.Create(path, Mp3MimeType, TagLib.ReadStyle.Average);

        TagLib.Id3v2.Tag id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
        id3v2.Title = title;

        foreach (TagLib.TagTypes type in SecondaryTagTypes)
        {
            if (file.TagTypesOnDisk.HasFlag(type))
                file.GetTag(type, false).Title = title;
            else
                file.RemoveTags(type);
        }

        file.Save();
    }

    private static bool IsCopyValid(TagWriteJournalEntry entry, string title)
    {
        AudioFingerprint original = ReadFingerprint(entry.Original);
        AudioFingerprint copy = ReadFingerprint(entry.Temp);

        return copy.Title == title
            && copy.Duration == original.Duration
            && copy.Bitrate == original.Bitrate
            && copy.SampleRate == original.SampleRate
            && copy.AudioHash == original.AudioHash
            && copy.FrameIds.SequenceEqual(original.FrameIds);
    }

    private static AudioFingerprint ReadFingerprint(string path)
    {
        long start;
        long end;
        TimeSpan duration;
        int bitrate;
        int sampleRate;
        string? title;
        string[] frameIds;

        using (TagLib.File file = TagLib.File.Create(path, Mp3MimeType, TagLib.ReadStyle.Average))
        {
            start = file.InvariantStartPosition;
            end = file.InvariantEndPosition;
            duration = file.Properties.Duration;
            bitrate = file.Properties.AudioBitrate;
            sampleRate = file.Properties.AudioSampleRate;

            TagLib.Id3v2.Tag? id3v2 = file.GetTag(TagLib.TagTypes.Id3v2, false) as TagLib.Id3v2.Tag;
            title = id3v2?.Title;
            frameIds = id3v2 is null
                ? []
                : id3v2.GetFrames()
                    .Select(f => f.FrameId.ToString())
                    .Where(id => id != TitleFrameId)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
        }

        return new AudioFingerprint(duration, bitrate, sampleRate, HashRange(path, start, end), frameIds, title);
    }

    private static string HashRange(string path, long start, long end)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(start, SeekOrigin.Begin);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long remaining = end - start;

        while (remaining > 0)
        {
            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));

            if (read == 0)
                break;

            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return Convert.ToHexString(hash.GetHashAndReset());
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

    private sealed record AudioFingerprint(
        TimeSpan Duration,
        int Bitrate,
        int SampleRate,
        string AudioHash,
        string[] FrameIds,
        string? Title);
}