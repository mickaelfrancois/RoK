namespace Rok.Application.Features.Playlists.IO;

public interface IPlaylistFormatReader
{
    ExportPlaylistFormat Format { get; }

    Task<PlaylistFileModel> ReadAsync(Stream stream, string fileNameHint, CancellationToken cancellationToken);
}
