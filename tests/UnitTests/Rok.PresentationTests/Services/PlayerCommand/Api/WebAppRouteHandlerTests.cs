using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces;
using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class WebAppRouteHandlerTests
{
    private const string Root = @"C:\rok\webapp";
    private const string DefaultRoot = @"C:\rok\default-webapp";

    private readonly Mock<IAppOptions> _options = new();
    private readonly Mock<IFileSystem> _fileSystem = new();

    public WebAppRouteHandlerTests()
    {
        _options.SetupGet(o => o.WebAppRoot).Returns(Root);
        _fileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystem.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
    }

    private WebAppRouteHandler BuildHandler() =>
        new(DefaultRoot, _options.Object, _fileSystem.Object, NullLogger<WebAppRouteHandler>.Instance);

    private void ExistingFiles(params string[] files) =>
        _fileSystem.Setup(f => f.FileExists(It.IsAny<string>()))
                   .Returns((string path) => files.Contains(path, StringComparer.OrdinalIgnoreCase));

    [Theory(DisplayName = "CanHandle should never intercept the API routes")]
    [InlineData("GET", "/api/player/status", false)]
    [InlineData("GET", "/api/playlists", false)]
    [InlineData("POST", "/index.html", false)]
    [InlineData("GET", "/index.html", true)]
    [InlineData("GET", "", true)]
    public void CanHandle_ShouldExcludeApiRoutesAndNonGetRequests(string method, string path, bool expected)
    {
        // Arrange
        WebAppRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "CanHandle should return false when the companion folder is missing")]
    public void CanHandle_ShouldReturnFalse_WhenFolderIsMissing()
    {
        // Arrange
        _fileSystem.Setup(f => f.DirectoryExists(Root)).Returns(false);
        WebAppRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle("GET", "/index.html");

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "CanHandle should fall back to the default folder when no root is configured")]
    public void CanHandle_ShouldUseDefaultRoot_WhenOptionIsEmpty()
    {
        // Arrange
        _options.SetupGet(o => o.WebAppRoot).Returns(string.Empty);
        WebAppRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle("GET", "/index.html");

        // Assert
        Assert.True(result);
        _fileSystem.Verify(f => f.DirectoryExists(DefaultRoot), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should serve the entry point for the root path")]
    public async Task HandleAsync_ShouldServeEntryPoint_ForRootPath()
    {
        // Arrange
        ExistingFiles(Path.Combine(Root, "index.html"));
        WebAppRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync(string.Empty);

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("text/html; charset=utf-8", result.ContentType);
    }

    [Fact(DisplayName = "HandleAsync should serve the WebAssembly runtime with its own media type")]
    public async Task HandleAsync_ShouldServeWasmWithItsMediaType()
    {
        // Arrange
        ExistingFiles(Path.Combine(Root, "_framework", "dotnet.runtime.wasm"));
        WebAppRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/_framework/dotnet.runtime.wasm");

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("application/wasm", result.ContentType);
    }

    [Theory(DisplayName = "HandleAsync should refuse any path escaping the companion folder")]
    [InlineData("/../secret.txt")]
    [InlineData("/../../windows/system32/config.sys")]
    [InlineData("/assets/../../secret.txt")]
    public async Task HandleAsync_ShouldRefuseTraversal(string path)
    {
        // Arrange
        ExistingFiles(@"C:\rok\secret.txt", @"C:\secret.txt");
        WebAppRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync(path);

        // Assert
        Assert.Equal(404, result.StatusCode);
        _fileSystem.Verify(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should refuse a sibling folder sharing the root name prefix")]
    public async Task HandleAsync_ShouldRefuseSiblingFolderWithSamePrefix()
    {
        // Arrange
        ExistingFiles(@"C:\rok\webapp-private\secret.txt");
        WebAppRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/../webapp-private/secret.txt");

        // Assert
        Assert.Equal(404, result.StatusCode);
    }

    [Fact(DisplayName = "HandleAsync should fall back to the entry point for a client-side route")]
    public async Task HandleAsync_ShouldFallBackToEntryPoint_ForClientSideRoute()
    {
        // Arrange
        ExistingFiles(Path.Combine(Root, "index.html"));
        WebAppRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/playlists/42");

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("text/html; charset=utf-8", result.ContentType);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound for a missing file carrying an extension")]
    public async Task HandleAsync_ShouldReturnNotFound_ForMissingAsset()
    {
        // Arrange
        ExistingFiles(Path.Combine(Root, "index.html"));
        WebAppRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/css/missing.css");

        // Assert
        Assert.Equal(404, result.StatusCode);
    }
}