namespace Rok.Application.Features.Playlists.IO;

public interface IPlaylistFormatResolver
{
    bool TryGetReader(string extension, out IPlaylistFormatReader? reader);

    bool TryGetWriter(string extension, out IPlaylistFormatWriter? writer);
}