using Rok.MetadataTool;

namespace Rok.ApplicationTests.Tools;

public class MixScanOptionsTests
{
    [Theory(DisplayName = "options_default_parallel_is_processor_count_minus_one")]
    [InlineData(8, 7)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void TryParse_DefaultParallel_IsProcessorCountMinusOne(int processors, int expected)
    {
        // Arrange
        string[] args = ["library.sqlite"];

        // Act
        var parsed = MixScanOptions.TryParse(args, processors, out var options, out var error);

        // Assert
        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(expected, options!.Parallel);
        Assert.Equal("library.sqlite", options.DatabasePath);
        Assert.False(options.Write);
        Assert.Null(options.Limit);
    }

    [Fact(DisplayName = "options_read_explicit_parallel_limit_and_write")]
    public void TryParse_ExplicitValues_AreRead()
    {
        // Arrange
        string[] args = ["library.sqlite", "--write", "--parallel", "3", "--limit", "10"];

        // Act
        var parsed = MixScanOptions.TryParse(args, 8, out var options, out _);

        // Assert
        Assert.True(parsed);
        Assert.True(options!.Write);
        Assert.Equal(3, options.Parallel);
        Assert.Equal(10, options.Limit);
    }

    [Fact(DisplayName = "options_accept_the_database_path_after_the_flags")]
    public void TryParse_PathAfterFlags_IsAccepted()
    {
        // Arrange
        string[] args = ["--limit", "5", "library.sqlite"];

        // Act
        var parsed = MixScanOptions.TryParse(args, 4, out var options, out _);

        // Assert
        Assert.True(parsed);
        Assert.Equal("library.sqlite", options!.DatabasePath);
        Assert.Equal(5, options.Limit);
    }

    [Theory(DisplayName = "options_reject_invalid_values")]
    [InlineData("library.sqlite", "--parallel", "0")]
    [InlineData("library.sqlite", "--parallel", "-2")]
    [InlineData("library.sqlite", "--limit", "abc")]
    [InlineData("library.sqlite", "--limit", "0")]
    [InlineData("library.sqlite", "--limit")]
    [InlineData("library.sqlite", "--parallel")]
    [InlineData("library.sqlite", "--unknown")]
    [InlineData("library.sqlite", "other.sqlite")]
    [InlineData("--write")]
    public void TryParse_InvalidArguments_ReturnsFalseWithError(params string[] args)
    {
        // Act
        var parsed = MixScanOptions.TryParse(args, 8, out var options, out var error);

        // Assert
        Assert.False(parsed);
        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact(DisplayName = "options_reject_missing_database_path")]
    public void TryParse_NoArguments_ReturnsFalse()
    {
        // Act
        var parsed = MixScanOptions.TryParse([], 8, out var options, out var error);

        // Assert
        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(error);
    }
}