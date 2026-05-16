using Rok.Application.Features.Playlists.IO;

namespace Rok.Infrastructure.Playlists;

public sealed class PlaylistFormatResolver : IPlaylistFormatResolver
{
    private readonly IReadOnlyDictionary<string, IPlaylistFormatReader> _readers;
    private readonly IReadOnlyDictionary<string, IPlaylistFormatWriter> _writers;

    public PlaylistFormatResolver(IEnumerable<IPlaylistFormatReader> readers, IEnumerable<IPlaylistFormatWriter> writers)
    {
        Dictionary<string, IPlaylistFormatReader> readerMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IPlaylistFormatWriter> writerMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (IPlaylistFormatReader reader in readers)
        {
            foreach (string ext in ExtensionsForReader(reader.Format))
            {
                readerMap[ext] = reader;
            }
        }

        foreach (IPlaylistFormatWriter writer in writers)
        {
            foreach (string ext in ExtensionsForWriter(writer.Format))
            {
                writerMap[ext] = writer;
            }
        }

        _readers = readerMap;
        _writers = writerMap;
    }

    public bool TryGetReader(string extension, out IPlaylistFormatReader? reader)
    {
        bool found = _readers.TryGetValue(extension, out IPlaylistFormatReader? r);
        reader = found ? r : null;
        return found;
    }

    public bool TryGetWriter(string extension, out IPlaylistFormatWriter? writer)
    {
        bool found = _writers.TryGetValue(extension, out IPlaylistFormatWriter? w);
        writer = found ? w : null;
        return found;
    }

    private static IEnumerable<string> ExtensionsForReader(ExportPlaylistFormat format)
    {
        return format switch
        {
            ExportPlaylistFormat.M3u8 => new[] { ".m3u", ".m3u8" },
            _ => Array.Empty<string>()
        };
    }

    private static IEnumerable<string> ExtensionsForWriter(ExportPlaylistFormat format)
    {
        return format switch
        {
            ExportPlaylistFormat.M3u8 => new[] { ".m3u8" },
            _ => Array.Empty<string>()
        };
    }
}