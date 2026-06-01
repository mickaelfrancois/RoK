using System.Runtime.InteropServices;
using Rok.Infrastructure.Telemetry;

namespace Rok.Infrastructure.UnitTests.Telemetry;

public class TelemetryClientTests
{
    private const int RpcEWrongThread = unchecked((int)0x8001010E);

    [Fact(DisplayName = "format_hresult_pads_to_eight_hex_digits_with_prefix")]
    public void FormatHResult_PadsToEightHexDigits()
    {
        // Arrange
        // Act
        string result = TelemetryClient.FormatHResult(RpcEWrongThread);

        // Assert
        Assert.Equal("0x8001010E", result);
    }

    [Fact(DisplayName = "resolve_hresult_prefers_inner_comexception_hresult")]
    public void ResolveHResult_WithWrappedComException_ReturnsComHResult()
    {
        // Arrange
        COMException inner = new(string.Empty, RpcEWrongThread);
        InvalidOperationException outer = new("wrapper", inner);

        // Act
        int result = TelemetryClient.ResolveHResult(outer);

        // Assert
        Assert.Equal(RpcEWrongThread, result);
    }

    [Fact(DisplayName = "resolve_hresult_without_comexception_falls_back_to_top_level_hresult")]
    public void ResolveHResult_WithoutComException_ReturnsTopLevelHResult()
    {
        // Arrange
        InvalidOperationException ex = new("boom");

        // Act
        int result = TelemetryClient.ResolveHResult(ex);

        // Assert
        Assert.Equal(ex.HResult, result);
    }

    [Fact(DisplayName = "build_full_message_surfaces_hresult_of_empty_message_comexception")]
    public void BuildFullMessage_WithEmptyMessageComException_IncludesHResult()
    {
        // Arrange
        COMException ex = new(string.Empty, RpcEWrongThread);

        // Act
        string result = TelemetryClient.BuildFullMessage(ex);

        // Assert
        Assert.Equal("[COMException 0x8001010E] ", result);
    }

    [Fact(DisplayName = "build_full_message_chains_inner_exceptions_with_their_hresults")]
    public void BuildFullMessage_WithInnerException_ChainsBothNodes()
    {
        // Arrange
        COMException inner = new(string.Empty, RpcEWrongThread);
        InvalidOperationException outer = new("layout failed", inner);

        // Act
        string result = TelemetryClient.BuildFullMessage(outer);

        // Assert
        Assert.Contains("[InvalidOperationException", result);
        Assert.Contains("layout failed", result);
        Assert.Contains(" ---> ", result);
        Assert.Contains("[COMException 0x8001010E] ", result);
    }
}
