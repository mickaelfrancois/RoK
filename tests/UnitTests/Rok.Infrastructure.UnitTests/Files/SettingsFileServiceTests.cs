using System.Text.Json;
using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using Rok.Application.Player;
using Rok.Infrastructure.Files;
using Rok.Shared.Enums;

namespace Rok.Infrastructure.UnitTests.Files;

public class SettingsFileServiceTests
{
    private const string ApplicationPath = @"C:\App";
    private const string SettingsFilePath = @"C:\App\settings.json";

    private static Mock<IFileSystem> CreateFileSystemMock()
    {
        Mock<IFileSystem> mock = new(MockBehavior.Strict);
        mock.Setup(fs => fs.Combine(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((path1, path2) => Path.Combine(path1, path2));
        return mock;
    }

    private static Mock<IFolderResolver> CreateFolderResolverMock()
    {
        return new Mock<IFolderResolver>(MockBehavior.Strict);
    }

    [Fact]
    public void Exists_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(true);
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        bool result = sut.Exists();

        // Assert
        Assert.True(result);
        fs.Verify(f => f.FileExists(SettingsFilePath), Times.Once);
    }

    [Fact]
    public void Exists_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(false);
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        bool result = sut.Exists();

        // Assert
        Assert.False(result);
        fs.Verify(f => f.FileExists(SettingsFilePath), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(false);
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        IAppOptions? result = await sut.LoadAsync<AppOptions>();

        // Assert
        Assert.Null(result);
        fs.Verify(f => f.FileExists(SettingsFilePath), Times.Once);
        fs.Verify(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_WhenFileExists_DeserializesAndReturnsOptions()
    {
        // Arrange
        AppOptions expectedOptions = new()
        {
            Id = Guid.NewGuid(),
            Theme = AppTheme.Dark,
            CachePath = @"C:\Cache",
            LibraryTokens = ["token1", "token2"],
            TelemetryEnabled = false
        };

        string json = JsonSerializer.Serialize(expectedOptions, new JsonSerializerOptions { WriteIndented = true });

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(SettingsFilePath, It.IsAny<CancellationToken>())).ReturnsAsync(json);
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        IAppOptions? result = await sut.LoadAsync<AppOptions>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedOptions.Id, result!.Id);
        Assert.Equal(expectedOptions.Theme, result.Theme);
        Assert.Equal(expectedOptions.CachePath, result.CachePath);
        Assert.Equal(expectedOptions.LibraryTokens.Count, result.LibraryTokens.Count);
        Assert.Equal(expectedOptions.TelemetryEnabled, result.TelemetryEnabled);
    }

    [Fact]
    public async Task LoadAsync_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        string invalidJson = "{ invalid json }";

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(SettingsFilePath, It.IsAny<CancellationToken>())).ReturnsAsync(invalidJson);
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        IAppOptions? result = await sut.LoadAsync<AppOptions>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_WithValidOptions_SerializesAndWritesToFile()
    {
        // Arrange
        AppOptions options = new()
        {
            Id = Guid.NewGuid(),
            Theme = AppTheme.Light,
            CachePath = @"C:\Cache",
            LibraryTokens = ["token1", "token2"]
        };

        string? capturedJson = null;

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.WriteAllTextAsync(SettingsFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, json, _) => capturedJson = json)
            .Returns(Task.CompletedTask);
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.SaveAsync(options);

        // Assert
        fs.Verify(f => f.WriteAllTextAsync(SettingsFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(capturedJson);
        Assert.Contains(options.Id.ToString(), capturedJson);
        Assert.Contains("\"Theme\"", capturedJson);
        Assert.Contains("\"LibraryTokens\"", capturedJson);
    }

    [Fact(DisplayName = "total_tracks_listened_survives_save_load_and_copy")]
    public async Task TotalTracksListened_SurvivesSaveLoadAndCopy()
    {
        // Arrange
        AppOptions options = new() { TotalTracksListened = 37 };
        string? capturedJson = null;

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.WriteAllTextAsync(SettingsFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, json, _) => capturedJson = json)
            .Returns(Task.CompletedTask);
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(SettingsFilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedJson!);
        SettingsFileService sut = new(ApplicationPath, CreateFolderResolverMock().Object, fs.Object);

        // Act
        await sut.SaveAsync(options);
        IAppOptions? loaded = await sut.LoadAsync<AppOptions>();
        AppOptions restored = new();
        restored.CopyFrom(loaded!);

        // Assert
        Assert.Equal(37, restored.TotalTracksListened);
    }

    [Fact(DisplayName = "replay_gain_options_survive_save_load_and_copy")]
    public async Task ReplayGainOptions_SurviveSaveLoadAndCopy()
    {
        // Arrange
        AppOptions options = new() { ReplayGainMode = EReplayGainMode.Album, ReplayGainPreampDb = -2.5 };
        string? capturedJson = null;

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.WriteAllTextAsync(SettingsFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, json, _) => capturedJson = json)
            .Returns(Task.CompletedTask);
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(SettingsFilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedJson!);
        SettingsFileService sut = new(ApplicationPath, CreateFolderResolverMock().Object, fs.Object);

        // Act
        await sut.SaveAsync(options);
        IAppOptions? loaded = await sut.LoadAsync<AppOptions>();
        AppOptions restored = new();
        restored.CopyFrom(loaded!);

        // Assert
        Assert.Equal(EReplayGainMode.Album, restored.ReplayGainMode);
        Assert.Equal(-2.5, restored.ReplayGainPreampDb);
    }

    [Fact(DisplayName = "restored_options_survive_save_load_and_copy")]
    public async Task RestoredOptions_SurviveSaveLoadAndCopy()
    {
        // Arrange
        AppOptions options = new()
        {
            CrossFade = false,
            IsGridView = false,
            AlbumsFilterByGenresId = [12, 34],
            ArtistsFilterByTags = ["live"]
        };
        string? capturedJson = null;

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.WriteAllTextAsync(SettingsFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, json, _) => capturedJson = json)
            .Returns(Task.CompletedTask);
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(SettingsFilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedJson!);
        SettingsFileService sut = new(ApplicationPath, CreateFolderResolverMock().Object, fs.Object);

        // Act
        await sut.SaveAsync(options);
        IAppOptions? loaded = await sut.LoadAsync<AppOptions>();
        AppOptions restored = new();
        restored.CopyFrom(loaded!);

        // Assert
        Assert.False(restored.CrossFade);
        Assert.False(restored.IsGridView);
        Assert.Equal([12L, 34L], restored.AlbumsFilterByGenresId);
        Assert.Equal(["live"], restored.ArtistsFilterByTags);
    }

    [Fact(DisplayName = "legacy_settings_without_new_keys_keep_defaults")]
    public async Task LegacySettings_WithoutNewKeys_KeepDefaults()
    {
        // Arrange
        const string legacyJson = """{ "Theme": 1, "PauseOnCall": false }""";

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(SettingsFilePath)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(SettingsFilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(legacyJson);
        SettingsFileService sut = new(ApplicationPath, CreateFolderResolverMock().Object, fs.Object);

        // Act
        IAppOptions? loaded = await sut.LoadAsync<AppOptions>();
        AppOptions restored = new();
        restored.CopyFrom(loaded!);

        // Assert
        Assert.False(restored.PauseOnCall);
        Assert.True(restored.CrossFade);
        Assert.NotNull(restored.AlbumsFilterByGenresId);
        Assert.Empty(restored.AlbumsFilterByGenresId);
    }

    [Fact]
    public Task SaveAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act & Assert
        return Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }

    [Fact]
    public async Task SaveAsync_FormatsJsonWithIndentation()
    {
        // Arrange
        AppOptions options = new()
        {
            Id = Guid.NewGuid(),
            Theme = AppTheme.Dark
        };

        string? capturedJson = null;

        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.WriteAllTextAsync(SettingsFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, json, _) => capturedJson = json)
            .Returns(Task.CompletedTask);
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.SaveAsync(options);

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("\n", capturedJson);
        Assert.Contains("  ", capturedJson);
    }

    [Fact]
    public Task RemoveInvalidLibraryTokensAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act & Assert
        return Assert.ThrowsAsync<ArgumentNullException>(() => sut.RemoveInvalidLibraryTokensAsync(null!));
    }

    [Fact]
    public async Task RemoveInvalidLibraryTokensAsync_WithNullLibraryTokens_DoesNothing()
    {
        // Arrange
        AppOptions options = new() { LibraryTokens = null! };

        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.RemoveInvalidLibraryTokensAsync(options);

        // Assert
        resolver.Verify(r => r.GetDisplayNameFromTokenAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveInvalidLibraryTokensAsync_WithEmptyLibraryTokens_DoesNothing()
    {
        // Arrange
        AppOptions options = new() { LibraryTokens = [] };

        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.RemoveInvalidLibraryTokensAsync(options);

        // Assert
        resolver.Verify(r => r.GetDisplayNameFromTokenAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveInvalidLibraryTokensAsync_WithAllValidTokens_DoesNotRemoveAny()
    {
        // Arrange
        AppOptions options = new() { LibraryTokens = ["token1", "token2", "token3"] };

        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("token1")).ReturnsAsync(@"C:\Music");
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("token2")).ReturnsAsync(@"D:\Audio");
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("token3")).ReturnsAsync(@"E:\Songs");
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.RemoveInvalidLibraryTokensAsync(options);

        // Assert
        Assert.Equal(3, options.LibraryTokens.Count);
        Assert.Contains("token1", options.LibraryTokens);
        Assert.Contains("token2", options.LibraryTokens);
        Assert.Contains("token3", options.LibraryTokens);
    }

    [Fact]
    public async Task RemoveInvalidLibraryTokensAsync_WithSomeInvalidTokens_RemovesOnlyInvalid()
    {
        // Arrange
        AppOptions options = new() { LibraryTokens = ["validToken1", "invalidToken", "validToken2"] };

        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("validToken1")).ReturnsAsync(@"C:\Music");
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("invalidToken")).ReturnsAsync((string?)null);
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("validToken2")).ReturnsAsync(@"D:\Audio");
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.RemoveInvalidLibraryTokensAsync(options);

        // Assert
        Assert.Equal(2, options.LibraryTokens.Count);
        Assert.Contains("validToken1", options.LibraryTokens);
        Assert.Contains("validToken2", options.LibraryTokens);
        Assert.DoesNotContain("invalidToken", options.LibraryTokens);
    }

    [Fact]
    public async Task RemoveInvalidLibraryTokensAsync_WithAllInvalidTokens_RemovesAll()
    {
        // Arrange
        AppOptions options = new() { LibraryTokens = ["invalid1", "invalid2", "invalid3"] };

        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.RemoveInvalidLibraryTokensAsync(options);

        // Assert
        Assert.Empty(options.LibraryTokens);
    }

    [Fact]
    public async Task RemoveInvalidLibraryTokensAsync_CallsResolverForEachToken()
    {
        // Arrange
        AppOptions options = new() { LibraryTokens = ["token1", "token2"] };

        Mock<IFileSystem> fs = CreateFileSystemMock();
        Mock<IFolderResolver> resolver = CreateFolderResolverMock();
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("token1")).ReturnsAsync(@"C:\Music");
        resolver.Setup(r => r.GetDisplayNameFromTokenAsync("token2")).ReturnsAsync(@"D:\Audio");
        SettingsFileService sut = new(ApplicationPath, resolver.Object, fs.Object);

        // Act
        await sut.RemoveInvalidLibraryTokensAsync(options);

        // Assert
        resolver.Verify(r => r.GetDisplayNameFromTokenAsync("token1"), Times.Once);
        resolver.Verify(r => r.GetDisplayNameFromTokenAsync("token2"), Times.Once);
    }
}