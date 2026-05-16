using Microsoft.UI.Xaml;
using Rok.Converters;

namespace Rok.PresentationTests.Converters;

public class IntToBoolConverterTests
{
    [Theory(DisplayName = "Convert should return true only for positive integers")]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData("not-an-int", false)]
    public void Convert_ShouldReturnTrueForPositiveInts(object? input, bool expected)
    {
        // Arrange
        IntToBoolConverter sut = new();

        // Act
        object result = sut.Convert(input!, typeof(bool), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "ConvertBack should map true to true and anything else to false")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(0, false)]
    public void ConvertBack_ShouldMapBoolToBool(object input, bool expected)
    {
        // Arrange
        IntToBoolConverter sut = new();

        // Act
        object result = sut.ConvertBack(input, typeof(int), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }
}

public class IntToVisibilityConverterTests
{
    [Theory(DisplayName = "Convert should map positive int to Visible and anything else to Collapsed")]
    [InlineData(5, Visibility.Visible)]
    [InlineData(0, Visibility.Collapsed)]
    [InlineData(-3, Visibility.Collapsed)]
    public void Convert_ShouldMapIntToVisibility(int input, Visibility expected)
    {
        // Arrange
        IntToVisibilityConverter sut = new();

        // Act
        object result = sut.Convert(input, typeof(Visibility), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "ConvertBack should map Visible to 1 and anything else to 0")]
    [InlineData(Visibility.Visible, 1)]
    [InlineData(Visibility.Collapsed, 0)]
    public void ConvertBack_ShouldMapVisibilityToInt(Visibility input, int expected)
    {
        // Arrange
        IntToVisibilityConverter sut = new();

        // Act
        object result = sut.ConvertBack(input, typeof(int), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }
}

public class DoubleToTimespanConverterTests
{
    [Theory(DisplayName = "Convert should format seconds as hours minutes seconds string")]
    [InlineData(0d, "00:00:00")]
    [InlineData(65d, "00:01:05")]
    [InlineData(3725d, "01:02:05")]
    public void Convert_ShouldFormatSecondsAsTimeSpanString(double seconds, string expected)
    {
        // Arrange
        DoubleToTimespanConverter sut = new();

        // Act
        object result = sut.Convert(seconds, typeof(string), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "ConvertBack should parse a TimeSpan string back to total seconds")]
    [InlineData("00:01:05", 65d)]
    [InlineData("invalid", 0d)]
    public void ConvertBack_ShouldParseTimeSpanString(string input, double expected)
    {
        // Arrange
        DoubleToTimespanConverter sut = new();

        // Act
        object result = sut.ConvertBack(input, typeof(double), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "ConvertBack should return zero when value is not a string")]
    public void ConvertBack_ShouldReturnZero_WhenNotString()
    {
        // Arrange
        DoubleToTimespanConverter sut = new();

        // Act
        object result = sut.ConvertBack(42, typeof(double), null!, "");

        // Assert
        Assert.Equal(0d, result);
    }
}

public class LongToTimespanConverterTests
{
    [Theory(DisplayName = "Convert should format long seconds as hours minutes seconds string")]
    [InlineData(0L, "00:00:00")]
    [InlineData(65L, "00:01:05")]
    [InlineData(3725L, "01:02:05")]
    public void Convert_ShouldFormatLongSecondsAsTimeSpanString(long seconds, string expected)
    {
        // Arrange
        LongToTimespanConverter sut = new();

        // Act
        object result = sut.Convert(seconds, typeof(string), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "ConvertBack should always return zero")]
    public void ConvertBack_ShouldReturnZero()
    {
        // Arrange
        LongToTimespanConverter sut = new();

        // Act
        object result = sut.ConvertBack("anything", typeof(long), null!, "");

        // Assert
        Assert.Equal(0L, result);
    }
}

public class FloatToStringConverterTests
{
    [Theory(DisplayName = "Convert should format positive float with plus sign and one decimal")]
    [InlineData(0f, "+")]
    [InlineData(2.5f, "+")]
    [InlineData(-3.5f, "-")]
    public void Convert_ShouldFormatFloatWithSignAndUnit(float input, string expectedSignPrefix)
    {
        // Arrange
        FloatToStringConverter sut = new();
        string formatted = input.ToString("F1");
        string expected = input >= 0 ? $"+{formatted} dB" : $"{formatted} dB";

        // Act
        object result = sut.Convert(input, typeof(string), null!, "");

        // Assert
        Assert.Equal(expected, result);
        Assert.StartsWith(expectedSignPrefix, (string)result);
    }

    [Fact(DisplayName = "Convert should return default unit when value is not a float")]
    public void Convert_ShouldReturnDefault_WhenNotFloat()
    {
        // Arrange
        FloatToStringConverter sut = new();

        // Act
        object result = sut.Convert("not-a-float", typeof(string), null!, "");

        // Assert
        Assert.Equal("0.0 dB", result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotImplementedException")]
    public void ConvertBack_ShouldThrow()
    {
        // Arrange
        FloatToStringConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack("ignored", typeof(float), null!, ""));
    }
}

public class NumberToFormattedStringConverterTests
{
    [Fact(DisplayName = "Convert should format an integer with grouping separators")]
    public void Convert_ShouldFormatInt()
    {
        // Arrange
        NumberToFormattedStringConverter sut = new();

        // Act
        object result = sut.Convert(1234, typeof(string), null!, "");

        // Assert
        Assert.IsType<string>(result);
        Assert.Contains("234", (string)result);
    }

    [Fact(DisplayName = "Convert should format a double with grouping separators")]
    public void Convert_ShouldFormatDouble()
    {
        // Arrange
        NumberToFormattedStringConverter sut = new();

        // Act
        object result = sut.Convert(1234.0, typeof(string), null!, "");

        // Assert
        Assert.IsType<string>(result);
        Assert.Contains("234", (string)result);
    }

    [Fact(DisplayName = "Convert should pass through values that are neither int nor double")]
    public void Convert_ShouldPassThroughOtherTypes()
    {
        // Arrange
        NumberToFormattedStringConverter sut = new();

        // Act
        object result = sut.Convert("hello", typeof(string), null!, "");

        // Assert
        Assert.Equal("hello", result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotImplementedException")]
    public void ConvertBack_ShouldThrow()
    {
        // Arrange
        NumberToFormattedStringConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack("ignored", typeof(int), null!, ""));
    }
}

public class TimeSpanToDoubleConverterTests
{
    [Fact(DisplayName = "Convert should return TimeSpan total seconds as double")]
    public void Convert_ShouldReturnTotalSeconds()
    {
        // Arrange
        TimeSpanToDoubleConverter sut = new();

        // Act
        object result = sut.Convert(TimeSpan.FromMinutes(2), typeof(double), null!, "");

        // Assert
        Assert.Equal(120d, result);
    }

    [Fact(DisplayName = "ConvertBack should return TimeSpan from seconds when value is provided")]
    public void ConvertBack_ShouldReturnTimeSpan()
    {
        // Arrange
        TimeSpanToDoubleConverter sut = new();

        // Act
        object result = sut.ConvertBack(120d, typeof(TimeSpan), null!, "");

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(2), result);
    }
}

public class VolumeToGlyphConverterTests
{
    [Theory(DisplayName = "Convert should map volume ranges to mute low medium and high glyph codes")]
    [InlineData(0d, "\xE992")]
    [InlineData(20d, "\xE993")]
    [InlineData(40d, "\xE994")]
    [InlineData(80d, "\xE995")]
    public void Convert_ShouldMapVolumeToGlyph(double volume, string expectedGlyph)
    {
        // Arrange
        VolumeToGlyphConverter sut = new();

        // Act
        object result = sut.Convert(volume, typeof(string), null!, "");

        // Assert
        Assert.Equal(expectedGlyph, result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotImplementedException")]
    public void ConvertBack_ShouldThrow()
    {
        // Arrange
        VolumeToGlyphConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack("ignored", typeof(double), null!, ""));
    }
}

public class StringToVisibilityConverterTests
{
    [Theory(DisplayName = "Convert should map non-empty string to Visible and empty or whitespace to Collapsed")]
    [InlineData("hello", Visibility.Visible)]
    [InlineData("", Visibility.Collapsed)]
    [InlineData("  ", Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    public void Convert_ShouldMapStringToVisibility(string? input, Visibility expected)
    {
        // Arrange
        StringToVisibilityConverter sut = new();

        // Act
        object result = sut.Convert(input!, typeof(Visibility), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "Convert should invert when InvertVisibility is true")]
    public void Convert_ShouldInvert_WhenInvertVisibilityTrue()
    {
        // Arrange
        StringToVisibilityConverter sut = new() { InvertVisibility = true };

        // Act
        object visibleWhenEmpty = sut.Convert("", typeof(Visibility), null!, "");
        object collapsedWhenFilled = sut.Convert("hello", typeof(Visibility), null!, "");

        // Assert
        Assert.Equal(Visibility.Visible, visibleWhenEmpty);
        Assert.Equal(Visibility.Collapsed, collapsedWhenFilled);
    }

    [Fact(DisplayName = "ConvertBack should return empty string")]
    public void ConvertBack_ShouldReturnEmptyString()
    {
        // Arrange
        StringToVisibilityConverter sut = new();

        // Act
        object result = sut.ConvertBack("ignored", typeof(string), null!, "");

        // Assert
        Assert.Equal("", result);
    }
}

public class LanguageAutoTranslationConverterTests
{
    [Theory(DisplayName = "Convert should show Visible only for translation-capable languages")]
    [InlineData("es-ES", Visibility.Visible)]
    [InlineData("uk-UA", Visibility.Visible)]
    [InlineData("en-US", Visibility.Collapsed)]
    [InlineData("fr-FR", Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    public void Convert_ShouldOnlyShowForTranslationLanguages(string? input, Visibility expected)
    {
        // Arrange
        LanguageAutoTranslationConverter sut = new();

        // Act
        object result = sut.Convert(input!, typeof(Visibility), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotImplementedException")]
    public void ConvertBack_ShouldThrow()
    {
        // Arrange
        LanguageAutoTranslationConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(Visibility.Visible, typeof(string), null!, ""));
    }
}

public class PlaybackStateToButtonIconConverterTests
{
    [Fact(DisplayName = "Convert should return Pause symbol when state is Playing")]
    public void Convert_ShouldReturnPause_WhenPlaying()
    {
        // Arrange
        PlaybackStateToButtonIconConverter sut = new();

        // Act
        object result = sut.Convert(Rok.Application.Player.EPlaybackState.Playing, typeof(Microsoft.UI.Xaml.Controls.Symbol), null!, "");

        // Assert
        Assert.Equal(Microsoft.UI.Xaml.Controls.Symbol.Pause, result);
    }

    [Theory(DisplayName = "Convert should return Play symbol when state is not Playing")]
    [InlineData(Rok.Application.Player.EPlaybackState.Paused)]
    [InlineData(Rok.Application.Player.EPlaybackState.Stopped)]
    [InlineData(Rok.Application.Player.EPlaybackState.Ended)]
    public void Convert_ShouldReturnPlay_WhenNotPlaying(Rok.Application.Player.EPlaybackState state)
    {
        // Arrange
        PlaybackStateToButtonIconConverter sut = new();

        // Act
        object result = sut.Convert(state, typeof(Microsoft.UI.Xaml.Controls.Symbol), null!, "");

        // Assert
        Assert.Equal(Microsoft.UI.Xaml.Controls.Symbol.Play, result);
    }

    [Fact(DisplayName = "Convert should return Play symbol when value is not a playback state")]
    public void Convert_ShouldReturnPlay_WhenValueIsNotPlaybackState()
    {
        // Arrange
        PlaybackStateToButtonIconConverter sut = new();

        // Act
        object result = sut.Convert("not-a-state", typeof(Microsoft.UI.Xaml.Controls.Symbol), null!, "");

        // Assert
        Assert.Equal(Microsoft.UI.Xaml.Controls.Symbol.Play, result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotImplementedException")]
    public void ConvertBack_ShouldThrow()
    {
        // Arrange
        PlaybackStateToButtonIconConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(Microsoft.UI.Xaml.Controls.Symbol.Play, typeof(Rok.Application.Player.EPlaybackState), null!, ""));
    }
}