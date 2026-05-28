# Webradio — Phase 1 (URL manuelle, MVP) — Design spec

**Date**: 2026-05-28
**Branch**: feat/webradio-phase-1
**Status**: Draft (awaiting review)

## Goal

Add the ability to listen to a webradio stream in Rok. Phase 1 covers manual URL entry, ICY metadata capture, and a mutually-exclusive "Radio" playback mode. Catalogue (Radio-Browser), HLS, OGG, listening history, scrobbling, and crossfade are explicitly out of scope.

## Scope

### In scope

- Engine support for HTTP audio streams (Shoutcast/Icecast, MP3 and AAC).
- ICY metadata parsing (`StreamTitle`) and propagation.
- A new `EPlaybackMode { None, Music, Radio }` in `PlayerService` with transparent switching between modes.
- Persistence of favorite stations in a new SQLite table (`Migration11` + `RadioStationEntity` + Dapper repository).
- A new "Radios" feature folder in `Rok.Application/Features/Radios/` with CQRS handlers.
- Presentation: a new `RadiosPage` (favorites list + "Play a URL" dialog), and adaptation of `PlayerView` to the radio mode.
- SMTC and Discord rich presence wired to the live `StreamTitle`.
- `.pls`, `.m3u`, `.m3u8` parsing when the entered URL points to a playlist file.

### Out of scope (Phase 1)

- HLS stream playback (an `.m3u8` *playlist file* pointing to a direct stream is fine; an `.m3u8` *segment manifest* is not).
- OGG/Vorbis decoding.
- Radio-Browser catalogue (deferred to Phase 3).
- Listening history, scoring, listen tracking, Last.fm scrobbling.
- Crossfade between stations.
- Per-station equalizer presets (the global equalizer still applies, but no preset bound to a station).
- Favicons / station artwork (deferred to Phase 3).
- Sophisticated reconnection (Phase 1 retries twice then surfaces an error).

## Architecture overview

Option A as agreed: **a single playback orchestrator** (`PlayerService`) operating in two mutually-exclusive modes. The audio output device, volume, mute, equalizer, sleep timer, call detection, SMTC, and Discord stay shared. Music-specific concerns (playlist, scoring, listen tracking, crossfade, navigation links, prev/next, seek, lyrics) are skipped when `Mode == Radio`.

### Layers touched

| Layer | Changes |
|---|---|
| Domain | New `RadioStationEntity`. |
| Shared | None. |
| Application | New `RadioStationDto`, `IRadioStationRepository`, `IPlayerEngine.SetStream`, `EPlaybackMode`, new `PlayerService` methods + invariants, new `Features/Radios/` (CQRS), new messages `RadioStationChanged` and `RadioMetadataChanged`. |
| Infrastructure | `Migration11`, `RadioStationRepository` (Dapper), `IcyStreamHandler` (HTTP + metadata parsing), `NAudioMediaPlayer.SetStream`, playlist file parser. |
| Import | None. |
| Presentation | New `RadiosPage` + ViewModel, new "Play a URL" dialog, adaptation of `PlayerView` (two visual templates driven by `Mode`), adaptation of SMTC and Discord services, navigation entry in the main shell. |

## Domain & persistence

### `RadioStationEntity`

```csharp
[Table("RadioStations")]
public class RadioStationEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? HomepageUrl { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? LastListen { get; set; }
}
```

No `Country`, `Tags`, `Codec`, `Bitrate`, `FaviconUrl`, `RadioBrowserUuid` in Phase 1. These belong to Phase 3 (catalogue). `LastListen` is kept (cheap, useful for ordering favorites); `ListenCount` is intentionally omitted — no scoring.

### `Migration11`

Creates `RadioStations` table:

```sql
CREATE TABLE RadioStations (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Name          TEXT    NOT NULL,
    StreamUrl     TEXT    NOT NULL,
    HomepageUrl   TEXT    NULL,
    AddedAt       TEXT    NOT NULL,
    LastListen    TEXT    NULL
);

CREATE UNIQUE INDEX UX_RadioStations_StreamUrl ON RadioStations(StreamUrl);
CREATE INDEX IX_RadioStations_LastListen ON RadioStations(LastListen DESC);
```

Registered in `MigrationService` after `Migration10`.

### `IRadioStationRepository`

Matches the existing repository style (Dapper, async, `CancellationToken`):

```csharp
public interface IRadioStationRepository
{
    Task<long> AddAsync(RadioStationEntity station, CancellationToken ct);
    Task<RadioStationEntity?> GetByIdAsync(long id, CancellationToken ct);
    Task<RadioStationEntity?> GetByUrlAsync(string streamUrl, CancellationToken ct);
    Task<IReadOnlyList<RadioStationEntity>> ListAsync(CancellationToken ct);
    Task UpdateAsync(RadioStationEntity station, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
    Task TouchLastListenAsync(long id, DateTime utcNow, CancellationToken ct);
}
```

A duplicate `StreamUrl` insert returns `ConflictError` at handler level (mapped from `SqliteException.SqliteErrorCode == 19`).

## Application layer

### `RadioStationDto`

```csharp
public record RadioStationDto(
    long Id,
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    DateTime AddedAt,
    DateTime? LastListen);
```

### `EPlaybackMode`

```csharp
namespace Rok.Application.Player;

public enum EPlaybackMode
{
    None,
    Music,
    Radio
}
```

### `IPlayerService` evolution

Additions:

```csharp
EPlaybackMode Mode { get; }
RadioStationDto? CurrentStation { get; }
string? CurrentStreamTitle { get; }
bool IsBuffering { get; }

void PlayRadioStation(RadioStationDto station);
```

Behavior:

- `PlayRadioStation` stops any current music playback, clears `Playlist`, sets `CurrentTrack = null`, sets `Mode = Radio`, calls `_player.SetStream(station)`, emits `RadioStationChanged`.
- `LoadPlaylist` / `Start` / `Play` (with a starting track) implicitly stop any active radio and switch `Mode` back to `Music`.
- `Stop` resets `Mode` to `None`.

Invariants when `Mode == Radio`:

| Property | Value |
|---|---|
| `Playlist` | empty list |
| `CurrentTrack` | `null` |
| `Position` | `0` |
| `Length` (via engine) | `0` |
| `CanNext` | `false` |
| `CanPrevious` | `false` |
| `CanSeek` | `false` |
| `IsLoopingEnabled` | unchanged but no effect |

The `PlayerService.Position` setter early-returns when `Mode == Radio`. Same for `Next` / `Previous` / `Skip` (no-ops).

### `IPlayerEngine` evolution

Add one method, one event, two read-only properties:

```csharp
bool SetStream(RadioStationDto station);
event EventHandler<string>? OnMetadataChanged;
bool IsLive { get; }
bool IsBuffering { get; }
```

Documented invariants on the new code path:

- `IsLive` is `true` when the active source is a stream.
- `IsBuffering` is `true` while the buffer is being filled at startup or after an underflow; see [Buffering strategy](#buffering-strategy).
- `Length` is `0` while `IsLive` (and Music-mode UI bindings ignore Length when `Mode == Radio`).
- `SetPosition` is a no-op when `IsLive`.
- `CrossfadeToAsync` returns immediately when `IsLive`.
- `OnMediaAboutToEnd` is not raised in stream mode.
- `OnMediaEnded` is raised only on terminal stream error (after retries exhausted).

### New CQRS feature `Rok.Application/Features/Radios/`

```
Features/Radios/
  Requests/
    AddRadioStationRequest.cs                  (Name, StreamUrl, HomepageUrl?)
    AddRadioStationRequestHandler.cs
    AddRadioStationRequestValidator.cs
    DeleteRadioStationRequest.cs
    DeleteRadioStationRequestHandler.cs
    GetRadioStationsRequest.cs
    GetRadioStationsRequestHandler.cs
    PlayRadioStationByIdRequest.cs             (favorite station: resolves DTO, calls IPlayerService, touches LastListen)
    PlayRadioStationByIdRequestHandler.cs
    PlayRadioUrlRequest.cs                     (ad-hoc URL: resolves playlist file, plays without persisting)
    PlayRadioUrlRequestHandler.cs
    PlayRadioUrlRequestValidator.cs
  Services/
    IRadioStreamUrlResolver.cs                 (resolves .pls/.m3u/.m3u8 → first stream URL)
    RadioStreamUrlResolver.cs
```

Validators (Phase 1):

- `AddRadioStationRequest`: `Name` non-empty (≤ 200 chars), `StreamUrl` non-empty + absolute http(s) URI.
- `PlayRadioUrlRequest`: `Url` non-empty + absolute http(s) URI.
- `PlayRadioStationByIdRequest`: `StationId` resolved in handler via repository; `NotFoundError` if absent.

Errors emitted:

| Case | Error |
|---|---|
| Add with same `StreamUrl` already present | `ConflictError("radio.duplicate")` |
| Play by id with unknown `StationId` | `NotFoundError("radio.not_found")` |
| Playlist resolution returned no stream URL | `OperationError("radio.no_stream_in_playlist")` |
| Resolved URL points to an HLS segment manifest | `OperationError("radio.hls_unsupported")` |

### New messages (`Rok.Application.Messages`)

```csharp
public sealed record RadioStationChanged(RadioStationDto Station);
public sealed record RadioMetadataChanged(string StreamTitle);
```

Both broadcast via `IMessenger`. Presentation subscribes for UI updates; `DiscordRichPresenceService` and `SystemMediaTransportControlsService` subscribe to refresh remote presence and SMTC display.

## Infrastructure

### Streaming engine (`NAudioMediaPlayer.SetStream`)

1. `IcyStreamHandler` opens the HTTP connection with `Icy-MetaData: 1` request header.
2. It reads `icy-metaint` from the response headers. If absent, the stream plays as-is with no metadata.
3. A background task reads from the underlying `Stream`: every `icy-metaint` bytes of audio are forwarded to a `BufferedWaveProvider`, the inline metadata block is parsed for `StreamTitle='...'`, and `OnMetadataChanged` is raised when the value changes.
4. Audio bytes feed an `Mp3FileReader` or `StreamMediaFoundationReader` (content-type sniffing: `audio/mpeg` → MP3, `audio/aac` / `audio/aacp` → AAC via Media Foundation).
5. The reader is wired through the existing `Equalizer` and then to `WaveOutEvent` — identical to the file path from `Init` onwards.
6. The `BufferedWaveProvider` absorbs network jitter. Sizing, pre-buffering, and underflow handling are described in [Buffering strategy](#buffering-strategy) below.

Reconnection policy (MVP): on `IOException`, read returning 0 bytes, or HTTP non-success after the read started, retry **twice** with 1 s and 2 s back-off. On third failure, raise `OnMediaEnded` and let `PlayerService` flip `PlaybackState` to `Ended` with `Mode = Radio` (UI shows "Stream stopped").

### Buffering strategy

Goal: avoid micro-cuts on slow / jittery networks, and avoid UI flicker on transient micro-underflows.

#### Sizing

`BufferedWaveProvider.BufferDuration` is set in **seconds** (not bytes), so the same code works for any bitrate (320 kbps MP3 ≈ 600 KB, 64 kbps AAC ≈ 120 KB for the same 15 s):

| Parameter | Value | Rationale |
|---|---|---|
| `BufferDuration` (total capacity) | **15 s** | Comfortable headroom over the pre-buffer target, room to absorb a ~10 s network hiccup. |
| Pre-buffer before first `Play()` | **3 s** | Avoids an immediate "Buffering" state on stream startup. `WaveOutEvent.Play()` is deferred until `BufferedBytes / AverageBytesPerSecond ≥ 3.0`. |
| Resume threshold after underflow | **2 s rebuffered** | Higher than the trigger threshold so we don't oscillate between Playing and Buffering. |
| UI "Buffering" trigger | underflow > **500 ms** | Lower than the resume threshold; ignores micro-glitches (< 500 ms) to prevent flicker. |
| "Error" terminal threshold | no new bytes from network for **5 s** while playback is stalled | Distinct from underflow: this means the *fetch* is dead, not just slow. Triggers the retry policy described above. |

The "average bytes per second" is read from the wave format produced by `Mp3FileReader` / `StreamMediaFoundationReader` once the first decode is complete; pre-buffer measurement waits until that is available.

#### States and transitions

The engine tracks two orthogonal axes:

- `PlaybackState`: `Playing` / `Paused` / `Stopped` / `Ended` (unchanged).
- `IsBuffering`: `true` / `false` (new in Phase 1, surfaced via `BufferingChanged` message).

Transitions during a stream session:

| From | Trigger | To |
|---|---|---|
| (stream starts) | fetch initiated, < 3 s buffered | `PlaybackState = Playing`, `IsBuffering = true` |
| `IsBuffering = true` (startup) | buffered ≥ 3 s | `WaveOutEvent.Play()`, `IsBuffering = false` |
| `IsBuffering = false` | underflow > 500 ms while playing | `IsBuffering = true` (audio stays paused naturally as the provider drains) |
| `IsBuffering = true` (mid-stream) | buffered ≥ 2 s again | `IsBuffering = false` |
| any | network silent > 5 s | retry policy; eventually `PlaybackState = Ended` |

Pause initiated by the user (Pause / call detection / SMTC) does **not** flip `IsBuffering`. The buffer keeps filling in the background up to `BufferDuration`; on resume, playback is instant if the buffer is non-empty.

#### Implementation notes

- A single `Timer` (250 ms tick — same cadence as `_positionTimer`) checks buffered seconds and applies the transitions above. No new threads beyond the existing fetch task.
- The "5 s without new bytes" terminal threshold is tracked by a `Stopwatch` reset on each successful read; if `Elapsed > TimeSpan.FromSeconds(5)` and the buffer is empty, the retry path is invoked.
- `IsBuffering` is exposed on `IPlayerEngine` (read-only) and surfaced on `IPlayerService` (read-only). The `BufferingChanged(bool isBuffering)` message is broadcast via `IMessenger` for UI subscribers.

### Playlist file parser (`RadioStreamUrlResolver`)

Detects format by:

1. Path/URL suffix (`.pls`, `.m3u`, `.m3u8`).
2. Or `Content-Type` (`audio/x-mpegurl`, `audio/x-scpls`, `application/vnd.apple.mpegurl`).

Behavior:

- Fetches the file (max 1 MB cap).
- `.pls` → first `FileN=` entry.
- `.m3u` / `.m3u8` (basic playlist) → first non-comment line that is a `http(s)://` URL.
- HLS segment manifest (presence of `#EXT-X-VERSION`, `#EXT-X-TARGETDURATION`, `#EXTINF:`, or `#EXT-X-STREAM-INF`) → returns `OperationError("radio.hls_unsupported")`. This is the explicit guard for the HLS exclusion.
- Otherwise → `OperationError("radio.no_stream_in_playlist")`.

If the original URL is already a direct stream (`Content-Type: audio/*` and no playlist-format markers), the resolver returns the URL unchanged.

### `RadioStationRepository`

Standard Dapper repository in `Rok.Infrastructure/Repositories/`, following the existing repository conventions (constructor receives the `AppDbContext`-style connection factory, async methods, `CancellationToken` on every call).

## Presentation

### New page: `RadiosPage.xaml`

Layout (Phase 1, minimal):

- Top bar: button **"Play a URL"** (opens `PlayRadioUrlDialog`), button **"Add to favorites"** (opens `AddRadioStationDialog`).
- Main area: list of favorite stations (Name, `LastListen` timestamp), each row with **Play** and **Delete** affordances.
- Empty state: "No favorite stations yet. Click 'Play a URL' to listen, or 'Add to favorites' to save one."

ViewModel `RadiosViewModel` registered in `Presentation/DependencyInjection.cs`. Loads favorites via `GetRadioStationsRequest`. Dispatches `PlayRadioStationByIdRequest` on play, `AddRadioStationRequest` / `DeleteRadioStationRequest` on add/delete.

### New dialogs

- **`PlayRadioUrlDialog.xaml`** — single URL field + Play/Cancel. On submit, dispatches `PlayRadioUrlRequest`. Errors (`OperationError`, `ValidationError`) shown inline.
- **`AddRadioStationDialog.xaml`** — Name field + URL field + optional Homepage field + Save/Cancel. Dispatches `AddRadioStationRequest`. Duplicate URL surfaces as inline error on the URL field.

### `PlayerView.xaml` adaptation

Two visual templates, switched via Visibility bindings on `Mode`:

- **Music template (existing)**: cover, artist link, album link, track title, progress bar, prev/next/seek, score, lyrics button.
- **Radio template (new)**: station name (large), live `StreamTitle` (medium, updated via `RadioMetadataChanged`), "LIVE" badge, **Stop** button only. No progress bar, no prev/next, no album/artist links, no score, no lyrics.

The right-side "Now playing" mini-display in the main shell follows the same pattern. The Volume / Mute / Equalizer controls remain visible and functional in both modes.

### Navigation

Add a "Radios" entry to the main navigation pane (alongside Albums, Artists, Tracks, Playlists, etc.). Routes to `RadiosPage`.

### SMTC adaptation (`SystemMediaTransportControlsService`)

When `Mode == Radio`:

- `Title` = parsed Track part of `StreamTitle` (split on first " - " — if no split, the whole string is the title).
- `Artist` = parsed Artist part of `StreamTitle` (or station name if no split).
- `AlbumArtist` = station name.
- `Next` / `Previous` buttons: disabled.
- Timeline: not published.

When `RadioMetadataChanged` fires, the display refreshes.

### Discord adaptation (`DiscordRichPresenceService`)

When `Mode == Radio`:

- `Details` = `"Listening to {StationName}"`.
- `State` = `StreamTitle` (truncated to Discord's 128-char limit).
- Large image: a static "radio" asset key — no per-station artwork in Phase 1.

## Tests

### `Rok.ApplicationTests`

- `AddRadioStationRequestHandlerTests` — success, duplicate URL → `ConflictError`, validation failures.
- `GetRadioStationsRequestHandlerTests` — empty list, ordered by `LastListen DESC` then `AddedAt DESC`.
- `DeleteRadioStationRequestHandlerTests` — success, unknown id → `NotFoundError`.
- `PlayRadioStationByIdRequestHandlerTests` — calls `IPlayerService.PlayRadioStation`, touches `LastListen`. Unknown id → `NotFoundError`.
- `PlayRadioUrlRequestHandlerTests` — resolves playlist file via stubbed `IRadioStreamUrlResolver`, calls `IPlayerService.PlayRadioStation` with an ad-hoc `RadioStationDto` (Id=0). HLS playlist → `OperationError("radio.hls_unsupported")`.
- `PlayerServiceRadioModeTests` — `PlayRadioStation` clears playlist, sets `Mode = Radio`, blocks `Next`/`Previous`/`Skip`/`SetPosition`, transparent bascule when `Start(track)` is called while in Radio mode.
- `RadioStreamUrlResolverTests` (unit, with a fake `HttpMessageHandler`) — `.pls` happy path, `.m3u` happy path, HLS manifest detection, no-stream → error, direct URL passthrough.

### `Rok.Infrastructure.UnitTests`

- `RadioStationRepositoryTests` — Add/Get/List/Update/Delete/TouchLastListen against the SQLite fixture (which loads all migrations including `Migration11`).
- `Migration11Tests` — schema check (table exists, indices present).

### `Rok.PresentationTests`

- `RadiosViewModelTests` — load favorites, play, add, delete commands.
- `PlayerViewModelRadioModeTests` — `Mode` switching updates UI-bound properties; music-only controls are hidden.

### Manual / integration

- Enter a known public Shoutcast MP3 URL → audio plays, `StreamTitle` appears, switching to a local album stops the stream and starts the album.
- Enter a `.pls` URL (e.g., from icecast.fr) → resolved, plays.
- Enter an HLS manifest URL → error message "HLS not supported".

No automated network tests in CI (external streams are flaky and out of our control).

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| AAC+/HE-AAC v2 support via Media Foundation is platform-dependent. | Run a POC against a 64 kbps AAC stream early in the implementation. Document codec support in the dialog ("MP3 and AAC streams supported"). |
| ARM64 audio path differs from x64. | Smoke-test on ARM64 build before merging. |
| ICY parsing edge cases (truncated UTF-8, escaped quotes, multi-byte titles). | Tolerant parser: skip malformed blocks, never throw, log at debug level. |
| `Length` getter currently coerces `<= 0` to `1` (NAudioMediaPlayer.cs line 41) — leaks to UI progress binding if Music-mode UI doesn't check `Mode` first. | Bind UI progress to `Mode == Music` plus `Length`; do not change the getter (see open question 1). |
| Buffer underflow confused with permanent failure. | Distinct thresholds in [Buffering strategy](#buffering-strategy): underflow > 500 ms = `IsBuffering = true`; no network bytes for > 5 s = retry → eventually `Ended`. |
| Micro-cuts on slow / jittery networks at startup. | 3 s pre-buffer before `WaveOutEvent.Play()`; 2 s resume threshold after mid-stream underflow (higher than the 500 ms trigger to avoid oscillation). |

## Open questions

1. **`Length` getter coercion**: the current `Length` getter returns `1` when the backing field is `<= 0`. In Radio mode we want effective `0`. Two options: (a) change the getter to return `0` and audit existing UI bindings, or (b) keep the coercion and rely on the new `IsLive` flag for UI gating. **Recommendation: (b)** — safer for existing bindings.

2. **Persistence path for "Play a URL"**: should the play dialog persist the URL? **Recommendation: no.** "Play a URL" is ad-hoc and ephemeral; saving is an explicit second action via "Add to favorites". The `PlayRadioUrlRequest` handler builds an in-memory `RadioStationDto(Id: 0, Name: "Ad-hoc stream", StreamUrl: <resolved>, …)` and never touches the repository. This matches the "no listening history" rule.

(Previously open: the "Buffering" state representation. **Resolved**: separate `IsBuffering` property + `BufferingChanged` message — see [Buffering strategy](#buffering-strategy).)

These two remaining questions are deliberately left open in this spec; they should be resolved in the implementation plan or during early implementation.

## Acceptance criteria

Phase 1 ships when:

- A user can paste a Shoutcast/Icecast MP3 or AAC URL into a dialog and hear audio within 3 s on a typical connection.
- A user can paste a `.pls` or `.m3u` URL and the first stream URL is auto-resolved and played.
- The currently broadcast track (ICY `StreamTitle`) is displayed in the player view and updates within 2 s of the metadata block arrival.
- The user can favorite a station, see it in the list, replay it, and delete it.
- Starting an album while a radio is playing stops the radio (and vice versa) without UI inconsistency.
- The Windows SMTC mini-controller shows the live `StreamTitle`; Discord rich presence shows station + title.
- The progress bar, prev/next, seek, score, and album/artist links are hidden when a radio is playing.
- All new unit tests pass; `dotnet build /p:Platform=x64` and `dotnet test /p:Platform=x64` are green.
- Manual smoke tests for HLS rejection and basic playback succeed.
