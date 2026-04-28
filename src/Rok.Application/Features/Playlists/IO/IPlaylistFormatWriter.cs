namespace Rok.Application.Features.Playlists.IO;

public interface IPlaylistFormatWriter
{
    ExportPlaylistFormat Format { get; }

    Task WriteAsync(Stream stream, PlaylistFileModel model, CancellationToken cancellationToken);
}
