using Rok.Shared.Enums;

namespace Rok.Application.Interfaces;

public interface IAppOptions
{
    Guid Id { get; set; }

    AppTheme Theme { get; set; }

    List<string> LibraryTokens { get; set; }

    bool CrossFade { get; set; }

    bool HideArtistsWithoutAlbum { get; set; }

    bool RefreshLibraryAtStartup { get; set; }

    bool ImportTrackWithArtistGenre { get; set; }

    bool NovaApiEnabled { get; set; }

    bool TelemetryEnabled { get; set; }

    string CachePath { get; set; }

    List<string> LibraryPath { get; set; }

    string ArtistsGroupBy { get; set; }

    List<string> ArtistsFilterBy { get; set; }

    List<long> ArtistsFilterByGenresId { get; set; }

    string AlbumsGroupBy { get; set; }

    List<string> AlbumsFilterBy { get; set; }

    List<long> AlbumsFilterByGenresId { get; set; }

    string TracksGroupBy { get; set; }

    List<string> TracksFilterBy { get; set; }

    List<long> TracksFilterByGenresId { get; set; }

    void SetPath(string path);

    void CopyFrom(IAppOptions options);

    void InitializeOptions(string applicationPath);
}