using Rok.Shared.Enums;

namespace Rok.Application.Interfaces;

public interface IAppOptions
{
    Guid Id { get; set; }

    AppTheme Theme { get; set; }

    int AlbumRecentThresholdDays { get; set; }

    int ArtistRecentThresholdDays { get; set; }

    List<string> LibraryTokens { get; set; }

    bool CrossFade { get; set; }

    bool IsGridView { get; set; }

    bool HideArtistsWithoutAlbum { get; set; }

    bool RefreshLibraryAtStartup { get; set; }

    bool ImportTrackWithArtistGenre { get; set; }

    bool NovaApiEnabled { get; set; }

    bool TelemetryEnabled { get; set; }

    bool DiscordRichPresenceEnabled { get; set; }

    string CachePath { get; set; }

    string ArtistsGroupBy { get; set; }

    List<string> ArtistsFilterBy { get; set; }

    List<string> ArtistsFilterByTags { get; set; }

    List<long> ArtistsFilterByGenresId { get; set; }

    string AlbumsGroupBy { get; set; }

    List<string> AlbumsFilterBy { get; set; }

    List<long> AlbumsFilterByGenresId { get; set; }

    List<string> AlbumsFilterByTags { get; set; }

    string TracksGroupBy { get; set; }

    List<string> TracksFilterBy { get; set; }

    List<string> TracksFilterByTags { get; set; }

    List<long> TracksFilterByGenresId { get; set; }

    void SetCachePath(string path);

    void CopyFrom(IAppOptions options);

    void InitializeOptions(string applicationPath);
}