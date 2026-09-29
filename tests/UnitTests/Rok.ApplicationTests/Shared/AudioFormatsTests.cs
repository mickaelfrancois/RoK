using Rok.Shared;

namespace Rok.ApplicationTests.Shared;

public class AudioFormatsTests
{
    [Fact(DisplayName = "supported_should_contain_exactly_the_media_foundation_formats")]
    public void Supported_ShouldContainExactlyMediaFoundationFormats()
    {
        // Arrange
        string[] expected = [".aac", ".aif", ".aiff", ".flac", ".m4a", ".mp3", ".wav", ".wma"];

        // Act
        IEnumerable<string> actual = AudioFormats.Supported.Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "known_unsupported_should_contain_exactly_ogg_opus_ape_and_wavpack")]
    public void KnownUnsupported_ShouldContainExactlyOggOpusApeAndWavPack()
    {
        // Arrange
        string[] expected = [".ape", ".ogg", ".opus", ".wv"];

        // Act
        IEnumerable<string> actual = AudioFormats.KnownUnsupported.Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "supported_and_known_unsupported_should_be_disjoint")]
    public void SupportedAndKnownUnsupported_ShouldBeDisjoint()
    {
        // Act
        IEnumerable<string> intersection = AudioFormats.Supported.Intersect(AudioFormats.KnownUnsupported, StringComparer.OrdinalIgnoreCase);

        // Assert
        Assert.Empty(intersection);
    }

    [Fact(DisplayName = "every_extension_should_start_with_a_dot_and_be_lowercase")]
    public void EveryExtension_ShouldStartWithDotAndBeLowercase()
    {
        // Arrange
        IEnumerable<string> all = AudioFormats.Supported.Concat(AudioFormats.KnownUnsupported);

        // Act & Assert
        Assert.All(all, extension =>
        {
            Assert.StartsWith(".", extension, StringComparison.Ordinal);
            Assert.Equal(extension.ToLowerInvariant(), extension);
        });
    }

    [Theory(DisplayName = "is_supported_should_return_true_whatever_the_extension_case")]
    [InlineData("track.M4A")]
    [InlineData("track.Flac")]
    [InlineData(@"C:\Music\Album\01 - track.aif")]
    [InlineData("track.WAV")]
    public void IsSupported_ShouldReturnTrue_WhateverExtensionCase(string filePath)
    {
        // Act
        bool result = AudioFormats.IsSupported(filePath);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "is_supported_should_return_false_for_unsupported_or_missing_extension")]
    [InlineData("track.ogg")]
    [InlineData("notes.txt")]
    [InlineData("track")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSupported_ShouldReturnFalse_ForUnsupportedOrMissingExtension(string? filePath)
    {
        // Act
        bool result = AudioFormats.IsSupported(filePath);

        // Assert
        Assert.False(result);
    }
}