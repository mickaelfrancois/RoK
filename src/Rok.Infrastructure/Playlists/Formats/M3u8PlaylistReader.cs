using System.Globalization;
using System.Text;
using Rok.Application.Features.Playlists.IO;

namespace Rok.Infrastructure.Playlists.Formats;

public sealed class M3u8PlaylistReader : IPlaylistFormatReader
{
    private const string ExtinfPrefix = "#EXTINF:";
    private const string ArtistTitleSeparator = " - ";

    public ExportPlaylistFormat Format => ExportPlaylistFormat.M3u8;

    public async Task<PlaylistFileModel> ReadAsync(Stream stream, string fileNameHint, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

        List<PlaylistFileEntry> entries = new();
        TimeSpan? pendingDuration = null;
        string? pendingArtist = null;
        string? pendingTitle = null;

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string trimmed = line.Trim();

            if (trimmed.Length == 0)
                continue;

            if (trimmed.StartsWith(ExtinfPrefix, StringComparison.Ordinal))
            {
                ParseExtinf(trimmed, out pendingDuration, out pendingArtist, out pendingTitle);
                continue;
            }

            if (trimmed[0] == '#')
                continue;

            entries.Add(new PlaylistFileEntry(trimmed, pendingTitle, pendingArtist, pendingDuration));
            pendingDuration = null;
            pendingArtist = null;
            pendingTitle = null;
        }

        string name = Path.GetFileNameWithoutExtension(fileNameHint);
        return new PlaylistFileModel(name, entries);
    }

    private static void ParseExtinf(string line, out TimeSpan? duration, out string? artist, out string? title)
    {
        duration = null;
        artist = null;
        title = null;

        ReadOnlySpan<char> payload = line.AsSpan(ExtinfPrefix.Length);
        int comma = payload.IndexOf(',');

        ReadOnlySpan<char> durationSpan = comma >= 0 ? payload[..comma] : payload;
        if (int.TryParse(durationSpan.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) && seconds >= 0)
            duration = TimeSpan.FromSeconds(seconds);

        if (comma < 0)
            return;

        string label = payload[(comma + 1)..].Trim().ToString();
        if (label.Length == 0)
            return;

        int sep = label.IndexOf(ArtistTitleSeparator, StringComparison.Ordinal);
        if (sep < 0)
        {
            title = label;
            return;
        }

        string a = label[..sep].Trim();
        string t = label[(sep + ArtistTitleSeparator.Length)..].Trim();
        artist = a.Length == 0 ? null : a;
        title = t.Length == 0 ? null : t;
    }
}
