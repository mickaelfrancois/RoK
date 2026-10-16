using System.Text.Json;

namespace Rok.Infrastructure.Tag;

/// <summary>
/// A tag write in progress: the original file, its temporary copy and the backup used by the fallback replace.
/// </summary>
internal sealed record TagWriteJournalEntry(string Original, string Temp, string Backup);

/// <summary>
/// Persists the tag writes in progress so an interrupted write can be undone at the next start.
/// The file is replaced atomically on every change.
/// </summary>
internal sealed class TagWriteJournal(string directory)
{
    private const string FileName = "tag-write-journal.json";

    private readonly string _path = Path.Combine(directory, FileName);
    private readonly Lock _lock = new();

    public IReadOnlyList<TagWriteJournalEntry> GetEntries()
    {
        lock (_lock)
        {
            return Load();
        }
    }

    public void Add(TagWriteJournalEntry entry)
    {
        lock (_lock)
        {
            List<TagWriteJournalEntry> entries = Load();
            entries.RemoveAll(e => e.Temp == entry.Temp);
            entries.Add(entry);
            Save(entries);
        }
    }

    public void Remove(TagWriteJournalEntry entry)
    {
        lock (_lock)
        {
            List<TagWriteJournalEntry> entries = Load();

            if (entries.RemoveAll(e => e.Temp == entry.Temp) == 0)
                return;

            Save(entries);
        }
    }

    private List<TagWriteJournalEntry> Load()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            string json = File.ReadAllText(_path);

            return JsonSerializer.Deserialize<List<TagWriteJournalEntry>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Save(List<TagWriteJournalEntry> entries)
    {
        Directory.CreateDirectory(directory);

        string temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}