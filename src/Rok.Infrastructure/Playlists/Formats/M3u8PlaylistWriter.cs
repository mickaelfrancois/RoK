using System.Globalization;
using System.Text;
using Rok.Application.Features.Playlists.IO;

namespace Rok.Infrastructure.Playlists.Formats;

public sealed class M3u8PlaylistWriter : IPlaylistFormatWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public ExportPlaylistFormat Format => ExportPlaylistFormat.M3u8;

    public async Task WriteAsync(Stream stream, PlaylistFileModel model, CancellationToken cancellationToken)
    {
        await using StreamWriter writer = new(stream, Utf8NoBom, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\n"
        };

        await writer.WriteLineAsync("#EXTM3U".AsMemory(), cancellationToken);

        foreach (PlaylistFileEntry entry in model.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int seconds = entry.Duration.HasValue
                ? (int)Math.Round(entry.Duration.Value.TotalSeconds, MidpointRounding.AwayFromZero)
                : -1;

            string artist = entry.Artist ?? string.Empty;
            string title = entry.Title ?? string.Empty;
            string label = string.Concat(artist, " - ", title);

            string extinf = string.Create(CultureInfo.InvariantCulture, $"#EXTINF:{seconds},{label}");
            await writer.WriteLineAsync(extinf.AsMemory(), cancellationToken);
            await writer.WriteLineAsync(entry.FilePath.AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }
}