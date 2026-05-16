using Rok.Converters;

namespace Rok.PresentationTests.Converters;

public class SecondsToReadableConverterTests
{
    [Theory(DisplayName = "Convert should format seconds as a minutes string with two-digit padding")]
    [InlineData(0d, "00min.")]
    [InlineData(60d, "01min.")]
    [InlineData(3600d, "60min.")]
    [InlineData(-120d, "02min.")]
    public void Convert_ShouldFormatSecondsAsMinutes(double seconds, string expected)
    {
        // Arrange
        SecondsToReadableConverter sut = new();

        // Act
        object result = sut.Convert(seconds, typeof(string), null!, "en-US");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "Convert should return value unchanged when not convertible")]
    public void Convert_WithNonConvertible_ShouldReturnValueUnchanged()
    {
        // Arrange
        SecondsToReadableConverter sut = new();
        object value = new();

        // Act
        object result = sut.Convert(value, typeof(string), null!, "en-US");

        // Assert
        Assert.Same(value, result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotSupportedException")]
    public void ConvertBack_ShouldThrowNotSupported()
    {
        // Arrange
        SecondsToReadableConverter sut = new();

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => sut.ConvertBack("ignored", typeof(double), null!, "en-US"));
    }
}