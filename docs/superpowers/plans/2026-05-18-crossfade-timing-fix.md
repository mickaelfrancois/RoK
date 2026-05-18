# Crossfade Timing Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix non-deterministic crossfade timing by replacing step-based fade progress with Stopwatch-based progress, and reading a fresh engine position before computing the pre-crossfade wait.

**Architecture:** Two localized changes — `NAudioMediaPlayer.CrossfadeToAsync` gets a Stopwatch loop (primary fix: eliminates fade-loop drift), and `PlayerService.RunCrossfadeAsync` reads `_player.Position` fresh (secondary fix: eliminates stale-snapshot error). No interface changes, no structural refactor. Existing tests remain valid.

**Tech Stack:** .NET 10, C# 13, NAudio, xUnit, `dotnet test /p:Platform=x64`

---

## File Map

| File | Change |
|---|---|
| `src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs` | Replace step-based loop with Stopwatch loop + guard on `durationSeconds <= 0` |
| `src/Rok.Application/Player/PlayerService.cs` | Read `_player.Position` fresh in `RunCrossfadeAsync` |

No new files. No new tests (NAudioMediaPlayer requires real audio hardware; existing `PlayerServiceCrossfadeTests` already covers the application-level crossfade flow and remains valid).

---

## Task 1 — Fix the crossfade loop in `NAudioMediaPlayer.CrossfadeToAsync`

**Files:**
- Modify: `src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs:227-321`

The current loop computes `progress = (double)i / steps` which ties fade progress to iteration count. With Windows `Task.Delay` drift (~15ms/call), 100 iterations at 50ms can take 5.0–5.7s instead of 5.0s, causing the old track to end before the fade completes.

- [ ] **Step 1: Add the `durationSeconds <= 0` guard**

Open `src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs`. Locate `CrossfadeToAsync` (line ~227). Add a guard as the very first statement of the method body, before anything else:

```csharp
public async Task CrossfadeToAsync(TrackDto nextTrack, double durationSeconds, double masterVolume, CancellationToken ct)
{
    if (durationSeconds <= 0)
        return;

    AudioFileReader nextReader;
    try
    {
        nextReader = new AudioFileReader(nextTrack.MusicFile);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Crossfade: failed to open next track {File}", nextTrack.MusicFile);
        return;
    }
    // ... rest of the method unchanged ...
```

- [ ] **Step 2: Replace the `for` loop with a Stopwatch-based `while` loop**

Locate the block starting at `const int intervalMs = 50;` (around line 265). Replace the entire block (from `const int intervalMs` to the closing brace of the `for` loop, inclusive) with:

```csharp
const int intervalMs = 50;
var sw = System.Diagnostics.Stopwatch.StartNew();

while (true)
{
    ct.ThrowIfCancellationRequested();

    double progress = Math.Clamp(sw.Elapsed.TotalSeconds / durationSeconds, 0.0, 1.0);

    double fadeOutVolume = Math.Max(0, DbInterpolate(progress, masterVolume));
    SetVolume(fadeOutVolume);

    double fadeInVolume = Math.Max(0, DbInterpolate(1.0 - progress, masterVolume));
    nextReader.Volume = (float)Math.Clamp(fadeInVolume / 100.0, 0.0, 1.0);

    if (progress >= 1.0)
        break;

    await Task.Delay(intervalMs, ct);
}
```

The Stopwatch measures wall-clock time regardless of per-iteration `Task.Delay` drift. When `progress` reaches 1.0 the loop exits — the fade always lasts exactly `durationSeconds` of real time.

- [ ] **Step 3: Build to verify no compilation errors**

```bash
dotnet build /p:Platform=x64 -v quiet
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Run existing crossfade tests**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "Crossfade" -v normal
```

Expected: all existing crossfade tests pass (they mock `CrossfadeToAsync` entirely and are unaffected by this change).

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs
git commit -m "fix(player): use Stopwatch-based progress in crossfade loop to eliminate timing drift"
```

---

## Task 2 — Read fresh position in `PlayerService.RunCrossfadeAsync`

**Files:**
- Modify: `src/Rok.Application/Player/PlayerService.cs:591-631`

`currentPosition` is captured in `CrossfadeToNextTrackAsync` and passed through to `RunCrossfadeAsync`. By the time `timeToWaitBeforeCrossfade` is computed, the value is slightly stale (CTS creation overhead etc.). Reading the position fresh removes this small error.

- [ ] **Step 1: Read position fresh just before the delay computation**

Open `src/Rok.Application/Player/PlayerService.cs`. Locate `RunCrossfadeAsync` (around line 591). The method signature is:

```csharp
private async Task RunCrossfadeAsync(int nextIndex, TrackDto nextTrack, double trackLength, double currentPosition, CancellationToken cancellationToken)
```

Find these two lines inside the method:

```csharp
double timeToWaitBeforeCrossfade = Math.Max(0, trackLength - currentPosition - crossfadeDurationSeconds);

if (timeToWaitBeforeCrossfade > 0)
    await Task.Delay(TimeSpan.FromSeconds(timeToWaitBeforeCrossfade), cancellationToken);
```

Replace them with:

```csharp
double freshPosition = _player.Position;
double timeToWaitBeforeCrossfade = Math.Max(0, trackLength - freshPosition - crossfadeDurationSeconds);

if (timeToWaitBeforeCrossfade > 0)
    await Task.Delay(TimeSpan.FromSeconds(timeToWaitBeforeCrossfade), cancellationToken);
```

Leave `currentPosition` in place — it is still used earlier in the method for `remainingTime` and `crossfadeDurationSeconds` computation, which is correct (those need the value at the moment the crossfade decision was made).

- [ ] **Step 2: Build**

```bash
dotnet build /p:Platform=x64 -v quiet
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Run full test suite**

```bash
dotnet test /p:Platform=x64
```

Expected: all tests pass. Note the exact count so you can verify no test was silently dropped.

- [ ] **Step 4: Commit**

```bash
git add src/Rok.Application/Player/PlayerService.cs
git commit -m "fix(player): read fresh engine position before computing crossfade wait delay"
```

---

## Task 3 — Final verification

- [ ] **Step 1: Clean build**

```bash
dotnet build /p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 2: Full test suite**

```bash
dotnet test /p:Platform=x64
```

Expected: all tests green.

- [ ] **Step 3: Manual smoke test**

Launch the app (`src/Presentation`). Play a playlist of at least 3 tracks with crossfade enabled. Listen to 2–3 transitions and confirm the fade is smooth and consistently timed. The old track should fade out exactly as the new track fades in, with no audible gap or abrupt cut.
