using Dapper;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Options;
using Rok.Application.Services;
using Rok.Domain.Enums;
using Rok.Import;
using Rok.Import.Services;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Integration;

/// <summary>
/// Runs the real library import on folders to check that the origin of the compilation status (tag, manual, derived) drives the rescan.
/// </summary>
public sealed class CompilationSourceRescanTests
{
    [Fact(DisplayName = "import_marks_a_single_artist_album_as_tag_compilation_when_a_file_has_the_flag")]
    public async Task Import_WhenAFileCarriesTheCompilationFlag_ShouldCreateATagCompilation()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("tagged", "tg1", "Tag Owner", "Tagged Album", flag: true);
        harness.AddFile("tagged", "tg2", "Tag Owner", "Tagged Album", flag: false);
        harness.AddFile("plain", "pl1", "Plain Owner", "Plain Album", flag: false);

        // Act
        await harness.ImportAsync();

        // Assert
        long tagged = harness.AlbumIdOf("tg1");
        long plain = harness.AlbumIdOf("pl1");

        Assert.Equal(tagged, harness.AlbumIdOf("tg2"));
        Assert.Equal(1, harness.IsCompilation(tagged));
        Assert.Equal((int)ECompilationSource.Tag, harness.Source(tagged));
        Assert.Equal(0, harness.IsCompilation(plain));
        Assert.Equal((int)ECompilationSource.Derived, harness.Source(plain));
    }

    [Fact(DisplayName = "rescan_keeps_a_manual_compilation_when_the_folder_drops_to_one_artist")]
    public async Task Rescan_WhenAManualCompilationDropsToOneArtist_ShouldStayACompilation()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("manual", "mn1", "Manual Artist A", "Manual Album", flag: false);
        harness.AddFile("manual", "mn2", "Manual Artist B", "Manual Album", flag: false);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf("mn1");

        harness.Execute("UPDATE Albums SET compilationSource = @source WHERE id = @albumId", new { albumId, source = (int)ECompilationSource.Manual });

        // Act
        harness.Retag("mn2", "Manual Artist A", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retagged file must be processed again");
        Assert.Equal("Manual Artist A", harness.ArtistNameOf("mn2"));
        Assert.Equal(albumId, harness.AlbumIdOf("mn2"));
        Assert.Equal(1, harness.IsCompilation(albumId));
        Assert.Equal((int)ECompilationSource.Manual, harness.Source(albumId));
    }

    [Fact(DisplayName = "rescan_drops_a_derived_compilation_when_the_folder_drops_to_one_artist")]
    public async Task Rescan_WhenADerivedCompilationDropsToOneArtist_ShouldStopBeingACompilation()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("derived", "dr1", "Derived Artist A", "Derived Album", flag: false);
        harness.AddFile("derived", "dr2", "Derived Artist B", "Derived Album", flag: false);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf("dr1");

        Assert.Equal(1, harness.IsCompilation(albumId));

        // Act
        harness.Retag("dr2", "Derived Artist A", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retagged file must be processed again");
        Assert.Equal(albumId, harness.AlbumIdOf("dr2"));
        Assert.Equal(0, harness.IsCompilation(albumId));
        Assert.Equal((int)ECompilationSource.Derived, harness.Source(albumId));
    }

    [Fact(DisplayName = "rescan_turns_a_derived_album_into_a_tag_compilation_when_a_file_gains_the_flag")]
    public async Task Rescan_WhenAFileGainsTheFlag_ShouldBecomeATagCompilationWithTheSameId()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("gain", "gn1", "Gain Owner", "Gain Album", flag: false);
        harness.AddFile("gain", "gn2", "Gain Owner", "Gain Album", flag: false);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf("gn1");

        Assert.Equal(0, harness.IsCompilation(albumId));

        // Act
        harness.SetFlag("gn2", true, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the flagged file must be processed again");
        Assert.Equal(albumId, harness.AlbumIdOf("gn1"));
        Assert.Equal(1, harness.IsCompilation(albumId));
        Assert.Equal((int)ECompilationSource.Tag, harness.Source(albumId));
    }

    [Fact(DisplayName = "rescan_falls_back_to_derived_when_the_flag_disappears")]
    public async Task Rescan_WhenTheFlagDisappears_ShouldFallBackToDerivedAndRecompute()
    {
        // Arrange
        using Harness harness = new();

        harness.AddFile("lost", "ls1", "Lost Owner", "Lost Album", flag: true);
        harness.AddFile("lost", "ls2", "Lost Owner", "Lost Album", flag: false);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf("ls1");

        Assert.Equal((int)ECompilationSource.Tag, harness.Source(albumId));

        // Act
        harness.SetFlag("ls1", false, 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the unflagged file must be processed again");
        Assert.Equal(albumId, harness.AlbumIdOf("ls1"));
        Assert.Equal(0, harness.IsCompilation(albumId));
        Assert.Equal((int)ECompilationSource.Derived, harness.Source(albumId));
    }

    [Fact(DisplayName = "set_compilation_writes_the_flag_in_the_files_and_survives_a_rescan")]
    public async Task SetCompilation_WhenCheckedOnASingleArtistAlbum_ShouldWriteTheFlagAndSurviveARescan()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);

        Assert.Equal(0, harness.Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId }));

        // Act
        await harness.SetCompilationAsync(albumId, true);

        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, "Retitled neighbour", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled neighbour must be processed again");

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
            Assert.True(harness.ReadCompilationFlag(file), file);

        Assert.Equal(albumId, harness.AlbumIdOf(AlbumTagWriteHarness.SecondFile));
        Assert.Equal(1, harness.Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId }));
        Assert.Equal((int)ECompilationSource.Manual, harness.Scalar<int>("SELECT compilationSource FROM Albums WHERE id = @albumId", new { albumId }));
        Assert.Equal(0, harness.PendingCount());
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "set_compilation_unchecked_removes_the_flag_from_the_files_and_survives_a_rescan")]
    public async Task SetCompilation_WhenUncheckedOnATagCompilation_ShouldRemoveTheFlagAndSurviveARescan()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
            harness.WriteCompilationFlag(file, true);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);

        Assert.Equal(1, harness.Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId }));

        // Act
        await harness.SetCompilationAsync(albumId, false);

        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, "Retitled neighbour", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled neighbour must be processed again");

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
            Assert.False(harness.ReadCompilationFlag(file), file);

        Assert.Equal(albumId, harness.AlbumIdOf(AlbumTagWriteHarness.SecondFile));
        Assert.Equal(0, harness.Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId }));
        Assert.Equal((int)ECompilationSource.Manual, harness.Scalar<int>("SELECT compilationSource FROM Albums WHERE id = @albumId", new { albumId }));
        Assert.Equal(0, harness.PendingCount());
        harness.AssertCountersMatchTracks();
    }

    [Fact(DisplayName = "set_compilation_interrupted_keeps_manual_status_through_a_rescan_and_resume_writes_the_flag")]
    public async Task SetCompilation_WhenAFileWriteFails_ShouldKeepTheStatusThroughARescanAndResumeShouldWriteTheFlag()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);

        harness.FailWritesTo(AlbumTagWriteHarness.SecondFile);

        // Act
        await harness.SetCompilationAsync(albumId, true);

        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, "Retitled neighbour", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled neighbour must be processed again");
        Assert.True(harness.PendingCount() > 0);
        Assert.False(harness.ReadCompilationFlag(AlbumTagWriteHarness.SecondFile));
        Assert.Equal(1, harness.Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId }));
        Assert.Equal((int)ECompilationSource.Manual, harness.Scalar<int>("SELECT compilationSource FROM Albums WHERE id = @albumId", new { albumId }));

        harness.StopFailingWrites();
        await harness.ResumeAsync(albumId);

        Assert.True(harness.ReadCompilationFlag(AlbumTagWriteHarness.SecondFile));
        Assert.Equal(0, harness.PendingCount());
        Assert.Equal(1, harness.Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId }));
        Assert.Equal((int)ECompilationSource.Manual, harness.Scalar<int>("SELECT compilationSource FROM Albums WHERE id = @albumId", new { albumId }));
    }

    [Fact(DisplayName = "set_compilation_with_tag_writing_disabled_changes_only_the_database_and_survives_a_rescan")]
    public async Task SetCompilation_WhenTagWritingIsDisabled_ShouldChangeOnlyTheDatabaseAndSurviveARescan()
    {
        // Arrange
        using AlbumTagWriteHarness harness = new(AlbumTagWriteHarness.CreateFlac);

        await harness.ImportTwiceAsync();

        long albumId = harness.AlbumIdOf(AlbumTagWriteHarness.FirstFile);

        harness.SetWriteTagsEnabled(false);

        // Act
        await harness.SetCompilationAsync(albumId, true);

        harness.Retitle(AlbumTagWriteHarness.NeighbourFile, "Retitled neighbour", 2);
        ImportStatisticsDto rescan = await harness.ImportAsync();

        // Assert
        Assert.True(rescan.TracksUpdated >= 1, "positive control: the retitled neighbour must be processed again");

        foreach (string file in AlbumTagWriteHarness.AlbumFiles)
            Assert.False(harness.ReadCompilationFlag(file), file);

        Assert.Equal(0, harness.PendingCount());
        Assert.Equal(albumId, harness.AlbumIdOf(AlbumTagWriteHarness.SecondFile));
        Assert.Equal(1, harness.Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId }));
        Assert.Equal((int)ECompilationSource.Manual, harness.Scalar<int>("SELECT compilationSource FROM Albums WHERE id = @albumId", new { albumId }));
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteDatabaseFixture _fixture = new();
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("CompilationSourceRescanTests_");
        private readonly TrackRepository _trackRepository;
        private readonly AlbumRepository _albumRepository;
        private readonly ArtistRepository _artistRepository;
        private readonly GenreRepository _genreRepository;
        private readonly TagService _tagService = new(NullLogger<TagService>.Instance);
        private readonly LibraryWriteGate _writeGate = new();
        private readonly AppOptions _options = new() { LibraryTokens = ["lib"], WriteTagsEnabled = true };
        private readonly Dictionary<string, string> _paths = [];

        public Harness()
        {
            _fixture.Connection.Execute("DELETE FROM Tracks; DELETE FROM Albums; DELETE FROM Artists; DELETE FROM Genres;");

            _options.SetCachePath(Path.Combine(_root.FullName, "cache"));

            _trackRepository = new TrackRepository(_fixture.Connection, _fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);
            _albumRepository = new AlbumRepository(_fixture.Connection, _fixture.Connection, NullLogger<AlbumRepository>.Instance, TimeProvider.System);
            _artistRepository = new ArtistRepository(_fixture.Connection, _fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);
            _genreRepository = new GenreRepository(_fixture.Connection, _fixture.Connection, NullLogger<GenreRepository>.Instance, TimeProvider.System);
        }

        private string LibraryFolder => Path.Combine(_root.FullName, "library");

        public T Scalar<T>(string sql, object? args = null)
        {
            return _fixture.Connection.ExecuteScalar<T>(sql, args)!;
        }

        public void Execute(string sql, object? args = null)
        {
            _fixture.Connection.Execute(sql, args);
        }

        public void AddFile(string folderName, string name, string artist, string album, bool flag)
        {
            string path = FlacTagFixture.CreateWithArtists(Path.Combine(LibraryFolder, folderName), [], [artist], name + ".flac");

            using (TagLib.File file = TagLib.File.Create(path))
            {
                file.Tag.Title = "Title " + name;
                file.Tag.Album = album;
                file.Tag.Genres = ["Rock"];

                file.Save();
            }

            _paths[name] = path;

            if (flag)
                WriteFlag(name, true);
        }

        public void Retag(string name, string artist, int days)
        {
            using (TagLib.File file = TagLib.File.Create(_paths[name]))
            {
                file.Tag.Performers = [artist];
                file.Save();
            }

            Touch(name, days);
        }

        public void SetFlag(string name, bool flag, int days)
        {
            WriteFlag(name, flag);

            using (TagLib.File file = TagLib.File.Create(_paths[name]))
            {
                file.Tag.Title = "Flagged " + name;
                file.Save();
            }

            Touch(name, days);
        }

        public long AlbumIdOf(string name)
        {
            long trackId = Scalar<long>("SELECT id FROM Tracks WHERE musicFile LIKE @pattern", new { pattern = $"%{name}.flac" });

            return Scalar<long>("SELECT albumId FROM Tracks WHERE id = @trackId", new { trackId });
        }

        public string ArtistNameOf(string name)
        {
            return Scalar<string>("SELECT a.name FROM Tracks t JOIN Artists a ON a.id = t.artistId WHERE t.musicFile LIKE @pattern", new { pattern = $"%{name}.flac" });
        }

        public int IsCompilation(long albumId)
        {
            return Scalar<int>("SELECT isCompilation FROM Albums WHERE id = @albumId", new { albumId });
        }

        public int Source(long albumId)
        {
            return Scalar<int>("SELECT compilationSource FROM Albums WHERE id = @albumId", new { albumId });
        }

        public async Task ImportTwiceAsync()
        {
            await ImportAsync();
            await ImportAsync();
        }

        public async Task<ImportStatisticsDto> ImportAsync()
        {
            Mock<IFolderResolver> folderResolver = new();
            folderResolver.Setup(r => r.GetPathFromTokenAsync("lib")).ReturnsAsync([LibraryFolder]);

            ImportService import = CreateImportService(folderResolver.Object);

            await import.ImportAsync(CancellationToken.None);

            return import.Statistics;
        }

        public void Dispose()
        {
            _fixture.Dispose();

            try
            {
                _root.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private void WriteFlag(string name, bool flag)
        {
            using (TagLib.File file = TagLib.File.Create(_paths[name]))
            {
                TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);

                xiph.IsCompilation = flag;

                file.Save();
            }
        }

        private void Touch(string name, int days)
        {
            File.SetLastWriteTimeUtc(_paths[name], DateTime.UtcNow.AddDays(days));
        }

        private ImportService CreateImportService(IFolderResolver folderResolver)
        {
            TimeProvider time = TimeProvider.System;
            Mock<IMessenger> messenger = new();
            Mock<IMediator> mediator = new();
            Mock<ILyricsService> lyrics = new();
            Mock<ITelemetryClient> telemetry = new();

            TrackImport trackImport = new(_trackRepository);
            AlbumImport albumImport = new(_albumRepository, time);
            ArtistImport artistImport = new(_artistRepository, time);
            GenreImport genreImport = new(_genreRepository, time);
            ImportTrackingService tracking = new();
            ImportMessageThrottler throttler = new();
            FileSystemService fileSystem = new(NullLogger<FileSystemService>.Instance);

            FolderImportProcessor folderProcessor = new(
                _options,
                trackImport,
                artistImport,
                albumImport,
                genreImport,
                fileSystem,
                new TrackFileProcessor(_tagService, NullLogger<TrackFileProcessor>.Instance),
                new TrackMetadataService(trackImport, NullLogger<TrackMetadataService>.Instance, time),
                new EmbeddedLyricsImporter(lyrics.Object, NullLogger<EmbeddedLyricsImporter>.Instance),
                tracking,
                _tagService,
                throttler,
                messenger.Object,
                NullLogger<FolderImportProcessor>.Instance);

            PostImportDominantColorTask dominantColor = new(
                Mock.Of<IDominantColorCalculator>(),
                _albumRepository,
                Mock.Of<IAlbumPictureService>(),
                _artistRepository,
                Mock.Of<IArtistPictureService>(),
                NullLogger<PostImportDominantColorTask>.Instance);

            PostImportApiEnrichmentTask apiEnrichment = new(
                artistImport,
                albumImport,
                Mock.Of<IArtistApiService>(),
                Mock.Of<IAlbumApiService>(),
                Mock.Of<IArtistPictureService>(),
                Mock.Of<IAlbumPictureService>(),
                Mock.Of<IBackdropPicture>(),
                mediator.Object,
                NullLogger<PostImportApiEnrichmentTask>.Instance);

            return new ImportService(
                folderResolver,
                _options,
                new Statistics(_trackRepository, _albumRepository, _artistRepository, _genreRepository, NullLogger<Statistics>.Instance),
                trackImport,
                genreImport,
                albumImport,
                artistImport,
                new CleanLibraryService(_trackRepository, _artistRepository, _albumRepository, _genreRepository, NullLogger<CleanLibraryService>.Instance),
                telemetry.Object,
                new ImportProgressService(messenger.Object),
                tracking,
                fileSystem,
                folderProcessor,
                dominantColor,
                apiEnrichment,
                throttler,
                _writeGate,
                NullLogger<ImportService>.Instance);
        }
    }
}