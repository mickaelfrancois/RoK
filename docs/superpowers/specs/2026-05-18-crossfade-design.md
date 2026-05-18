# Crossfade timing fix — Design spec

**Date**: 2026-05-18  
**Branch**: feat/crossfade-improvements  
**Status**: Approved

## Problem

The crossfade between tracks has inconsistent timing: sometimes smooth, sometimes the old track ends before the new one has fully faded in, creating an audible gap or abrupt cut. The issue is non-deterministic and affects all track lengths.

### Root cause

Two sources of drift accumulate:

1. **Crossfade loop drift (primary)** — `CrossfadeToAsync` uses a step-based loop of `steps × Task.Delay(50ms)`. On Windows, `Task.Delay` resolution is ~15ms per call. For a 5s crossfade (100 iterations), the loop can run 5.0–5.7s instead of 5.0s. The old track finishes while the fade is still in progress.

2. **Position snapshot staleness (secondary)** — `currentPosition` is read in `CrossfadeToNextTrackAsync` and passed to `RunCrossfadeAsync`. The value is slightly stale by the time the wait is computed. Drift of the single `Task.Delay(timeToWaitBeforeCrossfade)` is ≤15ms — acceptable by itself, but compounds with source 1.

## Solution

Two minimal, localized changes. No interface changes, no structural refactor.

### Fix 1 — Stopwatch-based progress in `CrossfadeToAsync` (primary)

**File**: `src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs`

Replace the step-counting `for` loop with a `while` loop driven by `Stopwatch.Elapsed`. `Task.Delay(50ms)` remains as sleep granularity, but fade progress is computed from actual elapsed time — immune to per-iteration drift.

```csharp
// Before
int steps = Math.Max(1, (int)(durationSeconds * 1000 / intervalMs));
for (int i = 0; i <= steps; i++)
{
    double progress = Math.Clamp((double)i / steps, 0.0, 1.0);
    ...
    await Task.Delay(intervalMs, ct);
}

// After
var sw = System.Diagnostics.Stopwatch.StartNew();
while (true)
{
    ct.ThrowIfCancellationRequested();
    double progress = Math.Clamp(sw.Elapsed.TotalSeconds / durationSeconds, 0.0, 1.0);
    double fadeOutVolume = Math.Max(0, DbInterpolate(progress, masterVolume));
    SetVolume(fadeOutVolume);
    double fadeInVolume = Math.Max(0, DbInterpolate(1.0 - progress, masterVolume));
    nextReader.Volume = (float)Math.Clamp(fadeInVolume / 100.0, 0.0, 1.0);
    if (progress >= 1.0) break;
    await Task.Delay(intervalMs, ct);
}
```

**Guard** — add at entry of `CrossfadeToAsync`:
```csharp
if (durationSeconds <= 0)
    return; // already handled upstream by RunCrossfadeAsync, but defense-in-depth
```

### Fix 2 — Fresh position read in `RunCrossfadeAsync` (secondary)

**File**: `src/Rok.Application/Player/PlayerService.cs`

Read `_player.Position` fresh just before computing `timeToWaitBeforeCrossfade`, instead of using the stale `currentPosition` parameter captured earlier in the call chain.

```csharp
// Before
double timeToWaitBeforeCrossfade = Math.Max(0, trackLength - currentPosition - crossfadeDurationSeconds);

// After
double freshPosition = _player.Position;
double timeToWaitBeforeCrossfade = Math.Max(0, trackLength - freshPosition - crossfadeDurationSeconds);
```

The `currentPosition` parameter is still used for `remainingTime` / `crossfadeDurationSeconds` computation (which happens before this point and is correct).

## Tests

### Existing tests (unchanged)

`PlayerServiceCrossfadeTests` mocks `CrossfadeToAsync` and does not test the internal loop. All existing tests remain valid.

### New tests in `Rok.Infrastructure.UnitTests`

Two new tests on `NAudioMediaPlayer` using real in-memory audio (e.g., NAudio `SilenceProvider` → `WaveFileWriter` → temp file, or a short real fixture file):

**`CrossfadeToAsync_ShouldCompleteWithinExpectedDuration`**
- Arrange: two short real audio files (or silence), engine loaded with track 1
- Act: measure wall-clock time of `CrossfadeToAsync(duration: 1.0)` with a `Stopwatch`
- Assert: elapsed ∈ `[1.0s, 1.3s]` (tight upper bound, ~30% margin for CI load)

**`CrossfadeToAsync_ShouldSetFullVolumeOnNextTrackAtEnd`**
- Arrange: same setup, `masterVolume = 80`
- Act: run `CrossfadeToAsync(duration: 0.1)` to completion
- Assert: after completion, `nextReader.Volume ≈ 0.8` (fade-in reached full)  
  (verify via the swapped `_audioFileReader` — the engine exposes nothing directly, so check via `SetVolume` call or a thin seam)

> Note: `NAudioMediaPlayer` currently has no seam for reading back volume. If testing via behavior (Stopwatch timing) proves sufficient, the second test may be dropped in favor of an integration test at `PlayerService` level.

## Out of scope

- Equalizer preset application during fade-in (new track plays with default EQ bands during crossfade; the preset is applied after `OnMediaChangedAsync`). This is a separate concern and not blocking.
- `WinUIMediaPlayer.CrossfadeToAsync` — already falls back to instant switch by design. No change.
- Configurable crossfade duration (currently hardcoded to 5s). No change.
