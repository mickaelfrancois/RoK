using Rok.ViewModels.Start;

namespace Rok.PresentationTests.ViewModels.Start;

public sealed class FolderValidatorTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("FolderValidatorTests_");

    public void Dispose() => _tempDir.Delete(recursive: true);


    [Fact(DisplayName = "when_folder_has_mp3_files_returns_valid")]
    public async Task ValidateAsync_ReturnsValid_WhenFolderContainsMp3()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "track.mp3"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.Valid, result);
    }

    [Fact(DisplayName = "when_folder_has_flac_files_returns_valid")]
    public async Task ValidateAsync_ReturnsValid_WhenFolderContainsFlac()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "track.flac"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.Valid, result);
    }

    [Fact(DisplayName = "when_folder_has_only_unsupported_files_returns_no_audio_files")]
    public async Task ValidateAsync_ReturnsNoAudioFiles_WhenFolderContainsOnlyUnsupportedFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "image.jpg"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "doc.pdf"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.NoAudioFiles, result);
    }

    [Fact(DisplayName = "when_folder_is_empty_returns_no_audio_files")]
    public async Task ValidateAsync_ReturnsNoAudioFiles_WhenFolderIsEmpty()
    {
        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.NoAudioFiles, result);
    }

    [Fact(DisplayName = "when_audio_file_is_in_subdirectory_returns_valid")]
    public async Task ValidateAsync_ReturnsValid_WhenAudioFileIsInSubdirectory()
    {
        string sub = Path.Combine(_tempDir.FullName, "Artist", "Album");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "track.mp3"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.Valid, result);
    }

    [Fact(DisplayName = "scan_should_count_unsupported_extensions_when_folder_has_only_m4a")]
    public async Task ScanAsync_CountsUnsupportedExtensions_WhenFolderHasOnlyM4a()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
            await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, $"track{i}.m4a"), string.Empty);

        // Act
        FolderScanResult result = await FolderValidator.ScanAsync(_tempDir.FullName);

        // Assert
        Assert.Equal(FolderValidationResult.NoAudioFiles, result.Status);
        Assert.Equal(3, result.UnsupportedCounts[".m4a"]);
        Assert.Single(result.UnsupportedCounts);
    }

    [Fact(DisplayName = "scan_should_count_extensions_case_insensitively_across_subdirectories")]
    public async Task ScanAsync_CountsExtensionsCaseInsensitively_AcrossSubdirectories()
    {
        // Arrange
        string sub = Path.Combine(_tempDir.FullName, "Artist", "Album");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "track.M4A"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(sub, "track.ogg"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(sub, "cover.jpg"), string.Empty);

        // Act
        FolderScanResult result = await FolderValidator.ScanAsync(_tempDir.FullName);

        // Assert
        Assert.Equal(FolderValidationResult.NoAudioFiles, result.Status);
        Assert.Equal(1, result.UnsupportedCounts[".m4a"]);
        Assert.Equal(1, result.UnsupportedCounts[".ogg"]);
        Assert.Equal(2, result.UnsupportedCounts.Count);
    }

    [Fact(DisplayName = "scan_should_return_valid_with_empty_counts_when_mp3_present")]
    public async Task ScanAsync_ReturnsValidWithEmptyCounts_WhenMp3Present()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "track.m4a"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "track.mp3"), string.Empty);

        // Act
        FolderScanResult result = await FolderValidator.ScanAsync(_tempDir.FullName);

        // Assert
        Assert.Equal(FolderValidationResult.Valid, result.Status);
        Assert.Empty(result.UnsupportedCounts);
    }

    [Fact(DisplayName = "scan_should_return_access_denied_when_folder_does_not_exist")]
    public async Task ScanAsync_ReturnsAccessDenied_WhenFolderDoesNotExist()
    {
        // Arrange
        string missing = Path.Combine(_tempDir.FullName, "missing");

        // Act
        FolderScanResult result = await FolderValidator.ScanAsync(missing);

        // Assert
        Assert.Equal(FolderValidationResult.AccessDenied, result.Status);
        Assert.Empty(result.UnsupportedCounts);
    }
}