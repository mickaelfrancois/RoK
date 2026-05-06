using Rok.ViewModels.Start;

namespace Rok.PresentationTests.ViewModels.Start;

public class FolderValidatorTests : IDisposable
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
}
