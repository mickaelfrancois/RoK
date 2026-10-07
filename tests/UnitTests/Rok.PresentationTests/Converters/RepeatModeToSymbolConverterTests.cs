using Microsoft.UI.Xaml.Controls;
using Rok.Application.Player;
using Rok.Converters;

namespace Rok.PresentationTests.Converters;

public class RepeatModeToSymbolConverterTests
{
    [Theory(DisplayName = "convert_returns_the_symbol_matching_the_repeat_mode")]
    [InlineData(ERepeatMode.Off, Symbol.RepeatAll)]
    [InlineData(ERepeatMode.All, Symbol.RepeatAll)]
    [InlineData(ERepeatMode.One, Symbol.RepeatOne)]
    public void Convert_ReturnsSymbolForMode(ERepeatMode mode, Symbol expected)
    {
        // Arrange
        RepeatModeToSymbolConverter sut = new();

        // Act
        object result = sut.Convert(mode, typeof(Symbol), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "convert_returns_repeat_all_when_value_is_not_a_repeat_mode")]
    public void Convert_ReturnsRepeatAll_WhenValueIsNotARepeatMode()
    {
        // Arrange
        RepeatModeToSymbolConverter sut = new();

        // Act
        object result = sut.Convert("not-a-mode", typeof(Symbol), null!, "");

        // Assert
        Assert.Equal(Symbol.RepeatAll, result);
    }

    [Fact(DisplayName = "convert_back_throws_not_implemented")]
    public void ConvertBack_Throws()
    {
        // Arrange
        RepeatModeToSymbolConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(Symbol.RepeatAll, typeof(ERepeatMode), null!, ""));
    }
}