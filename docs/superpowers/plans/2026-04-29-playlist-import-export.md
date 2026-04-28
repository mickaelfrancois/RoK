# Playlist Import / Export (M3U8) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add bidirectional playlist import/export limited to the M3U8 format, fully respecting Rok's Clean Architecture (Domain → Application → Infrastructure → Presentation).

**Architecture:** Strategy pattern via `IPlaylistFormatReader` / `IPlaylistFormatWriter` (Application interfaces, Infrastructure implementations). Two CQRS handlers (`ImportPlaylistCommandHandler`, `ExportPlaylistCommandHandler`) orchestrate parsing, file I/O, track matching (`ITrackRepository.GetByFilePathAsync`) and atomic SQLite writes. Two Presentation services (`PlaylistImportService`, `PlaylistExportService`) own pickers, dialogs, and toasts. Adding PLS/XSPF later = 1 reader + 1 writer + 1 enum entry + 1 DI line.

**Tech Stack:** .NET 10 / C# 13, WinUI 3, Dapper + SQLite, Dapper.Contrib, MiF.Mediator, MiF.SimpleMessenger, MiF.Result, CommunityToolkit.Mvvm, xUnit + Moq + Microsoft.Extensions.TimeProvider.Testing.

**Reference design:** `docs/superpowers/specs/2026-04-28-playlist-import-export-design.md`

---

## Codebase pre-flight notes

These are the alignments between the design and the actual codebase the engineer must respect. Any deviation here is a bug.

| Topic | Design says | Codebase reality | Choice |
|---|---|---|---|
| `Result.Ok` | yes | `MiF.Result.Result.Success()` / `Result<T>.Success(value)` | Use `Success` everywhere |
| Mediator call | `mediator.Send(...)` | `IMediator.SendMessageAsync(...)` (MiF.Mediator) | Use `SendMessageAsync` |
| Track file column | `WHERE filePath = …` | actual SQLite column is `musicFile`; entity prop is `TrackEntity.MusicFile` | SQL: `WHERE musicFile = @filePath COLLATE NOCASE LIMIT 1` |
| Messenger | publish via messenger | static `MiF.SimpleMessenger.Messenger.Send(...)` (see `Rok.Application/Player/PlayerService.cs`) | `Messenger.Send(new PlaylistImportedMessage(id))` after commit |
| Transaction | "open SQLite transaction" | existing repos don't expose `IDbTransaction`; `PlaylistHeaderRepository.DeleteAsync` calls `connection.BeginTransaction()` directly | Inject `IDbConnection` into the import handler and run inserts under `BeginTransaction()` with raw Dapper SQL |
| Messages folder | `Rok.Application/Features/Playlists/Messages/` | existing messages live under flat `Rok.Application/Messages/` | Follow the design (new folder) — message is feature-specific |

The `playlists` table columns (from `MigrationService.cs` and existing repositories): `id, name, picture, duration, trackCount, trackMaximum, durationMaximum, groupsJson, type, creatDate, editDate`.

The `playlisttracks` table columns: `id, playlistId, trackId, position, listened, creatDate`.

`PlaylistType` enum (`Rok.Shared.Enums`): `Smart = 0`, `Classic = 1` (verify via `Rok.Application.Dto.PlaylistHeaderDto.IsSmart => Type == 0`).

---

## File structure

### New files

```
src/Rok.Application/Features/Playlists/IO/
  ExportPlaylistFormat.cs                    enum, M3u8 only in v1
  PlaylistFileModel.cs                       record + PlaylistFileEntry record
  IPlaylistFormatReader.cs                   interface
  IPlaylistFormatWriter.cs                   interface
  IPlaylistFormatResolver.cs                 interface

src/Rok.Application/Features/Playlists/Messages/
  PlaylistImportedMessage.cs                 messenger payload

src/Rok.Application/Features/Playlists/
  PlaylistImportResult.cs                    DTO returned by import command (with PlaylistImportStatus enum)

src/Rok.Application/Features/Playlists/Command/
  ImportPlaylistCommandHandler.cs            command + handler in same file (existing convention)
  ExportPlaylistCommandHandler.cs            command + handler in same file

src/Rok.Infrastructure/Playlists/
  PlaylistFormatResolver.cs                  resolves reader/writer by extension
  Formats/M3u8PlaylistReader.cs              M3U8 parser
  Formats/M3u8PlaylistWriter.cs              M3U8 serializer

src/Presentation/ViewModels/Playlists/Services/
  PlaylistImportService.cs                   FileOpenPicker + dispatch + recap toast

src/Presentation/ViewModels/Playlist/Services/
  PlaylistExportService.cs                   smart-warning dialog + FileSavePicker + toast

src/Presentation/ViewModels/Playlists/Handlers/
  PlaylistImportedMessageHandler.cs          forces playlists list refresh
```

### Modified files

```
src/Rok.Application/Interfaces/Repositories/ITrackRepository.cs    + GetByFilePathAsync
src/Rok.Infrastructure/Repositories/TrackRepository.cs              + GetByFilePathAsync impl
src/Rok.Infrastructure/DependencyInjection.cs                       register reader, writer, resolver
src/Rok.Application/DependencyInjection.cs                          (no change — handlers auto-discovered by AddSimpleMediator)
src/Presentation/DependencyInjection.cs                             register PlaylistImportService, PlaylistExportService, PlaylistImportedMessageHandler
src/Presentation/ViewModels/Playlists/PlaylistsViewModel.cs         + ImportPlaylistsCommand, subscribe to PlaylistImportedMessage
src/Presentation/ViewModels/Playlist/PlaylistViewModel.cs           + ExportPlaylistCommand
src/Presentation/Pages/PlaylistsPage.xaml                           + Import button + per-item Export MenuFlyoutItem
src/Presentation/Pages/PlaylistPage.xaml                            + Export AppBarButton
src/Presentation/Strings/en-US/Resources.resw                       new x:Uid strings (import button, export menu, smart warning, toasts)
src/Presentation/Strings/fr-FR/Resources.resw                       same strings, French
```

### New test files

```
tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistReaderTests.cs
tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistWriterTests.cs
tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/PlaylistFormatResolverTests.cs
tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/TrackRepositoryGetByFilePathTests.cs
tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/    M3U8 fixtures (8 files)

tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ImportPlaylistCommandHandlerTests.cs
tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ExportPlaylistCommandHandlerTests.cs

tests/UnitTests/Rok.PresentationTests/ViewModels/Playlists/Services/PlaylistImportServiceTests.cs
tests/UnitTests/Rok.PresentationTests/ViewModels/Playlist/Services/PlaylistExportServiceTests.cs
```

---

## Task 1: Application IO contracts (enum + record + reader/writer interfaces)

**Files:**
- Create: `src/Rok.Application/Features/Playlists/IO/ExportPlaylistFormat.cs`
- Create: `src/Rok.Application/Features/Playlists/IO/PlaylistFileModel.cs`
- Create: `src/Rok.Application/Features/Playlists/IO/IPlaylistFormatReader.cs`
- Create: `src/Rok.Application/Features/Playlists/IO/IPlaylistFormatWriter.cs`
- Create: `src/Rok.Application/Features/Playlists/IO/IPlaylistFormatResolver.cs`

These files are interfaces and DTOs only — no behavior, so no test in this task. Tests will pin behavior in later tasks (resolver, reader, writer, handlers).

- [ ] **Step 1: Create `ExportPlaylistFormat.cs`**

```csharp
namespace Rok.Application.Features.Playlists.IO;

public enum ExportPlaylistFormat
{
    M3u8
}
```

- [ ] **Step 2: Create `PlaylistFileModel.cs`**

```csharp
namespace Rok.Application.Features.Playlists.IO;

public sealed record PlaylistFileModel(
    string Name,
    IReadOnlyList<PlaylistFileEntry> Entries);

public sealed record PlaylistFileEntry(
    string FilePath,
    string? Title,
    string? Artist,
    TimeSpan? Duration);
```

- [ ] **Step 3: Create `IPlaylistFormatReader.cs`**

```csharp
namespace Rok.Application.Features.Playlists.IO;

public interface IPlaylistFormatReader
{
    ExportPlaylistFormat Format { get; }

    Task<PlaylistFileModel> ReadAsync(Stream stream, string fileNameHint, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Create `IPlaylistFormatWriter.cs`**

```csharp
namespace Rok.Application.Features.Playlists.IO;

public interface IPlaylistFormatWriter
{
    ExportPlaylistFormat Format { get; }

    Task WriteAsync(Stream stream, PlaylistFileModel model, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Create `IPlaylistFormatResolver.cs`**

```csharp
namespace Rok.Application.Features.Playlists.IO;

public interface IPlaylistFormatResolver
{
    bool TryGetReader(string extension, out IPlaylistFormatReader? reader);

    bool TryGetWriter(string extension, out IPlaylistFormatWriter? writer);
}
```

- [ ] **Step 6: Build**

Run: `dotnet build /p:Platform=x64`
Expected: build succeeds, zero warnings (treat-warnings-as-errors is enabled).

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Application/Features/Playlists/IO/
git commit -m "feat(playlists): add IO contracts for playlist file formats"
```

---

## Task 2: `PlaylistFormatResolver` (Infrastructure) with tests

**Files:**
- Create: `src/Rok.Infrastructure/Playlists/PlaylistFormatResolver.cs`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/PlaylistFormatResolverTests.cs`

The resolver receives `IEnumerable<IPlaylistFormatReader>` and `IEnumerable<IPlaylistFormatWriter>` from DI. In v1 only the M3U8 reader/writer are registered; the reader accepts both `.m3u` and `.m3u8`, the writer only `.m3u8`. Extension comparison is case-insensitive.

- [ ] **Step 1: Write the failing test file**

Create `tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/PlaylistFormatResolverTests.cs`:

```csharp
using Moq;
using Rok.Application.Features.Playlists.IO;
using Rok.Infrastructure.Playlists;

namespace Rok.Infrastructure.UnitTests.Playlists;

public class PlaylistFormatResolverTests
{
    private static IPlaylistFormatReader BuildReader()
    {
        Mock<IPlaylistFormatReader> reader = new();
        reader.SetupGet(r => r.Format).Returns(ExportPlaylistFormat.M3u8);
        return reader.Object;
    }

    private static IPlaylistFormatWriter BuildWriter()
    {
        Mock<IPlaylistFormatWriter> writer = new();
        writer.SetupGet(w => w.Format).Returns(ExportPlaylistFormat.M3u8);
        return writer.Object;
    }

    [Fact(DisplayName = "resolves_reader_for_m3u8_extension")]
    public void Resolves_reader_for_m3u8_extension()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool found = sut.TryGetReader(".m3u8", out IPlaylistFormatReader? reader);

        // Assert
        Assert.True(found);
        Assert.NotNull(reader);
    }

    [Fact(DisplayName = "resolves_reader_for_m3u_extension")]
    public void Resolves_reader_for_m3u_extension()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool found = sut.TryGetReader(".m3u", out IPlaylistFormatReader? reader);

        // Assert
        Assert.True(found);
        Assert.NotNull(reader);
    }

    [Fact(DisplayName = "extension_match_is_case_insensitive")]
    public void Extension_match_is_case_insensitive()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool readerFound = sut.TryGetReader(".M3U8", out _);
        bool writerFound = sut.TryGetWriter(".M3U8", out _);

        // Assert
        Assert.True(readerFound);
        Assert.True(writerFound);
    }

    [Fact(DisplayName = "returns_false_for_unknown_extension")]
    public void Returns_false_for_unknown_extension()
    {
        // Arrange
        PlaylistFormatResolver sut = new(new[] { BuildReader() }, new[] { BuildWriter() });

        // Act
        bool readerFound = sut.TryGetReader(".pls", out IPlaylistFormatReader? reader);
        bool writerFound = sut.TryGetWriter(".m3u", out IPlaylistFormatWriter? writer);

        // Assert
        Assert.False(readerFound);
        Assert.Null(reader);
        Assert.False(writerFound);
        Assert.Null(writer);
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64`
Expected: build fails — `PlaylistFormatResolver` does not exist.

- [ ] **Step 3: Implement `PlaylistFormatResolver`**

Create `src/Rok.Infrastructure/Playlists/PlaylistFormatResolver.cs`:

```csharp
using Rok.Application.Features.Playlists.IO;

namespace Rok.Infrastructure.Playlists;

public sealed class PlaylistFormatResolver : IPlaylistFormatResolver
{
    private readonly IReadOnlyDictionary<string, IPlaylistFormatReader> _readers;
    private readonly IReadOnlyDictionary<string, IPlaylistFormatWriter> _writers;

    public PlaylistFormatResolver(IEnumerable<IPlaylistFormatReader> readers, IEnumerable<IPlaylistFormatWriter> writers)
    {
        Dictionary<string, IPlaylistFormatReader> readerMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IPlaylistFormatWriter> writerMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (IPlaylistFormatReader reader in readers)
        {
            foreach (string ext in ExtensionsForReader(reader.Format))
            {
                readerMap[ext] = reader;
            }
        }

        foreach (IPlaylistFormatWriter writer in writers)
        {
            foreach (string ext in ExtensionsForWriter(writer.Format))
            {
                writerMap[ext] = writer;
            }
        }

        _readers = readerMap;
        _writers = writerMap;
    }

    public bool TryGetReader(string extension, out IPlaylistFormatReader? reader)
    {
        bool found = _readers.TryGetValue(extension, out IPlaylistFormatReader? r);
        reader = found ? r : null;
        return found;
    }

    public bool TryGetWriter(string extension, out IPlaylistFormatWriter? writer)
    {
        bool found = _writers.TryGetValue(extension, out IPlaylistFormatWriter? w);
        writer = found ? w : null;
        return found;
    }

    private static IEnumerable<string> ExtensionsForReader(ExportPlaylistFormat format)
    {
        return format switch
        {
            ExportPlaylistFormat.M3u8 => new[] { ".m3u", ".m3u8" },
            _ => Array.Empty<string>()
        };
    }

    private static IEnumerable<string> ExtensionsForWriter(ExportPlaylistFormat format)
    {
        return format switch
        {
            ExportPlaylistFormat.M3u8 => new[] { ".m3u8" },
            _ => Array.Empty<string>()
        };
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlaylistFormatResolverTests"`
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Infrastructure/Playlists/PlaylistFormatResolver.cs tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/PlaylistFormatResolverTests.cs
git commit -m "feat(playlists): add format resolver matching extensions to readers/writers"
```

---

## Task 3: `M3u8PlaylistWriter` with tests

**Files:**
- Create: `src/Rok.Infrastructure/Playlists/Formats/M3u8PlaylistWriter.cs`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistWriterTests.cs`

Output rules (from spec §M3u8PlaylistWriter):
- `#EXTM3U` always first line
- `#EXTINF:<seconds>,<artist> - <title>` always emitted (one per entry)
- Duration rounded to integer seconds; `-1` if `Duration` is null
- If artist or title is null/empty, still emit the dash separator (`Artist - `, ` - Title`, or `,` with empty rest)
- UTF-8 **without BOM**
- Line endings `\n` (LF), no platform-default CRLF

- [ ] **Step 1: Write failing tests**

Create `tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistWriterTests.cs`:

```csharp
using System.Text;
using Rok.Application.Features.Playlists.IO;
using Rok.Infrastructure.Playlists.Formats;

namespace Rok.Infrastructure.UnitTests.Playlists.Formats;

public class M3u8PlaylistWriterTests
{
    private static async Task<(string Text, byte[] Bytes)> WriteAsync(PlaylistFileModel model)
    {
        M3u8PlaylistWriter writer = new();
        using MemoryStream stream = new();
        await writer.WriteAsync(stream, model, CancellationToken.None);
        byte[] bytes = stream.ToArray();
        return (Encoding.UTF8.GetString(bytes), bytes);
    }

    [Fact(DisplayName = "writes_extm3u_header_first")]
    public async Task Writes_extm3u_header_first()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>());

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.StartsWith("#EXTM3U\n", text);
    }

    [Fact(DisplayName = "writes_extinf_with_seconds_and_artist_dash_title")]
    public async Task Writes_extinf_with_seconds_and_artist_dash_title()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "One More Time", "Daft Punk", TimeSpan.FromSeconds(215))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:215,Daft Punk - One More Time\n", text);
    }

    [Fact(DisplayName = "writes_path_after_extinf")]
    public async Task Writes_path_after_extinf()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "T", "A", TimeSpan.FromSeconds(10))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:10,A - T\nD:\\Music\\track.mp3\n", text);
    }

    [Fact(DisplayName = "writes_extinf_with_minus_one_when_duration_unknown")]
    public async Task Writes_extinf_with_minus_one_when_duration_unknown()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "T", "A", null)
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:-1,A - T\n", text);
    }

    [Fact(DisplayName = "output_is_utf8_without_bom")]
    public async Task Output_is_utf8_without_bom()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>());

        // Act
        (_, byte[] bytes) = await WriteAsync(model);

        // Assert — UTF-8 BOM is EF BB BF
        Assert.True(bytes.Length < 3 || !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF));
    }

    [Fact(DisplayName = "uses_lf_line_endings")]
    public async Task Uses_lf_line_endings()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "T", "A", TimeSpan.FromSeconds(10))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.DoesNotContain("\r\n", text);
    }

    [Fact(DisplayName = "emits_empty_artist_with_dash_when_artist_null")]
    public async Task Emits_empty_artist_with_dash_when_artist_null()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "Title only", null, TimeSpan.FromSeconds(60))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:60, - Title only\n", text);
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~M3u8PlaylistWriterTests"`
Expected: fails to build — `M3u8PlaylistWriter` is missing.

- [ ] **Step 3: Implement `M3u8PlaylistWriter`**

Create `src/Rok.Infrastructure/Playlists/Formats/M3u8PlaylistWriter.cs`:

```csharp
using System.Globalization;
using System.Text;
using Rok.Application.Features.Playlists.IO;

namespace Rok.Infrastructure.Playlists.Formats;

public sealed class M3u8PlaylistWriter : IPlaylistFormatWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public ExportPlaylistFormat Format => ExportPlaylistFormat.M3u8;

    public async Task WriteAsync(Stream stream, PlaylistFileModel model, CancellationToken cancellationToken)
    {
        await using StreamWriter writer = new(stream, Utf8NoBom, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\n"
        };

        await writer.WriteLineAsync("#EXTM3U".AsMemory(), cancellationToken);

        foreach (PlaylistFileEntry entry in model.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int seconds = entry.Duration.HasValue
                ? (int)Math.Round(entry.Duration.Value.TotalSeconds, MidpointRounding.AwayFromZero)
                : -1;

            string artist = entry.Artist ?? string.Empty;
            string title = entry.Title ?? string.Empty;
            string label = string.Concat(artist, " - ", title);

            string extinf = string.Create(CultureInfo.InvariantCulture, $"#EXTINF:{seconds},{label}");
            await writer.WriteLineAsync(extinf.AsMemory(), cancellationToken);
            await writer.WriteLineAsync(entry.FilePath.AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~M3u8PlaylistWriterTests"`
Expected: 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Infrastructure/Playlists/Formats/M3u8PlaylistWriter.cs tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistWriterTests.cs
git commit -m "feat(playlists): add M3U8 writer with EXTM3U/EXTINF and UTF-8 no-BOM output"
```

---

## Task 4: M3U8 fixture files

**Files:**
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/minimal.m3u8`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/extended.m3u8`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/with_dash_in_title.m3u8`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/unknown_directives.m3u8`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/utf8_bom.m3u8`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/utf8_no_bom.m3u8`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/crlf_endings.m3u8`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/lf_endings.m3u8`
- Modify: `tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj` to copy fixtures to output

These fixtures back the reader tests. They need to ship to the test bin folder so tests can `File.OpenRead` them.

- [ ] **Step 1: Write `minimal.m3u8` (LF, no BOM, paths only)**

Content (write exactly, with `\n` line endings):

```
D:\Music\Daft Punk\Discovery\01 - One More Time.mp3
D:\Music\Daft Punk\Discovery\02 - Aerodynamic.mp3
D:\Music\Daft Punk\Discovery\03 - Digital Love.mp3
```

(Use a tool that writes raw bytes — most editors are fine; verify no BOM is added.)

- [ ] **Step 2: Write `extended.m3u8` (LF, no BOM, with EXTM3U + EXTINF)**

```
#EXTM3U
#EXTINF:215,Daft Punk - One More Time
D:\Music\Daft Punk\Discovery\01 - One More Time.mp3
#EXTINF:212,Daft Punk - Aerodynamic
D:\Music\Daft Punk\Discovery\02 - Aerodynamic.mp3
```

- [ ] **Step 3: Write `with_dash_in_title.m3u8`**

```
#EXTM3U
#EXTINF:300,Bowie - Ziggy Stardust - Live
D:\Music\Bowie\Ziggy.mp3
```

- [ ] **Step 4: Write `unknown_directives.m3u8`**

```
#EXTM3U
#PLAYLIST:My weird playlist
#EXTGRP:Rock
#EXTINF:200,Foo - Bar
D:\Music\foo.mp3
```

- [ ] **Step 5: Write `utf8_bom.m3u8` (UTF-8 with BOM)**

Content (note: file must start with bytes `EF BB BF` before `#EXTM3U`):

```
#EXTM3U
#EXTINF:100,Édith Piaf - La Vie en rose
D:\Music\piaf.mp3
```

Use a hex editor or `Encoding.UTF8.GetPreamble()` from a one-shot script if needed. Verify with `Get-Content -Encoding Byte -TotalCount 3 utf8_bom.m3u8` returns `239 187 191`.

- [ ] **Step 6: Write `utf8_no_bom.m3u8` (same content, no BOM)**

```
#EXTM3U
#EXTINF:100,Édith Piaf - La Vie en rose
D:\Music\piaf.mp3
```

- [ ] **Step 7: Write `crlf_endings.m3u8` (`\r\n` line endings)**

Same lines as `extended.m3u8` but joined with `\r\n`. The simplest reliable way is to copy `extended.m3u8` and convert with `(Get-Content extended.m3u8 -Raw) -replace "\n", "`r`n" | Set-Content -NoNewline crlf_endings.m3u8` — verify with `Format-Hex` that pairs of `0D 0A` exist.

- [ ] **Step 8: Write `lf_endings.m3u8` (identical content, LF endings)**

Identical to `extended.m3u8`. Keep as a separate fixture for clarity in the test name.

- [ ] **Step 9: Update test csproj to copy fixtures to output**

Edit `tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj`. Inside an `<ItemGroup>` (creating one if absent), add:

```xml
<ItemGroup>
  <None Update="TestData\Playlists\*.m3u*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 10: Verify fixtures land in bin**

Run: `dotnet build tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64`
Then verify: `ls tests/UnitTests/Rok.Infrastructure.UnitTests/bin/x64/Debug/*/TestData/Playlists/`
Expected: 8 fixture files present.

- [ ] **Step 11: Commit**

```bash
git add tests/UnitTests/Rok.Infrastructure.UnitTests/TestData/Playlists/ tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj
git commit -m "test(playlists): add M3U8 fixtures and copy to test output"
```

---

## Task 5: `M3u8PlaylistReader` with tests

**Files:**
- Create: `src/Rok.Infrastructure/Playlists/Formats/M3u8PlaylistReader.cs`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistReaderTests.cs`

Parsing rules (from spec §M3u8PlaylistReader):
- `#EXTM3U` tolerated whether present or absent
- `#EXTINF:<seconds>,<artist> - <title>` — split label on the **first** ` - ` so titles containing ` - ` survive correctly; if no ` - `, treat the whole label as `Title` and `Artist=null`
- Any `#…` other than `#EXTINF` → ignored as comment/directive
- Empty lines ignored
- Any other line is a file path; the next non-`#` non-empty line consumes the most recent `#EXTINF` payload (one EXTINF binds to the next path)
- `Name` returned = `Path.GetFileNameWithoutExtension(fileNameHint)`
- Encoding: `StreamReader` with `detectEncodingFromByteOrderMarks: true`, fallback UTF-8

- [ ] **Step 1: Write failing tests**

Create `tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistReaderTests.cs`:

```csharp
using Rok.Application.Features.Playlists.IO;
using Rok.Infrastructure.Playlists.Formats;

namespace Rok.Infrastructure.UnitTests.Playlists.Formats;

public class M3u8PlaylistReaderTests
{
    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TestData", "Playlists", name);

    private static async Task<PlaylistFileModel> ReadAsync(string fileName)
    {
        M3u8PlaylistReader reader = new();
        await using FileStream fs = File.OpenRead(FixturePath(fileName));
        return await reader.ReadAsync(fs, fileName, CancellationToken.None);
    }

    [Fact(DisplayName = "reads_minimal_playlist_with_paths_only")]
    public async Task Reads_minimal_playlist_with_paths_only()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("minimal.m3u8");

        // Assert
        Assert.Equal("minimal", model.Name);
        Assert.Equal(3, model.Entries.Count);
        Assert.Null(model.Entries[0].Artist);
        Assert.Null(model.Entries[0].Title);
        Assert.Null(model.Entries[0].Duration);
    }

    [Fact(DisplayName = "reads_extm3u_header_when_present")]
    public async Task Reads_extm3u_header_when_present()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("extended.m3u8");

        // Assert
        Assert.Equal(2, model.Entries.Count);
    }

    [Fact(DisplayName = "tolerates_missing_extm3u_header")]
    public async Task Tolerates_missing_extm3u_header()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("minimal.m3u8");

        // Assert — minimal.m3u8 has no #EXTM3U yet still parses
        Assert.NotEmpty(model.Entries);
    }

    [Fact(DisplayName = "reads_extinf_artist_title_and_duration")]
    public async Task Reads_extinf_artist_title_and_duration()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("extended.m3u8");

        // Assert
        PlaylistFileEntry first = model.Entries[0];
        Assert.Equal("Daft Punk", first.Artist);
        Assert.Equal("One More Time", first.Title);
        Assert.Equal(TimeSpan.FromSeconds(215), first.Duration);
    }

    [Fact(DisplayName = "parses_extinf_with_dash_in_title")]
    public async Task Parses_extinf_with_dash_in_title()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("with_dash_in_title.m3u8");

        // Assert — split on FIRST " - "
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Bowie", only.Artist);
        Assert.Equal("Ziggy Stardust - Live", only.Title);
    }

    [Fact(DisplayName = "treats_unknown_directives_as_comments")]
    public async Task Treats_unknown_directives_as_comments()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("unknown_directives.m3u8");

        // Assert — name comes from filename, not from #PLAYLIST
        Assert.Equal("unknown_directives", model.Name);
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Foo", only.Artist);
        Assert.Equal("Bar", only.Title);
    }

    [Fact(DisplayName = "skips_blank_lines")]
    public async Task Skips_blank_lines()
    {
        // Arrange — write a tmp file inline so we can interleave blanks
        string tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "#EXTM3U\n\n\nD:\\foo.mp3\n\n");
        try
        {
            M3u8PlaylistReader reader = new();
            await using FileStream fs = File.OpenRead(tmp);

            // Act
            PlaylistFileModel model = await reader.ReadAsync(fs, "tmp.m3u8", CancellationToken.None);

            // Assert
            Assert.Single(model.Entries);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact(DisplayName = "handles_utf8_with_bom")]
    public async Task Handles_utf8_with_bom()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("utf8_bom.m3u8");

        // Assert — accent must round-trip
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Édith Piaf", only.Artist);
    }

    [Fact(DisplayName = "handles_utf8_without_bom")]
    public async Task Handles_utf8_without_bom()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("utf8_no_bom.m3u8");

        // Assert
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Édith Piaf", only.Artist);
    }

    [Fact(DisplayName = "crlf_and_lf_line_endings_supported")]
    public async Task Crlf_and_lf_line_endings_supported()
    {
        // Arrange + Act
        PlaylistFileModel crlf = await ReadAsync("crlf_endings.m3u8");
        PlaylistFileModel lf = await ReadAsync("lf_endings.m3u8");

        // Assert
        Assert.Equal(crlf.Entries.Count, lf.Entries.Count);
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~M3u8PlaylistReaderTests"`
Expected: build fails — `M3u8PlaylistReader` missing.

- [ ] **Step 3: Implement `M3u8PlaylistReader`**

Create `src/Rok.Infrastructure/Playlists/Formats/M3u8PlaylistReader.cs`:

```csharp
using System.Globalization;
using System.Text;
using Rok.Application.Features.Playlists.IO;

namespace Rok.Infrastructure.Playlists.Formats;

public sealed class M3u8PlaylistReader : IPlaylistFormatReader
{
    private const string ExtinfPrefix = "#EXTINF:";
    private const string ArtistTitleSeparator = " - ";

    public ExportPlaylistFormat Format => ExportPlaylistFormat.M3u8;

    public async Task<PlaylistFileModel> ReadAsync(Stream stream, string fileNameHint, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

        List<PlaylistFileEntry> entries = new();
        TimeSpan? pendingDuration = null;
        string? pendingArtist = null;
        string? pendingTitle = null;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            string trimmed = line.Trim();

            if (trimmed.Length == 0)
                continue;

            if (trimmed.StartsWith(ExtinfPrefix, StringComparison.Ordinal))
            {
                ParseExtinf(trimmed, out pendingDuration, out pendingArtist, out pendingTitle);
                continue;
            }

            if (trimmed[0] == '#')
                continue;

            entries.Add(new PlaylistFileEntry(trimmed, pendingTitle, pendingArtist, pendingDuration));
            pendingDuration = null;
            pendingArtist = null;
            pendingTitle = null;
        }

        string name = Path.GetFileNameWithoutExtension(fileNameHint);
        return new PlaylistFileModel(name, entries);
    }

    private static void ParseExtinf(string line, out TimeSpan? duration, out string? artist, out string? title)
    {
        duration = null;
        artist = null;
        title = null;

        ReadOnlySpan<char> payload = line.AsSpan(ExtinfPrefix.Length);
        int comma = payload.IndexOf(',');

        ReadOnlySpan<char> durationSpan = comma >= 0 ? payload[..comma] : payload;
        if (int.TryParse(durationSpan.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) && seconds >= 0)
            duration = TimeSpan.FromSeconds(seconds);

        if (comma < 0)
            return;

        string label = payload[(comma + 1)..].Trim().ToString();
        if (label.Length == 0)
            return;

        int sep = label.IndexOf(ArtistTitleSeparator, StringComparison.Ordinal);
        if (sep < 0)
        {
            title = label;
            return;
        }

        string a = label[..sep].Trim();
        string t = label[(sep + ArtistTitleSeparator.Length)..].Trim();
        artist = a.Length == 0 ? null : a;
        title = t.Length == 0 ? null : t;
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~M3u8PlaylistReaderTests"`
Expected: 10 tests pass.

- [ ] **Step 5: Add a writer↔reader roundtrip test**

Append to `M3u8PlaylistWriterTests.cs`:

```csharp
[Fact(DisplayName = "roundtrip_with_reader_preserves_paths_and_metadata")]
public async Task Roundtrip_with_reader_preserves_paths_and_metadata()
{
    // Arrange
    PlaylistFileModel original = new("Mix", new List<PlaylistFileEntry>
    {
        new(@"D:\Music\a.mp3", "Title A", "Artist A", TimeSpan.FromSeconds(123)),
        new(@"D:\Music\b.mp3", "Title B - Live", "Artist B", TimeSpan.FromSeconds(45))
    });

    M3u8PlaylistWriter writer = new();
    using MemoryStream stream = new();
    await writer.WriteAsync(stream, original, CancellationToken.None);

    // Act
    stream.Position = 0;
    Rok.Infrastructure.Playlists.Formats.M3u8PlaylistReader reader = new();
    PlaylistFileModel parsed = await reader.ReadAsync(stream, "round.m3u8", CancellationToken.None);

    // Assert
    Assert.Equal(2, parsed.Entries.Count);
    Assert.Equal(@"D:\Music\a.mp3", parsed.Entries[0].FilePath);
    Assert.Equal("Title A", parsed.Entries[0].Title);
    Assert.Equal("Artist A", parsed.Entries[0].Artist);
    Assert.Equal(TimeSpan.FromSeconds(123), parsed.Entries[0].Duration);
    Assert.Equal("Title B - Live", parsed.Entries[1].Title);
}
```

- [ ] **Step 6: Run all infra tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64`
Expected: previously passing tests still pass + 11 new (10 reader + 1 roundtrip).

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Infrastructure/Playlists/Formats/M3u8PlaylistReader.cs tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistReaderTests.cs tests/UnitTests/Rok.Infrastructure.UnitTests/Playlists/Formats/M3u8PlaylistWriterTests.cs
git commit -m "feat(playlists): add M3U8 reader with EXTINF parsing and BOM-tolerant decoding"
```

---

## Task 6: `ITrackRepository.GetByFilePathAsync` + impl + tests

**Files:**
- Modify: `src/Rok.Application/Interfaces/Repositories/ITrackRepository.cs`
- Modify: `src/Rok.Infrastructure/Repositories/TrackRepository.cs`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/TrackRepositoryGetByFilePathTests.cs`

The DB column is `musicFile`. Match strategy: case-insensitive (Windows path semantics) using SQLite `COLLATE NOCASE`.

- [ ] **Step 1: Write failing tests**

Create `tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/TrackRepositoryGetByFilePathTests.cs`:

```csharp
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class TrackRepositoryGetByFilePathTests
{
    private static SqliteDatabaseFixture CreateFixture()
    {
        SqliteDatabaseFixture fixture = new();
        // Insert a known file path on top of the seeded tracks
        fixture.Connection.Execute(@"
            INSERT INTO Tracks(
                id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber
            ) VALUES (
                100, 'PathTrack', 180, 1000, 128, @path, @now, 0, 0, 0, 0, @now, 1, 1, 1
            )", new { path = @"D:\Music\Daft Punk\Discovery\01 - One More Time.mp3", now = DateTime.UtcNow });
        return fixture;
    }

    private static TrackRepository CreateRepository(SqliteDatabaseFixture fixture)
        => new(fixture.Connection, fixture.Connection, NullLogger<TrackRepository>.Instance);

    [Fact(DisplayName = "returns_track_when_file_path_matches_exactly")]
    public async Task Returns_track_when_file_path_matches_exactly()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? track = await repo.GetByFilePathAsync(@"D:\Music\Daft Punk\Discovery\01 - One More Time.mp3", CancellationToken.None);

        // Assert
        Assert.NotNull(track);
        Assert.Equal(100, track.Id);
    }

    [Fact(DisplayName = "returns_track_when_file_path_matches_case_insensitive")]
    public async Task Returns_track_when_file_path_matches_case_insensitive()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? track = await repo.GetByFilePathAsync(@"d:\music\daft punk\discovery\01 - one more time.mp3", CancellationToken.None);

        // Assert
        Assert.NotNull(track);
        Assert.Equal(100, track.Id);
    }

    [Fact(DisplayName = "returns_null_when_no_match")]
    public async Task Returns_null_when_no_match()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? track = await repo.GetByFilePathAsync(@"D:\Nope\nope.mp3", CancellationToken.None);

        // Assert
        Assert.Null(track);
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~TrackRepositoryGetByFilePathTests"`
Expected: build fails — `GetByFilePathAsync` missing.

- [ ] **Step 3: Add the method to the interface**

Edit `src/Rok.Application/Interfaces/Repositories/ITrackRepository.cs` — append before the closing brace:

```csharp
    Task<TrackEntity?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken);
```

- [ ] **Step 4: Implement on `TrackRepository`**

Edit `src/Rok.Infrastructure/Repositories/TrackRepository.cs` — append before the override of `GetSelectQuery`:

```csharp
    public async Task<TrackEntity?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        string sql = GetSelectQuery() + " WHERE tracks.musicFile = @filePath COLLATE NOCASE " + DefaultGroupBy + " LIMIT 1";

        IDbConnection localConnection = ResolveConnection(RepositoryConnectionKind.Foreground);
        return await localConnection.QueryFirstOrDefaultAsync<TrackEntity>(new CommandDefinition(sql, new { filePath }, cancellationToken: cancellationToken));
    }
```

(`Dapper` and `CommandDefinition` are already in scope via existing usings.)

- [ ] **Step 5: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~TrackRepositoryGetByFilePathTests"`
Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Rok.Application/Interfaces/Repositories/ITrackRepository.cs src/Rok.Infrastructure/Repositories/TrackRepository.cs tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/TrackRepositoryGetByFilePathTests.cs
git commit -m "feat(playlists): add ITrackRepository.GetByFilePathAsync with case-insensitive match"
```

---

## Task 7: Application messaging + DTO scaffolding

**Files:**
- Create: `src/Rok.Application/Features/Playlists/Messages/PlaylistImportedMessage.cs`
- Create: `src/Rok.Application/Features/Playlists/PlaylistImportResult.cs`

These are pure data types and have no behavior, so no tests in this task. They'll be exercised by the import handler tests (Task 8) and import service tests (Task 11).

- [ ] **Step 1: Create `PlaylistImportedMessage.cs`**

```csharp
namespace Rok.Application.Features.Playlists.Messages;

public sealed record PlaylistImportedMessage(long PlaylistId);
```

- [ ] **Step 2: Create `PlaylistImportResult.cs`**

```csharp
namespace Rok.Application.Features.Playlists;

public enum PlaylistImportStatus
{
    Imported,
    Skipped
}

public sealed record PlaylistImportResult(
    PlaylistImportStatus Status,
    long? PlaylistId,
    string? FinalName,
    int MatchedCount,
    int IgnoredCount);
```

- [ ] **Step 3: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success, no warnings.

- [ ] **Step 4: Commit**

```bash
git add src/Rok.Application/Features/Playlists/Messages/ src/Rok.Application/Features/Playlists/PlaylistImportResult.cs
git commit -m "feat(playlists): add PlaylistImportedMessage and PlaylistImportResult"
```

---

## Task 8: `ImportPlaylistCommandHandler` with tests

**Files:**
- Create: `src/Rok.Application/Features/Playlists/Command/ImportPlaylistCommandHandler.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ImportPlaylistCommandHandlerTests.cs`

Pipeline (from spec §ImportPlaylistCommandHandler):
1. Resolve reader from extension. Unknown → `Result.Fail("UnsupportedFormat")`.
2. Open file, parse → `PlaylistFileModel`. Exception → `Result.Fail`.
3. For each entry → `ITrackRepository.GetByFilePathAsync`. Compute matched/ignored.
4. If matched count == 0 → return `Result.Success(new PlaylistImportResult(Skipped, null, null, 0, ignored))`. No transaction.
5. Compute `FinalName`: probe `playlists.name` for collisions, suffix `(2)` … `(999)`. Beyond that → `Result.Fail("NameCollisionExhausted")`.
6. Open SQLite transaction on the foreground connection. INSERT header (Type=Classic), INSERT tracks (Position 0..N-1, Listened=false). On exception → rollback, propagate as `Result.Fail`.
7. After commit → `Messenger.Send(new PlaylistImportedMessage(playlistId))`.
8. Return `Result.Success(new PlaylistImportResult(Imported, id, finalName, matched, ignored))`.

Constructor dependencies: `IPlaylistFormatResolver`, `ITrackRepository`, `IDbConnection`, `ILogger<ImportPlaylistCommandHandler>`. The handler does its own SQL/transaction management because the existing repositories don't expose `IDbTransaction` parameters. The header/track inserts use raw Dapper SQL keyed to the actual schema.

PlaylistType.Classic value: from existing code path the integer for Classic is `(int)PlaylistType.Classic` — `PlaylistsViewModel.cs:78` and `PlaylistCreationService.cs` confirm Classic ≠ 0; `PlaylistHeaderDto.IsSmart => Type == 0` proves Smart=0, so Classic=1.

- [ ] **Step 1: Write failing tests (subset 1: parse failure + matching + skipped)**

Create `tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ImportPlaylistCommandHandlerTests.cs`:

```csharp
using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Playlists;

public class ImportPlaylistCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IPlaylistFormatResolver> _resolver = new();
    private readonly Mock<IPlaylistFormatReader> _reader = new();
    private readonly Mock<ITrackRepository> _trackRepository = new();

    public ImportPlaylistCommandHandlerTests()
    {
        _connection = new SqliteConnection($"Data Source=ImportHandler_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _connection.Open();
        _connection.Execute("CREATE TABLE playlists (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, picture TEXT, duration INTEGER, trackCount INTEGER, trackMaximum INTEGER, durationMaximum INTEGER, groupsJson TEXT, type INTEGER, creatDate TEXT, editDate TEXT)");
        _connection.Execute("CREATE TABLE playlisttracks (id INTEGER PRIMARY KEY AUTOINCREMENT, playlistId INTEGER, trackId INTEGER, position INTEGER, listened INTEGER, creatDate TEXT)");
    }

    public void Dispose() => _connection.Dispose();

    private ImportPlaylistCommandHandler BuildHandler()
    {
        IPlaylistFormatReader reader = _reader.Object;
        _resolver.Setup(r => r.TryGetReader(It.IsAny<string>(), out reader)).Returns(true);
        return new ImportPlaylistCommandHandler(_resolver.Object, _trackRepository.Object, _connection, NullLogger<ImportPlaylistCommandHandler>.Instance);
    }

    private static PlaylistFileModel Model(string name, params (string Path, string? Title, string? Artist, TimeSpan? Duration)[] entries)
        => new(name, entries.Select(e => new PlaylistFileEntry(e.Path, e.Title, e.Artist, e.Duration)).ToList());

    private string WritePlaylistFile(string content, string fileName = "test.m3u8")
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{fileName}");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact(DisplayName = "creates_playlist_with_only_matched_tracks")]
    public async Task Creates_playlist_with_only_matched_tracks()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix",
                        ("D:\\a.mp3", "A", "Aa", TimeSpan.FromSeconds(100)),
                        ("D:\\miss.mp3", null, null, null),
                        ("D:\\b.mp3", "B", "Bb", TimeSpan.FromSeconds(50))));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 11, Duration = 100 });
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\miss.mp3", It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\b.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 12, Duration = 50 });

            ImportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(PlaylistImportStatus.Imported, result.Value.Status);
            Assert.Equal(2, result.Value.MatchedCount);
            Assert.Equal(1, result.Value.IgnoredCount);

            int trackRows = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlisttracks WHERE playlistId = @id", new { id = result.Value.PlaylistId });
            Assert.Equal(2, trackRows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "skipped_when_zero_tracks_match")]
    public async Task Skipped_when_zero_tracks_match()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Empty", ("D:\\miss.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);

            ImportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(PlaylistImportStatus.Skipped, result.Value.Status);
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "name_collision_is_suffixed_with_paren_two")]
    public async Task Name_collision_is_suffixed_with_paren_two()
    {
        // Arrange
        _connection.Execute("INSERT INTO playlists(name, type, creatDate) VALUES (@name, 1, @now)", new { name = "Mix", now = DateTime.UtcNow });
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1, Duration = 10 });

            ImportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Mix (2)", result.Value.FinalName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "track_positions_are_zero_based_and_sequential")]
    public async Task Track_positions_are_zero_based_and_sequential()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Order",
                        ("D:\\1.mp3", null, null, null),
                        ("D:\\2.mp3", null, null, null),
                        ("D:\\3.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\1.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 100 });
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\2.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 101 });
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\3.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 102 });

            ImportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            List<int> positions = _connection.Query<int>("SELECT position FROM playlisttracks WHERE playlistId = @id ORDER BY position", new { id = result.Value.PlaylistId }).ToList();
            Assert.Equal(new[] { 0, 1, 2 }, positions);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "unsupported_extension_returns_fail")]
    public async Task Unsupported_extension_returns_fail()
    {
        // Arrange
        IPlaylistFormatReader? nullReader = null;
        _resolver.Setup(r => r.TryGetReader(It.IsAny<string>(), out nullReader)).Returns(false);
        string path = WritePlaylistFile("ignored", "weird.foo");
        try
        {
            ImportPlaylistCommandHandler sut = new(_resolver.Object, _trackRepository.Object, _connection, NullLogger<ImportPlaylistCommandHandler>.Instance);

            // Act
            Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "parse_error_returns_failed_without_inserting")]
    public async Task Parse_error_returns_failed_without_inserting()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidDataException("garbage"));

            ImportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "cancellation_propagates_and_rolls_back")]
    public async Task Cancellation_propagates_and_rolls_back()
    {
        // Arrange
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new OperationCanceledException());

            ImportPlaylistCommandHandler sut = BuildHandler();

            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act + Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => sut.HandleAsync(new ImportPlaylistCommand(path), cts.Token));
            Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~ImportPlaylistCommandHandlerTests"`
Expected: build fails — `ImportPlaylistCommandHandler` and `ImportPlaylistCommand` missing.

- [ ] **Step 3: Implement the handler**

Create `src/Rok.Application/Features/Playlists/Command/ImportPlaylistCommandHandler.cs`:

```csharp
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using MiF.SimpleMessenger;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Playlists.Messages;
using Rok.Application.Interfaces.Repositories;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Command;

public sealed record ImportPlaylistCommand(string FilePath) : ICommand<Result<PlaylistImportResult>>;

public sealed class ImportPlaylistCommandHandler(
    IPlaylistFormatResolver _resolver,
    ITrackRepository _trackRepository,
    IDbConnection _connection,
    ILogger<ImportPlaylistCommandHandler> _logger)
    : ICommandHandler<ImportPlaylistCommand, Result<PlaylistImportResult>>
{
    private const int NameCollisionHardCap = 999;
    private const string ProbeNameSql = "SELECT 1 FROM playlists WHERE name = @name LIMIT 1";
    private const string InsertHeaderSql =
        "INSERT INTO playlists(name, picture, duration, trackCount, trackMaximum, durationMaximum, groupsJson, type, creatDate) " +
        "VALUES (@Name, '', @Duration, @TrackCount, 0, 0, '', @Type, @CreatDate); SELECT last_insert_rowid();";
    private const string InsertTrackSql =
        "INSERT INTO playlisttracks(playlistId, trackId, position, listened, creatDate) " +
        "VALUES (@PlaylistId, @TrackId, @Position, 0, @CreatDate)";

    public async Task<Result<PlaylistImportResult>> HandleAsync(ImportPlaylistCommand command, CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(command.FilePath);
        if (!_resolver.TryGetReader(extension, out IPlaylistFormatReader? reader) || reader == null)
        {
            _logger.LogWarning("Unsupported playlist format: {Extension}", extension);
            return Result<PlaylistImportResult>.Fail("UnsupportedFormat");
        }

        PlaylistFileModel model;
        try
        {
            await using FileStream fs = File.OpenRead(command.FilePath);
            model = await reader.ReadAsync(fs, Path.GetFileName(command.FilePath), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse playlist {Path}", command.FilePath);
            return Result<PlaylistImportResult>.Fail("ParseError");
        }

        List<(TrackEntity Track, PlaylistFileEntry Entry)> matched = new();
        int ignored = 0;

        foreach (PlaylistFileEntry entry in model.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TrackEntity? track = await _trackRepository.GetByFilePathAsync(entry.FilePath, cancellationToken);
            if (track == null)
            {
                ignored++;
                continue;
            }

            matched.Add((track, entry));
        }

        if (matched.Count == 0)
        {
            _logger.LogInformation("Playlist {Name} skipped: 0 tracks matched, {Ignored} ignored", model.Name, ignored);
            return Result<PlaylistImportResult>.Success(new PlaylistImportResult(PlaylistImportStatus.Skipped, null, null, 0, ignored));
        }

        string? finalName = await ResolveFinalNameAsync(model.Name, cancellationToken);
        if (finalName == null)
        {
            _logger.LogError("Name collision exhausted for {Name}", model.Name);
            return Result<PlaylistImportResult>.Fail("NameCollisionExhausted");
        }

        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        long playlistId;
        using (IDbTransaction transaction = _connection.BeginTransaction())
        {
            try
            {
                long totalDuration = matched.Sum(m => m.Track.Duration);
                DateTime now = DateTime.UtcNow;

                playlistId = await _connection.QuerySingleAsync<long>(new CommandDefinition(
                    InsertHeaderSql,
                    new
                    {
                        Name = finalName,
                        Duration = totalDuration,
                        TrackCount = matched.Count,
                        Type = (int)PlaylistType.Classic,
                        CreatDate = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                for (int i = 0; i < matched.Count; i++)
                {
                    await _connection.ExecuteAsync(new CommandDefinition(
                        InsertTrackSql,
                        new
                        {
                            PlaylistId = playlistId,
                            TrackId = matched[i].Track.Id,
                            Position = i,
                            CreatDate = now
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                transaction.Commit();
            }
            catch (OperationCanceledException)
            {
                transaction.Rollback();
                throw;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to insert playlist {Name}", finalName);
                return Result<PlaylistImportResult>.Fail("DatabaseError");
            }
        }

        Messenger.Send(new PlaylistImportedMessage(playlistId));

        _logger.LogInformation("Imported playlist {Name} (Id={Id}): {Matched} tracks, {Ignored} ignored", finalName, playlistId, matched.Count, ignored);

        return Result<PlaylistImportResult>.Success(new PlaylistImportResult(PlaylistImportStatus.Imported, playlistId, finalName, matched.Count, ignored));
    }

    private async Task<string?> ResolveFinalNameAsync(string baseName, CancellationToken cancellationToken)
    {
        string candidate = baseName;
        for (int suffix = 1; suffix <= NameCollisionHardCap; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int? hit = await _connection.ExecuteScalarAsync<int?>(new CommandDefinition(ProbeNameSql, new { name = candidate }, cancellationToken: cancellationToken));
            if (hit == null)
                return candidate;

            candidate = $"{baseName} ({suffix + 1})";
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~ImportPlaylistCommandHandlerTests"`
Expected: 7 tests pass.

- [ ] **Step 5: Add the remaining import handler tests (collision walk + DB error + message publish)**

Append to `ImportPlaylistCommandHandlerTests.cs`:

```csharp
[Fact(DisplayName = "name_collision_walks_until_free_slot")]
public async Task Name_collision_walks_until_free_slot()
{
    // Arrange — pre-fill "Mix", "Mix (2)", "Mix (3)" so import lands on "Mix (4)"
    DateTime now = DateTime.UtcNow;
    foreach (string n in new[] { "Mix", "Mix (2)", "Mix (3)" })
        _connection.Execute("INSERT INTO playlists(name, type, creatDate) VALUES (@name, 1, @now)", new { name = n, now });

    string path = WritePlaylistFile("dummy");
    try
    {
        _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
        _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });

        ImportPlaylistCommandHandler sut = BuildHandler();

        // Act
        Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Mix (4)", result.Value.FinalName);
    }
    finally
    {
        File.Delete(path);
    }
}

[Fact(DisplayName = "ignored_count_reflects_unmatched_paths")]
public async Task Ignored_count_reflects_unmatched_paths()
{
    // Arrange
    string path = WritePlaylistFile("dummy");
    try
    {
        _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Model("Mix",
                    ("D:\\a.mp3", null, null, null),
                    ("D:\\b.mp3", null, null, null),
                    ("D:\\c.mp3", null, null, null)));
        _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });
        _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\b.mp3", It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);
        _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\c.mp3", It.IsAny<CancellationToken>())).ReturnsAsync((TrackEntity?)null);

        ImportPlaylistCommandHandler sut = BuildHandler();

        // Act
        Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.MatchedCount);
        Assert.Equal(2, result.Value.IgnoredCount);
    }
    finally
    {
        File.Delete(path);
    }
}

[Fact(DisplayName = "skipped_does_not_create_header")]
public async Task Skipped_does_not_create_header()
{
    // Arrange — same as Skipped_when_zero_tracks_match but assert via DB
    string path = WritePlaylistFile("dummy");
    try
    {
        _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Model("Empty"));

        ImportPlaylistCommandHandler sut = BuildHandler();

        // Act
        await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

        // Assert
        Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
    }
    finally
    {
        File.Delete(path);
    }
}

[Fact(DisplayName = "db_error_during_track_insert_rolls_back_header")]
public async Task Db_error_during_track_insert_rolls_back_header()
{
    // Arrange — drop playlisttracks so the inserts fail mid-transaction
    _connection.Execute("DROP TABLE playlisttracks");
    string path = WritePlaylistFile("dummy");
    try
    {
        _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Model("Boom", ("D:\\a.mp3", null, null, null)));
        _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });

        ImportPlaylistCommandHandler sut = BuildHandler();

        // Act
        Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM playlists"));
    }
    finally
    {
        File.Delete(path);
    }
}

[Fact(DisplayName = "publishes_playlist_imported_message_on_success")]
public async Task Publishes_playlist_imported_message_on_success()
{
    // Arrange
    long? receivedId = null;
    void Listener(PlaylistImportedMessage m) => receivedId = m.PlaylistId;
    MiF.SimpleMessenger.Messenger.Subscribe<PlaylistImportedMessage>(Listener);
    try
    {
        string path = WritePlaylistFile("dummy");
        try
        {
            _reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Model("Mix", ("D:\\a.mp3", null, null, null)));
            _trackRepository.Setup(r => r.GetByFilePathAsync("D:\\a.mp3", It.IsAny<CancellationToken>())).ReturnsAsync(new TrackEntity { Id = 1 });

            ImportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result<PlaylistImportResult> result = await sut.HandleAsync(new ImportPlaylistCommand(path), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(result.Value.PlaylistId, receivedId);
        }
        finally
        {
            File.Delete(path);
        }
    }
    finally
    {
        MiF.SimpleMessenger.Messenger.Unsubscribe<PlaylistImportedMessage>(Listener);
    }
}
```

NOTE: confirm the actual `Messenger.Subscribe` / `Unsubscribe` signature on `MiF.SimpleMessenger` matches — adjust the test if the API differs (e.g., if it returns a token instead of using the delegate as the key). One way: open `src/Presentation/ViewModels/Albums/AlbumsViewModel.cs` (or any VM that calls `Messenger.Subscribe`) for the canonical call shape. If the API doesn't allow targeted unsubscribe, replace the unsubscribe with leaving the listener in place — the test still passes since it only verifies that a message was received during the test run.

- [ ] **Step 6: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~ImportPlaylistCommandHandlerTests"`
Expected: 12 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Application/Features/Playlists/Command/ImportPlaylistCommandHandler.cs tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ImportPlaylistCommandHandlerTests.cs
git commit -m "feat(playlists): add ImportPlaylistCommandHandler with transactional creation"
```

---

## Task 9: `ExportPlaylistCommandHandler` with tests

**Files:**
- Create: `src/Rok.Application/Features/Playlists/Command/ExportPlaylistCommandHandler.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ExportPlaylistCommandHandlerTests.cs`

Pipeline (from spec §ExportPlaylistCommandHandler):
1. `mediator.SendMessageAsync(new GetPlaylistByIdQuery(id))` → header (Fail if `Result.Fail`).
2. `mediator.SendMessageAsync(new GetTracksByPlaylistIdQuery(id))` → ordered `IEnumerable<TrackDto>`.
3. Map to `PlaylistFileModel` (Name = header.Name; Entries = `MusicFile`, `Title`, `ArtistName`, `Duration` seconds).
4. Resolve writer; unknown extension → `Result.Fail`.
5. Atomic write: `<final>.tmp`, `WriteAsync`, close, `File.Move(tmp, final, overwrite:true)`. On exception, attempt to delete the tmp and propagate `Result.Fail`.

Constructor dependencies: `IMediator`, `IPlaylistFormatResolver`, `ILogger<ExportPlaylistCommandHandler>`.

- [ ] **Step 1: Write failing tests**

Create `tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ExportPlaylistCommandHandlerTests.cs`:

```csharp
using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;

namespace Rok.ApplicationTests.Features.Playlists;

public class ExportPlaylistCommandHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlaylistFormatResolver> _resolver = new();
    private readonly Mock<IPlaylistFormatWriter> _writer = new();

    private (string TmpDir, string FinalPath) PrepareTempFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return (dir, Path.Combine(dir, "out.m3u8"));
    }

    private ExportPlaylistCommandHandler BuildHandler()
    {
        IPlaylistFormatWriter writer = _writer.Object;
        _resolver.Setup(r => r.TryGetWriter(It.IsAny<string>(), out writer)).Returns(true);
        return new ExportPlaylistCommandHandler(_mediator.Object, _resolver.Object, NullLogger<ExportPlaylistCommandHandler>.Instance);
    }

    [Fact(DisplayName = "writes_classic_playlist_with_all_tracks_in_order")]
    public async Task Writes_classic_playlist_with_all_tracks_in_order()
    {
        // Arrange
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "Mix", Type = 1 }));
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new[]
                     {
                         new TrackDto { Title = "T1", ArtistName = "A1", MusicFile = "D:\\1.mp3", Duration = 100 },
                         new TrackDto { Title = "T2", ArtistName = "A2", MusicFile = "D:\\2.mp3", Duration = 200 }
                     });

            PlaylistFileModel? captured = null;
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()))
                   .Callback<Stream, PlaylistFileModel, CancellationToken>((_, m, _) => captured = m)
                   .Returns(Task.CompletedTask);

            ExportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result result = await sut.HandleAsync(new ExportPlaylistCommand(1, final), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Equal("Mix", captured!.Name);
            Assert.Equal(2, captured.Entries.Count);
            Assert.Equal("D:\\1.mp3", captured.Entries[0].FilePath);
            Assert.Equal(TimeSpan.FromSeconds(100), captured.Entries[0].Duration);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "writes_smart_playlist_using_persisted_tracks_only")]
    public async Task Writes_smart_playlist_using_persisted_tracks_only()
    {
        // Arrange — Smart (Type=0). Handler should NOT regenerate; same query path.
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "Smart", Type = 0 }));
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new[] { new TrackDto { Title = "T", ArtistName = "A", MusicFile = "D:\\s.mp3", Duration = 10 } });
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            ExportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result result = await sut.HandleAsync(new ExportPlaylistCommand(1, final), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GeneratePlaylistTracksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "empty_playlist_writes_only_extm3u_header")]
    public async Task Empty_playlist_writes_only_extm3u_header()
    {
        // Arrange
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "Empty", Type = 1 }));
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Array.Empty<TrackDto>());

            PlaylistFileModel? captured = null;
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()))
                   .Callback<Stream, PlaylistFileModel, CancellationToken>((_, m, _) => captured = m)
                   .Returns(Task.CompletedTask);

            ExportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result result = await sut.HandleAsync(new ExportPlaylistCommand(1, final), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Empty(captured!.Entries);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "playlist_not_found_returns_fail")]
    public async Task Playlist_not_found_returns_fail()
    {
        // Arrange
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Fail("NotFound"));

            ExportPlaylistCommandHandler sut = BuildHandler();

            // Act
            Result result = await sut.HandleAsync(new ExportPlaylistCommand(1, final), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            _writer.Verify(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "unsupported_extension_returns_fail")]
    public async Task Unsupported_extension_returns_fail()
    {
        // Arrange
        (string dir, string final) = PrepareTempFile();
        string weirdPath = Path.Combine(dir, "out.foo");
        try
        {
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "X", Type = 1 }));
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Array.Empty<TrackDto>());
            IPlaylistFormatWriter? noWriter = null;
            _resolver.Setup(r => r.TryGetWriter(It.IsAny<string>(), out noWriter)).Returns(false);

            ExportPlaylistCommandHandler sut = new(_mediator.Object, _resolver.Object, NullLogger<ExportPlaylistCommandHandler>.Instance);

            // Act
            Result result = await sut.HandleAsync(new ExportPlaylistCommand(1, weirdPath), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact(DisplayName = "atomic_write_uses_tmp_then_move")]
    public async Task Atomic_write_uses_tmp_then_move()
    {
        // Arrange — verify that WriteAsync receives a stream pointing at a .tmp file path adjacent to the final path
        (string dir, string final) = PrepareTempFile();
        try
        {
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<PlaylistHeaderDto>.Success(new PlaylistHeaderDto { Id = 1, Name = "X", Type = 1 }));
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Array.Empty<TrackDto>());

            string? tmpSeen = null;
            _writer.Setup(w => w.WriteAsync(It.IsAny<Stream>(), It.IsAny<PlaylistFileModel>(), It.IsAny<CancellationToken>()))
                   .Callback<Stream, PlaylistFileModel, CancellationToken>((s, _, _) =>
                   {
                       if (s is FileStream fs)
                           tmpSeen = fs.Name;
                   })
                   .Returns(Task.CompletedTask);

            ExportPlaylistCommandHandler sut = BuildHandler();

            // Act
            await sut.HandleAsync(new ExportPlaylistCommand(1, final), CancellationToken.None);

            // Assert
            Assert.NotNull(tmpSeen);
            Assert.EndsWith(".tmp", tmpSeen);
            Assert.True(File.Exists(final));
            Assert.False(File.Exists(tmpSeen!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~ExportPlaylistCommandHandlerTests"`
Expected: build fails — `ExportPlaylistCommandHandler` and `ExportPlaylistCommand` missing.

- [ ] **Step 3: Implement the handler**

Create `src/Rok.Application/Features/Playlists/Command/ExportPlaylistCommandHandler.cs`:

```csharp
using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;

namespace Rok.Application.Features.Playlists.Command;

public sealed record ExportPlaylistCommand(long PlaylistId, string FilePath) : ICommand<Result>;

public sealed class ExportPlaylistCommandHandler(
    IMediator _mediator,
    IPlaylistFormatResolver _resolver,
    ILogger<ExportPlaylistCommandHandler> _logger)
    : ICommandHandler<ExportPlaylistCommand, Result>
{
    public async Task<Result> HandleAsync(ExportPlaylistCommand command, CancellationToken cancellationToken)
    {
        Result<PlaylistHeaderDto> headerResult = await _mediator.SendMessageAsync(new GetPlaylistByIdQuery(command.PlaylistId), cancellationToken);
        if (!headerResult.IsSuccess)
        {
            _logger.LogWarning("Export aborted: playlist {Id} not found", command.PlaylistId);
            return Result.Fail("PlaylistNotFound");
        }

        IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetTracksByPlaylistIdQuery(command.PlaylistId), cancellationToken);

        List<PlaylistFileEntry> entries = tracks
            .Select(t => new PlaylistFileEntry(
                t.MusicFile,
                t.Title,
                t.ArtistName,
                t.Duration > 0 ? TimeSpan.FromSeconds(t.Duration) : null))
            .ToList();

        PlaylistFileModel model = new(headerResult.Value.Name, entries);

        string extension = Path.GetExtension(command.FilePath);
        if (!_resolver.TryGetWriter(extension, out IPlaylistFormatWriter? writer) || writer == null)
        {
            _logger.LogWarning("Export aborted: unsupported format {Extension}", extension);
            return Result.Fail("UnsupportedFormat");
        }

        string tempPath = command.FilePath + ".tmp";

        try
        {
            await using (FileStream fs = File.Create(tempPath))
            {
                await writer.WriteAsync(fs, model, cancellationToken);
            }

            File.Move(tempPath, command.FilePath, overwrite: true);
            _logger.LogInformation("Exported playlist {Id} to {Path}", command.PlaylistId, command.FilePath);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            _logger.LogError(ex, "Failed to export playlist {Id} to {Path}", command.PlaylistId, command.FilePath);
            return Result.Fail("WriteError");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~ExportPlaylistCommandHandlerTests"`
Expected: 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Features/Playlists/Command/ExportPlaylistCommandHandler.cs tests/UnitTests/Rok.ApplicationTests/Features/Playlists/ExportPlaylistCommandHandlerTests.cs
git commit -m "feat(playlists): add ExportPlaylistCommandHandler with atomic .tmp write"
```

---

## Task 10: Infrastructure DI registration

**Files:**
- Modify: `src/Rok.Infrastructure/DependencyInjection.cs`

Register the M3U8 reader and writer (each as both their concrete interface and as the format collection) plus the resolver. The handlers in Application get them via constructor injection.

- [ ] **Step 1: Add registrations**

Edit `src/Rok.Infrastructure/DependencyInjection.cs`. Add to the top:

```csharp
using Rok.Application.Features.Playlists.IO;
using Rok.Infrastructure.Playlists;
using Rok.Infrastructure.Playlists.Formats;
```

Inside `AddInfrastructure`, after `services.AddScoped<IListeningEventRepository, ListeningEventRepository>();` (or any clean spot near other repos), add:

```csharp
services.AddSingleton<IPlaylistFormatReader, M3u8PlaylistReader>();
services.AddSingleton<IPlaylistFormatWriter, M3u8PlaylistWriter>();
services.AddSingleton<IPlaylistFormatResolver, PlaylistFormatResolver>();
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 3: Run all tests so far**

Run: `dotnet test /p:Platform=x64`
Expected: all previously-green tests still pass; new tests added in tasks 2-9 pass.

- [ ] **Step 4: Commit**

```bash
git add src/Rok.Infrastructure/DependencyInjection.cs
git commit -m "build(playlists): register M3U8 reader, writer and format resolver"
```

---

## Task 11: `PlaylistImportService` (Presentation) with tests

**Files:**
- Create: `src/Presentation/ViewModels/Playlists/Services/PlaylistImportService.cs`
- Create: `tests/UnitTests/Rok.PresentationTests/ViewModels/Playlists/Services/PlaylistImportServiceTests.cs`

The service owns the FileOpenPicker, the loop calling the import command for each file, and the recap toast. Picker invocation lives behind an interface (`IPlaylistFilePickerService`) so the service is testable without WinUI; the production implementation calls `FileOpenPicker.PickMultipleFilesAsync`. The picker dependency is the only UI seam — everything else is plain async logic.

Toast text patterns (from spec §10):
- Imported only: `"N importée(s) — X piste(s), Y ignorée(s)"`
- + skipped: append `" — Z vide(s) ignorée(s)"`
- + failed: append `" — F en échec"`
- If `imported == 0 && (skipped > 0 || failed > 0)` → `NotificationType.Warning`; else `Success`.

Localization: place final strings in resources later (Task 16). For this task, emit the literal English strings shown above so tests can assert exact content.

- [ ] **Step 1: Create the file picker abstraction (Presentation, tiny interface)**

Create `src/Presentation/ViewModels/Playlists/Services/IPlaylistFilePickerService.cs`:

```csharp
namespace Rok.ViewModels.Playlists.Services;

public interface IPlaylistFilePickerService
{
    Task<IReadOnlyList<string>> PickPlaylistFilesAsync();

    Task<string?> PickSavePathAsync(string suggestedFileName);
}
```

The production implementation calling WinUI pickers (FileOpenPicker / FileSavePicker with `InitializeWithWindow.Initialize(picker, App.MainWindowHandle)`) lands in Task 13.

- [ ] **Step 2: Write failing tests**

Create `tests/UnitTests/Rok.PresentationTests/ViewModels/Playlists/Services/PlaylistImportServiceTests.cs`:

```csharp
using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Messages;
using Rok.ViewModels.Playlists.Services;
using Rok.Shared.Enums;
using MiF.SimpleMessenger;

namespace Rok.PresentationTests.ViewModels.Playlists.Services;

public class PlaylistImportServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlaylistFilePickerService> _picker = new();

    private PlaylistImportService BuildService()
        => new(_mediator.Object, _picker.Object, NullLogger<PlaylistImportService>.Instance);

    private static Result<PlaylistImportResult> Imported(int matched, int ignored)
        => Result<PlaylistImportResult>.Success(new PlaylistImportResult(PlaylistImportStatus.Imported, 1, "Mix", matched, ignored));

    private static Result<PlaylistImportResult> Skipped(int ignored)
        => Result<PlaylistImportResult>.Success(new PlaylistImportResult(PlaylistImportStatus.Skipped, null, null, 0, ignored));

    private static Result<PlaylistImportResult> Failed()
        => Result<PlaylistImportResult>.Fail("ParseError");

    [Fact(DisplayName = "does_not_show_toast_when_user_cancels_picker")]
    public async Task Does_not_show_toast_when_user_cancels_picker()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(Array.Empty<string>());

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.Null(captured);
            _mediator.Verify(m => m.SendMessageAsync(It.IsAny<ImportPlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            Messenger.Unsubscribe<ShowNotificationMessage>(Listen);
        }
    }

    [Fact(DisplayName = "aggregates_counts_across_multiple_files")]
    public async Task Aggregates_counts_across_multiple_files()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8", "b.m3u8" });
            _mediator.SetupSequence(m => m.SendMessageAsync(It.IsAny<ImportPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Imported(matched: 5, ignored: 1))
                     .ReturnsAsync(Imported(matched: 3, ignored: 2));

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Contains("2", captured!.Message); // 2 imported
            Assert.Contains("8", captured.Message);  // 5+3 tracks
            Assert.Contains("3", captured.Message);  // 1+2 ignored
            Assert.Equal(NotificationType.Success, captured.Type);
        }
        finally
        {
            Messenger.Unsubscribe<ShowNotificationMessage>(Listen);
        }
    }

    [Fact(DisplayName = "toast_includes_skipped_count_when_present")]
    public async Task Toast_includes_skipped_count_when_present()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8", "b.m3u8" });
            _mediator.SetupSequence(m => m.SendMessageAsync(It.IsAny<ImportPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Imported(matched: 2, ignored: 0))
                     .ReturnsAsync(Skipped(ignored: 4));

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Contains("vide", captured!.Message);
            Assert.Equal(NotificationType.Success, captured.Type);
        }
        finally
        {
            Messenger.Unsubscribe<ShowNotificationMessage>(Listen);
        }
    }

    [Fact(DisplayName = "toast_includes_failed_count_when_present")]
    public async Task Toast_includes_failed_count_when_present()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8", "b.m3u8" });
            _mediator.SetupSequence(m => m.SendMessageAsync(It.IsAny<ImportPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Imported(matched: 1, ignored: 0))
                     .ReturnsAsync(Failed());

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Contains("échec", captured!.Message);
        }
        finally
        {
            Messenger.Unsubscribe<ShowNotificationMessage>(Listen);
        }
    }

    [Fact(DisplayName = "warns_when_zero_imported_but_skipped_or_failed")]
    public async Task Warns_when_zero_imported_but_skipped_or_failed()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8" });
            _mediator.Setup(m => m.SendMessageAsync(It.IsAny<ImportPlaylistCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Failed());

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(NotificationType.Warning, captured!.Type);
        }
        finally
        {
            Messenger.Unsubscribe<ShowNotificationMessage>(Listen);
        }
    }
}
```

(If `MiF.SimpleMessenger.Messenger` does not expose a matching `Subscribe`/`Unsubscribe` pair, replace the listener wiring with whatever the package exposes — e.g., adapt to a token-based API. Verify against existing usage in `src/Presentation/ViewModels/Albums/AlbumsViewModel.cs` or similar.)

- [ ] **Step 3: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.PresentationTests/Rok.PresentationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlaylistImportServiceTests"`
Expected: fails to build — `PlaylistImportService` is missing.

- [ ] **Step 4: Implement `PlaylistImportService`**

Create `src/Presentation/ViewModels/Playlists/Services/PlaylistImportService.cs`:

```csharp
using MiF.Mediator.Interfaces;
using MiF.SimpleMessenger;
using Microsoft.Extensions.Logging;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Messages;
using Rok.Shared.Enums;

namespace Rok.ViewModels.Playlists.Services;

public sealed class PlaylistImportService(
    IMediator _mediator,
    IPlaylistFilePickerService _picker,
    ILogger<PlaylistImportService> _logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> files = await _picker.PickPlaylistFilesAsync();
        if (files.Count == 0)
            return;

        int imported = 0;
        int tracksTotal = 0;
        int ignoredTotal = 0;
        int skipped = 0;
        int failed = 0;

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Result<PlaylistImportResult> result = await _mediator.SendMessageAsync(new ImportPlaylistCommand(file), cancellationToken);

            if (!result.IsSuccess)
            {
                failed++;
                _logger.LogError("Import failed for {File}: {Error}", file, result.Error);
                continue;
            }

            switch (result.Value.Status)
            {
                case PlaylistImportStatus.Imported:
                    imported++;
                    tracksTotal += result.Value.MatchedCount;
                    ignoredTotal += result.Value.IgnoredCount;
                    break;

                case PlaylistImportStatus.Skipped:
                    skipped++;
                    break;
            }
        }

        Messenger.Send(BuildToast(imported, tracksTotal, ignoredTotal, skipped, failed));
    }

    private static ShowNotificationMessage BuildToast(int imported, int tracks, int ignored, int skipped, int failed)
    {
        string message = $"{imported} importée(s) — {tracks} piste(s), {ignored} ignorée(s)";

        if (skipped > 0)
            message += $" — {skipped} vide(s) ignorée(s)";

        if (failed > 0)
            message += $" — {failed} en échec";

        NotificationType type = (imported == 0 && (skipped > 0 || failed > 0))
            ? NotificationType.Warning
            : NotificationType.Success;

        return new ShowNotificationMessage { Message = message, Type = type };
    }
}
```

- [ ] **Step 5: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.PresentationTests/Rok.PresentationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlaylistImportServiceTests"`
Expected: 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Presentation/ViewModels/Playlists/Services/PlaylistImportService.cs src/Presentation/ViewModels/Playlists/Services/IPlaylistFilePickerService.cs tests/UnitTests/Rok.PresentationTests/ViewModels/Playlists/Services/PlaylistImportServiceTests.cs
git commit -m "feat(playlists): add PlaylistImportService aggregating per-file results into a recap toast"
```

---

## Task 12: `PlaylistExportService` (Presentation) with tests

**Files:**
- Create: `src/Presentation/ViewModels/Playlist/Services/IPlaylistExportPrompts.cs`
- Create: `src/Presentation/ViewModels/Playlist/Services/PlaylistExportService.cs`
- Create: `tests/UnitTests/Rok.PresentationTests/ViewModels/Playlist/Services/PlaylistExportServiceTests.cs`

The export service needs three UI seams (warning dialog, save picker, toast). Toast goes via Messenger as in the rest of the app. Dialog + save picker are abstracted behind one tiny interface so the service is plain async logic.

- [ ] **Step 1: Create the prompt abstraction**

Create `src/Presentation/ViewModels/Playlist/Services/IPlaylistExportPrompts.cs`:

```csharp
namespace Rok.ViewModels.Playlist.Services;

public interface IPlaylistExportPrompts
{
    Task<bool> ConfirmSmartPlaylistExportAsync();

    Task<string?> PickSavePathAsync(string suggestedFileName);
}
```

- [ ] **Step 2: Write failing tests**

Create `tests/UnitTests/Rok.PresentationTests/ViewModels/Playlist/Services/PlaylistExportServiceTests.cs`:

```csharp
using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Command;
using Rok.ViewModels.Playlist.Services;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistExportServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlaylistExportPrompts> _prompts = new();

    private PlaylistExportService BuildService()
        => new(_mediator.Object, _prompts.Object, NullLogger<PlaylistExportService>.Instance);

    [Fact(DisplayName = "shows_warning_dialog_for_smart_playlist_before_picker")]
    public async Task Shows_warning_dialog_for_smart_playlist_before_picker()
    {
        // Arrange
        _prompts.Setup(p => p.ConfirmSmartPlaylistExportAsync()).ReturnsAsync(true);
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync("X:\\out.m3u8");
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<ExportPlaylistCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Smart", Type = 0 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _prompts.Verify(p => p.ConfirmSmartPlaylistExportAsync(), Times.Once);
        _prompts.Verify(p => p.PickSavePathAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "does_not_show_warning_for_classic_playlist")]
    public async Task Does_not_show_warning_for_classic_playlist()
    {
        // Arrange
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync("X:\\out.m3u8");
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<ExportPlaylistCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Classic", Type = 1 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _prompts.Verify(p => p.ConfirmSmartPlaylistExportAsync(), Times.Never);
    }

    [Fact(DisplayName = "does_not_call_handler_when_dialog_cancelled")]
    public async Task Does_not_call_handler_when_dialog_cancelled()
    {
        // Arrange
        _prompts.Setup(p => p.ConfirmSmartPlaylistExportAsync()).ReturnsAsync(false);

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Smart", Type = 0 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _prompts.Verify(p => p.PickSavePathAsync(It.IsAny<string>()), Times.Never);
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<ExportPlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "does_not_call_handler_when_picker_cancelled")]
    public async Task Does_not_call_handler_when_picker_cancelled()
    {
        // Arrange
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Mix", Type = 1 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<ExportPlaylistCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "passes_chosen_path_to_export_command")]
    public async Task Passes_chosen_path_to_export_command()
    {
        // Arrange
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync("X:\\final.m3u8");
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<ExportPlaylistCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        PlaylistHeaderDto playlist = new() { Id = 42, Name = "Mix", Type = 1 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<ExportPlaylistCommand>(c => c.PlaylistId == 42 && c.FilePath == "X:\\final.m3u8"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

- [ ] **Step 3: Run tests — expect compile error**

Run: `dotnet test tests/UnitTests/Rok.PresentationTests/Rok.PresentationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlaylistExportServiceTests"`
Expected: build fails — `PlaylistExportService` missing.

- [ ] **Step 4: Implement `PlaylistExportService`**

Create `src/Presentation/ViewModels/Playlist/Services/PlaylistExportService.cs`:

```csharp
using MiF.Mediator.Interfaces;
using MiF.SimpleMessenger;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Messages;
using Rok.Shared.Enums;

namespace Rok.ViewModels.Playlist.Services;

public sealed class PlaylistExportService(
    IMediator _mediator,
    IPlaylistExportPrompts _prompts,
    ILogger<PlaylistExportService> _logger)
{
    public async Task RunAsync(PlaylistHeaderDto playlist, CancellationToken cancellationToken)
    {
        if (playlist.IsSmart)
        {
            bool proceed = await _prompts.ConfirmSmartPlaylistExportAsync();
            if (!proceed)
                return;
        }

        string? path = await _prompts.PickSavePathAsync($"{playlist.Name}.m3u8");
        if (string.IsNullOrEmpty(path))
            return;

        Result result = await _mediator.SendMessageAsync(new ExportPlaylistCommand(playlist.Id, path), cancellationToken);

        if (result.IsSuccess)
        {
            Messenger.Send(new ShowNotificationMessage { Message = "Playlist exportée", Type = NotificationType.Success });
        }
        else
        {
            _logger.LogError("Export failed for playlist {Id}: {Error}", playlist.Id, result.Error);
            Messenger.Send(new ShowNotificationMessage { Message = "Échec de l'export", Type = NotificationType.Error });
        }
    }
}
```

- [ ] **Step 5: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.PresentationTests/Rok.PresentationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlaylistExportServiceTests"`
Expected: 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Presentation/ViewModels/Playlist/Services/PlaylistExportService.cs src/Presentation/ViewModels/Playlist/Services/IPlaylistExportPrompts.cs tests/UnitTests/Rok.PresentationTests/ViewModels/Playlist/Services/PlaylistExportServiceTests.cs
git commit -m "feat(playlists): add PlaylistExportService with smart-playlist warning dialog"
```

---

## Task 13: WinUI implementations of `IPlaylistFilePickerService` and `IPlaylistExportPrompts`

**Files:**
- Create: `src/Presentation/ViewModels/Playlists/Services/PlaylistFilePickerService.cs`
- Create: `src/Presentation/ViewModels/Playlist/Services/PlaylistExportPrompts.cs`

These are the WinUI-facing implementations. They are not unit-tested (they touch `FileOpenPicker` / `FileSavePicker` / `ContentDialog`); we cover them with the manual smoke run at the end (Task 18).

- [ ] **Step 1: Implement `PlaylistFilePickerService`**

Create `src/Presentation/ViewModels/Playlists/Services/PlaylistFilePickerService.cs`:

```csharp
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Rok.ViewModels.Playlists.Services;

public sealed class PlaylistFilePickerService : IPlaylistFilePickerService
{
    public async Task<IReadOnlyList<string>> PickPlaylistFilesAsync()
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };
        picker.FileTypeFilter.Add(".m3u");
        picker.FileTypeFilter.Add(".m3u8");

        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
        return files.Select(f => f.Path).ToList();
    }

    public async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = ".m3u8"
        };
        picker.FileTypeChoices.Add("M3U8 playlist", new List<string> { ".m3u8" });

        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        StorageFile? file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
```

- [ ] **Step 2: Implement `PlaylistExportPrompts`**

Create `src/Presentation/ViewModels/Playlist/Services/PlaylistExportPrompts.cs`:

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.ViewModels.Playlists.Services;

namespace Rok.ViewModels.Playlist.Services;

public sealed class PlaylistExportPrompts(IPlaylistFilePickerService _picker, ResourceLoader _resourceLoader) : IPlaylistExportPrompts
{
    public async Task<bool> ConfirmSmartPlaylistExportAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = (App.MainWindow as Window)?.Content?.XamlRoot,
            Title = _resourceLoader.GetString("ExportSmartPlaylistTitle"),
            Content = _resourceLoader.GetString("ExportSmartPlaylistMessage"),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public Task<string?> PickSavePathAsync(string suggestedFileName)
        => _picker.PickSavePathAsync(suggestedFileName);
}
```

(If the codebase has a `MainWindow` property somewhere central, use that. Otherwise, the page that calls the service can pass its `XamlRoot` via a constructor parameter or set a static. Confirm by `Grep "App.MainWindow"` — adjust accordingly.)

- [ ] **Step 3: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/ViewModels/Playlists/Services/PlaylistFilePickerService.cs src/Presentation/ViewModels/Playlist/Services/PlaylistExportPrompts.cs
git commit -m "feat(playlists): add WinUI implementations of file pickers and smart-warning dialog"
```

---

## Task 14: `PlaylistImportedMessageHandler` and `PlaylistsViewModel.ImportPlaylistsCommand`

**Files:**
- Create: `src/Presentation/ViewModels/Playlists/Handlers/PlaylistImportedMessageHandler.cs`
- Modify: `src/Presentation/ViewModels/Playlists/PlaylistsViewModel.cs`

The handler exposes a `DataChanged` event so `PlaylistsViewModel` can refresh after a new playlist arrives. Pattern matches `PlaylistUpdateMessageHandler`. The VM gets a new `[RelayCommand] ImportPlaylistsAsync` that delegates to `PlaylistImportService`.

- [ ] **Step 1: Create the handler**

Create `src/Presentation/ViewModels/Playlists/Handlers/PlaylistImportedMessageHandler.cs`:

```csharp
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Messages;
using Rok.ViewModels.Playlists.Services;

namespace Rok.ViewModels.Playlists.Handlers;

public sealed class PlaylistImportedMessageHandler(PlaylistsDataLoader _dataLoader, ILogger<PlaylistImportedMessageHandler> _logger)
{
    public event EventHandler? DataChanged;

    public async Task HandleAsync(PlaylistImportedMessage message)
    {
        PlaylistHeaderDto? playlistDto = await _dataLoader.GetPlaylistByIdAsync(message.PlaylistId);
        if (playlistDto == null)
        {
            _logger.LogWarning("Imported playlist {Id} not found in repository", message.PlaylistId);
            return;
        }

        _dataLoader.AddPlaylist(playlistDto);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

(Confirm `PlaylistsDataLoader` exposes `GetPlaylistByIdAsync` and `AddPlaylist` — they are referenced from `PlaylistUpdateMessageHandler` already.)

- [ ] **Step 2: Modify `PlaylistsViewModel`**

Edit `src/Presentation/ViewModels/Playlists/PlaylistsViewModel.cs`:

1. Add field & constructor parameter for `PlaylistImportService _importService` and `PlaylistImportedMessageHandler _importedHandler`.
2. Subscribe to `PlaylistImportedMessage` in `SubscribeToMessages` and to `_importedHandler.DataChanged` in `SubscribeToEvents`.
3. Add `[RelayCommand] private Task ImportPlaylistsAsync() => _importService.RunAsync(CancellationToken.None);`
4. Unsubscribe in `Dispose`.

Concretely (exact diff):

In the `using` block at the top, ensure:

```csharp
using Rok.Application.Features.Playlists.Messages;
```

Replace the field declaration block:

```csharp
    private readonly PlaylistsDataLoader _dataLoader;
    private readonly PlaylistCreationService _creationService;
    private readonly PlaylistUpdateMessageHandler _updateHandler;
    private readonly IAppOptions _appOptions;
```

with:

```csharp
    private readonly PlaylistsDataLoader _dataLoader;
    private readonly PlaylistCreationService _creationService;
    private readonly PlaylistImportService _importService;
    private readonly PlaylistUpdateMessageHandler _updateHandler;
    private readonly PlaylistImportedMessageHandler _importedHandler;
    private readonly IAppOptions _appOptions;
```

Replace the constructor signature and body:

```csharp
    public PlaylistsViewModel(
        PlaylistsDataLoader dataLoader,
        PlaylistCreationService creationService,
        PlaylistImportService importService,
        PlaylistUpdateMessageHandler updateHandler,
        PlaylistImportedMessageHandler importedHandler,
        IAppOptions appOptions,
        ILogger<PlaylistsViewModel> logger)
    {
        _dataLoader = Guard.Against.Null(dataLoader);
        _creationService = Guard.Against.Null(creationService);
        _importService = Guard.Against.Null(importService);
        _updateHandler = Guard.Against.Null(updateHandler);
        _importedHandler = Guard.Against.Null(importedHandler);
        _appOptions = Guard.Against.Null(appOptions);
        _logger = Guard.Against.Null(logger);

        SubscribeToMessages();
        SubscribeToEvents();
    }
```

Replace `SubscribeToMessages` and `SubscribeToEvents`:

```csharp
    private void SubscribeToMessages()
    {
        Messenger.Subscribe<PlaylistUpdatedMessage>(async (message) => await _updateHandler.HandleAsync(message));
        Messenger.Subscribe<PlaylistImportedMessage>(async (message) => await _importedHandler.HandleAsync(message));
    }

    private void SubscribeToEvents()
    {
        _updateHandler.DataChanged += OnDataChanged;
        _importedHandler.DataChanged += OnDataChanged;
    }
```

Add the relay command, near the other `[RelayCommand]` methods:

```csharp
    [RelayCommand]
    private async Task ImportPlaylistsAsync()
    {
        try
        {
            await _importService.RunAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playlist import failed");
        }
    }
```

Replace the `Dispose(bool)` body:

```csharp
            if (disposing)
            {
                _updateHandler.DataChanged -= OnDataChanged;
                _importedHandler.DataChanged -= OnDataChanged;
                _dataLoader.Clear();
                Playlists.Clear();
            }
```

- [ ] **Step 3: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success — DI is wired in Task 16, but the type compiles.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/ViewModels/Playlists/Handlers/PlaylistImportedMessageHandler.cs src/Presentation/ViewModels/Playlists/PlaylistsViewModel.cs
git commit -m "feat(playlists): wire ImportPlaylistsCommand and PlaylistImportedMessage refresh in PlaylistsViewModel"
```

---

## Task 15: `PlaylistViewModel.ExportPlaylistCommand`

**Files:**
- Modify: `src/Presentation/ViewModels/Playlist/PlaylistViewModel.cs`

Inject `PlaylistExportService` and add `[RelayCommand] private Task ExportPlaylistAsync() => _exportService.RunAsync(Playlist, CancellationToken.None);`.

- [ ] **Step 1: Modify `PlaylistViewModel`**

Edit `src/Presentation/ViewModels/Playlist/PlaylistViewModel.cs`:

1. Add `private readonly PlaylistExportService _exportService;` to the field block.
2. Add `PlaylistExportService exportService` to the constructor parameters and `_exportService = Guard.Against.Null(exportService);` in the body.
3. Add the relay command alongside the existing ones:

```csharp
    [RelayCommand]
    private async Task ExportPlaylistAsync()
    {
        if (Playlist.Id <= 0)
            return;

        try
        {
            await _exportService.RunAsync(Playlist, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playlist export failed for {Id}", Playlist.Id);
        }
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success after Task 16 wires DI; this build will fail until then if anything else depends on the new constructor signature. Verify the only DI consumer of `PlaylistViewModel` is `App.ServiceProvider.GetRequiredService<PlaylistViewModel>()` and the registration happens in `Presentation/DependencyInjection.cs` — both paths get the new service automatically once Task 16 lands.

If the build fails purely due to the constructor change cascading into a test that constructs the VM by hand, update those tests to pass a `Mock<PlaylistExportService>.Object` (or a real instance with mocked deps). Run `Grep "new PlaylistViewModel"` to find any test sites.

- [ ] **Step 3: Commit (with Task 16 if build is broken in isolation)**

If the build is currently green, commit now:

```bash
git add src/Presentation/ViewModels/Playlist/PlaylistViewModel.cs
git commit -m "feat(playlists): add ExportPlaylistCommand to PlaylistViewModel"
```

If the build is broken because DI is not yet wired, defer the commit and bundle with Task 16.

---

## Task 16: Presentation DI registration

**Files:**
- Modify: `src/Presentation/DependencyInjection.cs`

Register `PlaylistImportService`, `PlaylistExportService`, `PlaylistImportedMessageHandler`, `IPlaylistFilePickerService`, `IPlaylistExportPrompts`. Pattern follows existing playlist registrations (singleton for handlers, transient for services that depend on transient VMs).

- [ ] **Step 1: Add registrations**

Edit `src/Presentation/DependencyInjection.cs`. Update the using block:

```csharp
using Rok.ViewModels.Playlist.Services;
using Rok.ViewModels.Playlists.Handlers;
using Rok.ViewModels.Playlists.Services;
```

(verify these are already present; some are — leave as-is if so).

In the `// Playlists ViewModel, services and handlers` block, after `services.AddSingleton<PlaylistUpdateMessageHandler>();`, add:

```csharp
        services.AddSingleton<PlaylistImportedMessageHandler>();
        services.AddSingleton<IPlaylistFilePickerService, PlaylistFilePickerService>();
        services.AddTransient<PlaylistImportService>();
```

In the `// Playlist detail` block, after `services.AddTransient<PlaylistGenerationService>();`, add:

```csharp
        services.AddTransient<IPlaylistExportPrompts, PlaylistExportPrompts>();
        services.AddTransient<PlaylistExportService>();
```

- [ ] **Step 2: Build the full solution**

Run: `dotnet build /p:Platform=x64`
Expected: success. Treat-warnings-as-errors is enabled — fix any unused-import warnings before continuing.

- [ ] **Step 3: Run all tests**

Run: `dotnet test /p:Platform=x64`
Expected: all previously-green tests still pass + every new test from tasks 2-12.

- [ ] **Step 4: Commit (bundle Task 15 if it was deferred)**

```bash
git add src/Presentation/DependencyInjection.cs src/Presentation/ViewModels/Playlist/PlaylistViewModel.cs
git commit -m "build(playlists): register import/export services and message handler"
```

---

## Task 17: XAML — Import button on `PlaylistsPage`, Export menu item, Export button on `PlaylistPage`

**Files:**
- Modify: `src/Presentation/Pages/PlaylistsPage.xaml`
- Modify: `src/Presentation/Pages/PlaylistPage.xaml`
- Modify: `src/Presentation/Strings/en-US/Resources.resw`
- Modify: `src/Presentation/Strings/fr-FR/Resources.resw`

UI elements needed:
- `PlaylistsPage`: Import `AppBarButton` (icon `OpenFile`) in the header `CommandBar`, bound to `ImportPlaylistsCommand`. Per-item Export `MenuFlyoutItem` on the playlist tile context flyout (icon `Save` or `Forward`).
- `PlaylistPage`: Export `AppBarButton` (icon `Save`) in the header `CommandBar`, bound to `ExportPlaylistCommand`.

Resource keys to add (en-US first, then fr-FR):
- `playlistsImport.Label` / `playlistsImport.ToolTipService.ToolTip`
- `playlistsImport.[uid] = "Import…"`
- `playlistExport.Label` / `playlistExport.ToolTipService.ToolTip`
- `playlistFlyoutExport.Text` (right-click menu item)
- `ExportSmartPlaylistTitle`, `ExportSmartPlaylistMessage`

- [ ] **Step 1: Locate the resw files and check existing key style**

Run `Grep "playlistsAdd"` in `src/Presentation/Strings`. Use the same `<data name="playlistsAdd.Label">` / `playlistsAdd.[uid]` shape.

- [ ] **Step 2: Add resource entries (en-US)**

Edit `src/Presentation/Strings/en-US/Resources.resw` and add:

```xml
<data name="playlistsImport.Label" xml:space="preserve">
  <value>Import…</value>
</data>
<data name="playlistsImport.ToolTipService.ToolTip" xml:space="preserve">
  <value>Import playlist files</value>
</data>
<data name="playlistExport.Label" xml:space="preserve">
  <value>Export</value>
</data>
<data name="playlistExport.ToolTipService.ToolTip" xml:space="preserve">
  <value>Export this playlist as M3U8</value>
</data>
<data name="playlistFlyoutExport.Text" xml:space="preserve">
  <value>Export…</value>
</data>
<data name="ExportSmartPlaylistTitle" xml:space="preserve">
  <value>Export smart playlist</value>
</data>
<data name="ExportSmartPlaylistMessage" xml:space="preserve">
  <value>The playlist will be exported as it currently is. Its smart rules will not be preserved in the file.</value>
</data>
```

- [ ] **Step 3: Add resource entries (fr-FR)**

Edit `src/Presentation/Strings/fr-FR/Resources.resw` with the French translations:

```xml
<data name="playlistsImport.Label" xml:space="preserve">
  <value>Importer…</value>
</data>
<data name="playlistsImport.ToolTipService.ToolTip" xml:space="preserve">
  <value>Importer des fichiers de playlist</value>
</data>
<data name="playlistExport.Label" xml:space="preserve">
  <value>Exporter</value>
</data>
<data name="playlistExport.ToolTipService.ToolTip" xml:space="preserve">
  <value>Exporter cette playlist en M3U8</value>
</data>
<data name="playlistFlyoutExport.Text" xml:space="preserve">
  <value>Exporter…</value>
</data>
<data name="ExportSmartPlaylistTitle" xml:space="preserve">
  <value>Exporter une playlist intelligente</value>
</data>
<data name="ExportSmartPlaylistMessage" xml:space="preserve">
  <value>La playlist sera exportée telle quelle. Ses règles intelligentes ne seront pas conservées dans le fichier.</value>
</data>
```

- [ ] **Step 4: Update `PlaylistsPage.xaml`**

Edit `src/Presentation/Pages/PlaylistsPage.xaml` — inside the `CommandBar.Content` `StackPanel`, after the `playlistsSmartAdd` button, add:

```xml
                    <AppBarButton x:Uid="playlistsImport"
                                  Style="{StaticResource AppBarButtonCompactStyle}"
                                  Icon="OpenFile"
                                  Command="{x:Bind ViewModel.ImportPlaylistsCommand}" />
```

To add a per-item context menu with "Export…", wrap each tile (`PlaylistGridTemplate` and `PlaylistListTemplate`) root `Grid` with a `Grid.ContextFlyout`:

```xml
<Grid.ContextFlyout>
    <MenuFlyout>
        <MenuFlyoutItem x:Uid="playlistFlyoutExport"
                        Command="{x:Bind ExportPlaylistCommand}">
            <MenuFlyoutItem.Icon>
                <FontIcon Glyph="&#xE74E;" />
            </MenuFlyoutItem.Icon>
        </MenuFlyoutItem>
    </MenuFlyout>
</Grid.ContextFlyout>
```

Add this block right after the opening `<Grid>` of each template's root grid (before `<Border>`).

- [ ] **Step 5: Update `PlaylistPage.xaml`**

Edit `src/Presentation/Pages/PlaylistPage.xaml`. In the `CommandBar` inside the header `RelativePanel` (around line 40), after the Delete button add:

```xml
                    <AppBarButton x:Uid="playlistExport"
                                  Style="{StaticResource AppBarButtonCompactStyle}"
                                  Icon="Save"
                                  Command="{x:Bind ViewModel.ExportPlaylistCommand}" />
```

- [ ] **Step 6: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success. If XAML compile fails due to a missing `x:Uid`, double-check both resw files were saved and the `.Label` / `.[uid]` style matches the existing convention in the project. Run `Grep "playlistsAdd.Label"` in `src/Presentation/Strings/` to confirm the format.

- [ ] **Step 7: Commit**

```bash
git add src/Presentation/Pages/PlaylistsPage.xaml src/Presentation/Pages/PlaylistPage.xaml src/Presentation/Strings/en-US/Resources.resw src/Presentation/Strings/fr-FR/Resources.resw
git commit -m "feat(playlists): add import button, per-item export menu and export header button"
```

---

## Task 18: Manual smoke test + final test pass

**Files:**
- None (manual run + commit)

This task validates that the wired UI actually works end-to-end. WinUI VMs can't be unit-tested for picker/ContentDialog interactions; a manual run is the verification gate.

- [ ] **Step 1: Final build & full test pass**

Run: `dotnet build /p:Platform=x64 && dotnet test /p:Platform=x64`
Expected: build is warning-free, every test passes.

- [ ] **Step 2: Launch the app**

Run the Presentation project (F5 in Visual Studio, or `dotnet run --project src/Presentation/Rok.csproj /p:Platform=x64`). Make sure `appsettings.json` is in place under `src/Presentation/` (copy from template if missing).

- [ ] **Step 3: Manual export — Classic playlist**

1. Open or create a Classic playlist with at least 3 tracks.
2. On `PlaylistPage`, click the new "Export" header button.
3. Pick a path, save as `.m3u8`.
4. Verify: toast says "Playlist exportée" (Success). Open the file in a text editor: `#EXTM3U` first line, one `#EXTINF:` and one path per track, UTF-8 (no BOM), LF endings.

- [ ] **Step 4: Manual export — Smart playlist**

1. Open a Smart playlist.
2. Click the Export button.
3. Verify: a `ContentDialog` appears with the snapshot warning. Click Cancel — no file should be written. Click again, accept, save — toast says "Playlist exportée".

- [ ] **Step 5: Manual import — single file**

1. On `PlaylistsPage`, click "Importer…".
2. Pick the file you exported in Step 3.
3. Verify: a new playlist with the file's name (suffixed `(2)` if the original Classic still exists) appears in the list immediately. Toast: `1 importée(s) — N piste(s), 0 ignorée(s)`.

- [ ] **Step 6: Manual import — multi-file with mixed outcomes**

1. Pick (a) the file from Step 3, (b) a hand-crafted M3U8 pointing at random nonexistent paths (zero matches → skipped), (c) optionally a corrupt/binary file renamed to `.m3u8` (failed).
2. Verify: a single recap toast like `1 importée(s) — N piste(s), 0 ignorée(s) — 1 vide(s) ignorée(s) — 1 en échec`. Toast type is Warning or Success per the `imported == 0 && (skipped > 0 || failed > 0)` rule.

- [ ] **Step 7: Right-click export from list**

1. On `PlaylistsPage`, right-click a playlist tile → "Exporter…".
2. Verify the same flow as Step 3.

- [ ] **Step 8: Commit nothing (verification only)**

If Steps 3-7 all behave as expected and no new code was needed, the feature is done. If a defect surfaces, write a regression test first (TDD), then fix, then commit.

- [ ] **Step 9: Run pre-push checks locally**

Run: `dotnet format /p:Platform=x64 && dotnet build --no-restore -v quiet /p:Platform=x64 && dotnet test --no-build /p:Platform=x64`
Expected: no formatting changes, build passes, tests pass — pushes will succeed under the husky pre-push hook.

---

## Total task count

18 tasks. ~45 unit tests added (resolver 4, M3U8 writer 7 + 1 roundtrip, M3U8 reader 10, track repo 3, import handler 12, export handler 6, import service 5, export service 5). Manual smoke covers the WinUI seams.

## Conventions to respect throughout

- All identifiers and code in **English**; no comments unless strictly necessary.
- AAA (Arrange / Act / Assert) layout in every test.
- `DisplayName` on `[Fact]` / `[Theory]` is **English snake_case** sentence.
- Test class name = `<TypeUnderTest>Tests`, organized by feature folder.
- Never use collection expressions (`new List<T>()` / `new[] { … }`).
- Braces on their own line; blank lines around conditions / loops / logical blocks; prefer early return.
- `var` only when the type is obvious from the RHS.
- `async`/`await` everywhere; no synchronous I/O.
- Treat-warnings-as-errors is on globally — keep builds warning-free.
- Conventional Commits format on every commit.
