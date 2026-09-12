using System.Net.Http.Json;
using Rok.WebApi.Contracts;

namespace Rok.Companion.Services;

/// <summary>
/// Talks to the Rok web API. Every call is relative, so the companion works unchanged whether it is served
/// by Rok itself or by the development server pointed at a running instance.
/// </summary>
public sealed class RokApiClient(HttpClient http)
{
    /// <summary>Relative URL of the cover of the track being played, cache-busted by track identifier.</summary>
    public static string AlbumCoverUrl(long trackId) => $"current/album-cover?track={trackId}";

    public Task<PlayerStatus?> GetStatusAsync(CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync<PlayerStatus>("api/player/status", RokJson.Options, cancellationToken);

    public Task<List<QueueEntry>?> GetQueueAsync(CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync<List<QueueEntry>>("api/player/queue", RokJson.Options, cancellationToken);

    public Task<List<PlaylistSummary>?> GetPlaylistsAsync(CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync<List<PlaylistSummary>>("api/playlists", RokJson.Options, cancellationToken);

    public Task<List<LibraryTrack>?> GetPlaylistTracksAsync(long playlistId, CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync<List<LibraryTrack>>($"api/playlists/{playlistId}/tracks", RokJson.Options, cancellationToken);

    public Task<bool> TogglePlaybackAsync() => PostAsync("api/player/toggle");

    public Task<bool> NextAsync() => PostAsync("api/player/next");

    public Task<bool> PreviousAsync() => PostAsync("api/player/previous");

    public Task<bool> ToggleMuteAsync() => PostAsync("api/player/mute");

    public Task<bool> ShuffleAsync() => PostAsync("api/player/shuffle");

    public Task<bool> ToggleLoopAsync() => PostAsync("api/player/loop");

    public Task<bool> SetVolumeAsync(double volume) =>
        PostAsync($"api/player/volume/{volume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");

    public Task<bool> SeekAsync(double seconds) =>
        PostAsync($"api/player/seek/{seconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");

    public Task<bool> PlayQueuedTrackAsync(long trackId) => PostAsync($"api/player/queue/{trackId}/play");

    public Task<bool> PlayPlaylistAsync(long playlistId) => PostAsync($"api/playlists/{playlistId}/play");

    public Task<bool> RateAsync(long trackId, int score) => PostAsync($"api/tracks/{trackId}/score/{score}");


    private async Task<bool> PostAsync(string route)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsync(route, content: null);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}