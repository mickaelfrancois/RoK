using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public sealed class FileSystemServiceTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("FileSystemServiceTests_");
    private readonly FileSystemService _service = new(NullLogger<FileSystemService>.Instance);

    public void Dispose() => _tempDir.Delete(recursive: true);

    private static void FillFullPath(string file, TrackFile trackFile) => trackFile.FullPath = file;

    private void CreateFiles(params string[] fileNames)
    {
        foreach (string fileName in fileNames)
            File.WriteAllText(Path.Combine(_tempDir.FullName, fileName), string.Empty);
    }

    [Fact(DisplayName = "get_music_files_should_keep_every_supported_format_and_skip_the_others")]
    public void GetMusicFiles_ShouldKeepSupportedFormats_AndSkipOthers()
    {
        // Arrange
        CreateFiles("a.mp3", "b.flac", "c.m4a", "d.aac", "e.wma", "f.wav", "g.aiff", "h.aif", "i.ogg", "cover.jpg", "notes.txt");

        // Act
        List<TrackFile> files = _service.GetMusicFiles(_tempDir.FullName, FillFullPath);

        // Assert
        string[] expected = ["a.mp3", "b.flac", "c.m4a", "d.aac", "e.wma", "f.wav", "g.aiff", "h.aif"];
        Assert.Equal(expected, files.Select(file => Path.GetFileName(file.FullPath)).Order(StringComparer.Ordinal));
    }

    [Fact(DisplayName = "get_music_files_should_match_extensions_case_insensitively")]
    public void GetMusicFiles_ShouldMatchExtensionsCaseInsensitively()
    {
        // Arrange
        CreateFiles("a.WAV", "b.Aif", "c.M4A");

        // Act
        List<TrackFile> files = _service.GetMusicFiles(_tempDir.FullName, FillFullPath);

        // Assert
        Assert.Equal(3, files.Count);
    }

    [Fact(DisplayName = "get_music_files_should_return_empty_list_when_folder_does_not_exist")]
    public void GetMusicFiles_ShouldReturnEmptyList_WhenFolderDoesNotExist()
    {
        // Arrange
        string missing = Path.Combine(_tempDir.FullName, "missing");

        // Act
        List<TrackFile> files = _service.GetMusicFiles(missing, FillFullPath);

        // Assert
        Assert.Empty(files);
    }

    [Fact(DisplayName = "get_music_files_should_skip_a_file_whose_properties_cannot_be_read")]
    public void GetMusicFiles_ShouldSkipFile_WhenPropertiesCannotBeRead()
    {
        // Arrange
        CreateFiles("broken.m4a", "ok.wav");

        static void FillOrThrow(string file, TrackFile trackFile)
        {
            if (file.EndsWith("broken.m4a", StringComparison.Ordinal))
                throw new InvalidOperationException("unreadable");

            trackFile.FullPath = file;
        }

        // Act
        List<TrackFile> files = _service.GetMusicFiles(_tempDir.FullName, FillOrThrow);

        // Assert
        TrackFile single = Assert.Single(files);
        Assert.Equal("ok.wav", Path.GetFileName(single.FullPath));
    }
}