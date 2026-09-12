using System.Net.Http.Json;
using Rok.WebApi.Contracts;

namespace Rok.Companion.Services;

/// <summary>
/// Talks to the Rok web API. Every call is relative, so the companion works unchanged whether it is served
/// by Rok itself or by the development server pointed at a running instance.
/// </summary>
/// <remarks>
/// Reads go through the compile-time metadata of <see cref="RokJsonContext"/>. The reflection-based overloads
/// compile fine but break once published: this app is trimmed, and the contract members would be stripped.
/// </remarks>
public sealed class RokApiClient(HttpClient http)
{
    /// <summary>Relative URL of the cover of the track being played, cache-busted by track identifier.</summary>
    public static string AlbumCoverUrl(long trackId) => $"current/album-cover?track={trackId}";

    public Task<PlayerStatus?> GetStatusAsync(CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync("api/player/status", RokJsonContext.Default.PlayerStatus, cancellationToken);

    public Task<List<QueueEntry>?> GetQueueAsync(CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync("api/player/queue", RokJsonContext.Default.ListQueueEntry, cancellationToken);

    public Task<List<PlaylistSummary>?> GetPlaylistsAsync(CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync("api/playlists", RokJsonContext.Default.ListPlaylistSummary, cancellationToken);

    public Task<List<LibraryTrack>?> GetPlaylistTracksAsync(long playlistId, CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync($"api/playlists/{playlistId}/tracks", RokJsonContext.Default.ListLibraryTrack, cancellationToken);

    public Task<bool> TogglePlaybackAsync() => PostAsync("api/player/toggle");

    public Task<bool> NextAsync() => PostAsync("api/player/next");

    public Task<bool> PreviousAsync() => PostAsync("api/player/previous");

    public Task<bool> ToggleMuteAsync() => PostAsync("api/player/mute");

    public Task<bool> ShuffleAsync() => PostAsync("api/player/shuffle");

    public Task<bool> ToggleLoopAsync() => PostAsync("api/player/loop");

    public Task<bool> SetVolumeAsync(double volume) => PostAsync($"api/player/volume/{Number(volume)}");

    public Task<bool> SeekAsync(double seconds) => PostAsync($"api/player/seek/{Number(seconds)}");

    public Task<bool> PlayQueuedTrackAsync(long trackId) => PostAsync($"api/player/queue/{trackId}/play");

    public Task<bool> PlayPlaylistAsync(long playlistId) => PostAsync($"api/playlists/{playlistId}/play");

    public Task<bool> RateAsync(long trackId, int score) => PostAsync($"api/tracks/{trackId}/score/{score}");


    /// <summary>Draws a random album, plays it, and reports what came up.</summary>
    public Task<SurprisePick?> SurpriseAlbumAsync() => DrawAsync("api/surprise/album");

    /// <summary>Draws a random artist, plays their catalogue, and reports what came up.</summary>
    public Task<SurprisePick?> SurpriseArtistAsync() => DrawAsync("api/surprise/artist");


    private async Task<SurprisePick?> DrawAsync(string route)
    {
        try
        {
            using StringContent body = new(string.Empty);
            using HttpResponseMessage response = await http.PostAsync(route, body);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync(RokJsonContext.Default.SurprisePick);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }


    private static string Number(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);


    private async Task<bool> PostAsync(string route)
    {
        try
        {
            // A POST carrying no declared length is turned away with 411 before it ever reaches Rok,
            // so an explicit empty body is sent rather than no content at all.
            using StringContent body = new(string.Empty);
            using HttpResponseMessage response = await http.PostAsync(route, body);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}