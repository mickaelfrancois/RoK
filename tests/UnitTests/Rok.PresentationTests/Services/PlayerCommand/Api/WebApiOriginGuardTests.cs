using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class WebApiOriginGuardTests
{
    [Theory(DisplayName = "IsStateChanging should cover every non-GET route and the legacy GET commands")]
    [InlineData("POST", "/api/player/next", true)]
    [InlineData("POST", "/api/tracks/1/score/5", true)]
    [InlineData("DELETE", "/api/playlists/1", true)]
    [InlineData("GET", "/next", true)]
    [InlineData("GET", "/toggle", true)]
    [InlineData("GET", "/volume/80", true)]
    [InlineData("GET", "/listen/playlist/MyMix", true)]
    [InlineData("GET", "/api/player/status", false)]
    [InlineData("GET", "/api/playlists", false)]
    [InlineData("GET", "/status", false)]
    [InlineData("GET", "/queue", false)]
    [InlineData("GET", "/current/album-cover", false)]
    [InlineData("GET", "/index.html", false)]
    public void IsStateChanging_ShouldFlagMutatingRoutes(string method, string path, bool expected)
    {
        // Arrange & Act
        bool result = WebApiOriginGuard.IsStateChanging(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "IsCrossSite should refuse a public origin and accept a local one")]
    [InlineData("https://evil.example", true)]
    [InlineData("http://evil.example:5075", true)]
    [InlineData("http://172.32.0.1", true)]
    [InlineData("http://8.8.8.8", true)]
    [InlineData("not-an-origin", true)]
    [InlineData("http://localhost:5075", false)]
    [InlineData("http://127.0.0.1:5001", false)]
    [InlineData("http://192.168.1.20:5075", false)]
    [InlineData("http://10.0.0.5", false)]
    [InlineData("http://172.16.0.1", false)]
    public void IsCrossSite_ShouldJudgeTheOrigin(string origin, bool expected)
    {
        // Arrange & Act
        bool result = WebApiOriginGuard.IsCrossSite(origin, secFetchSite: null);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "IsCrossSite should fall back to Sec-Fetch-Site when no origin is sent")]
    [InlineData("cross-site", true)]
    [InlineData("Cross-Site", true)]
    [InlineData("same-site", false)]
    [InlineData("same-origin", false)]
    [InlineData("none", false)]
    [InlineData(null, false)]
    public void IsCrossSite_ShouldFallBackToSecFetchSite(string? secFetchSite, bool expected)
    {
        // Arrange & Act
        bool result = WebApiOriginGuard.IsCrossSite(origin: null, secFetchSite);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "IsCrossSite should trust a scripted call carrying neither header")]
    public void IsCrossSite_ShouldTrustScriptedCall()
    {
        // Arrange & Act
        bool result = WebApiOriginGuard.IsCrossSite(origin: string.Empty, secFetchSite: null);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "IsCrossSite should let the origin decide even when Sec-Fetch-Site disagrees")]
    public void IsCrossSite_ShouldPreferTheOrigin()
    {
        // Arrange & Act
        bool devServer = WebApiOriginGuard.IsCrossSite("http://localhost:5001", "cross-site");
        bool attacker = WebApiOriginGuard.IsCrossSite("https://evil.example", "same-origin");

        // Assert
        Assert.False(devServer);
        Assert.True(attacker);
    }
}