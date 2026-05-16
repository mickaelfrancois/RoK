using Microsoft.UI.Xaml;
using Rok.Converters;

namespace Rok.PresentationTests.Converters;

public class BoolToVisibilityConverterTests
{
    [Theory(DisplayName = "Convert should map true to Visible and anything else to Collapsed")]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    [InlineData("not-a-bool", Visibility.Collapsed)]
    public void Convert_ShouldMapBoolToVisibility(object? input, Visibility expected)
    {
        // Arrange
        BoolToVisibilityConverter sut = new();

        // Act
        object result = sut.Convert(input!, typeof(Visibility), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "ConvertBack should map Visible to true and anything else to false")]
    [InlineData(Visibility.Visible, true)]
    [InlineData(Visibility.Collapsed, false)]
    public void ConvertBack_ShouldMapVisibilityToBool(Visibility input, bool expected)
    {
        // Arrange
        BoolToVisibilityConverter sut = new();

        // Act
        object result = sut.ConvertBack(input, typeof(bool), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }
}

public class BoolToInvertVisibilityConverterTests
{
    [Theory(DisplayName = "Convert should map true to Collapsed and anything else to Visible")]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    [InlineData(null, Visibility.Visible)]
    public void Convert_ShouldInvertBoolToVisibility(object? input, Visibility expected)
    {
        // Arrange
        BoolToInvertVisibilityConverter sut = new();

        // Act
        object result = sut.Convert(input!, typeof(Visibility), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "ConvertBack should map Visible to false and anything else to true")]
    [InlineData(Visibility.Visible, false)]
    [InlineData(Visibility.Collapsed, true)]
    public void ConvertBack_ShouldInvertVisibilityToBool(Visibility input, bool expected)
    {
        // Arrange
        BoolToInvertVisibilityConverter sut = new();

        // Act
        object result = sut.ConvertBack(input, typeof(bool), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }
}

public class BoolToInvertBoolConverterTests
{
    [Theory(DisplayName = "Convert should invert a boolean and return false for non-boolean values")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData("not-a-bool", false)]
    [InlineData(null, false)]
    public void Convert_ShouldInvertBool(object? input, bool expected)
    {
        // Arrange
        BoolToInvertBoolConverter sut = new();

        // Act
        object result = sut.Convert(input!, typeof(bool), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotImplementedException")]
    public void ConvertBack_ShouldThrow()
    {
        // Arrange
        BoolToInvertBoolConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(true, typeof(bool), null!, ""));
    }
}

public class BoolToOpacityConverterTests
{
    [Theory(DisplayName = "Convert should map true to 1.0 and anything else to 0.0")]
    [InlineData(true, 1.0)]
    [InlineData(false, 0.0)]
    [InlineData(null, 0.0)]
    [InlineData("not-a-bool", 0.0)]
    public void Convert_ShouldMapBoolToOpacity(object? input, double expected)
    {
        // Arrange
        BoolToOpacityConverter sut = new();

        // Act
        object result = sut.Convert(input!, typeof(double), null!, "");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "ConvertBack should throw NotImplementedException")]
    public void ConvertBack_ShouldThrow()
    {
        // Arrange
        BoolToOpacityConverter sut = new();

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => sut.ConvertBack(1.0, typeof(bool), null!, ""));
    }
}

public class BoolToGridLengthAutoConverterTests
{
    [Fact(DisplayName = "Convert should return TrueValue when input is true")]
    public void Convert_ShouldReturnTrueValue_WhenInputTrue()
    {
        // Arrange
        BoolToGridLengthAutoConverter sut = new();

        // Act
        object result = sut.Convert(true, typeof(GridLength), null!, "");

        // Assert
        Assert.Equal(GridLength.Auto, (GridLength)result);
    }

    [Fact(DisplayName = "Convert should return FalseValue when input is false")]
    public void Convert_ShouldReturnFalseValue_WhenInputFalse()
    {
        // Arrange
        BoolToGridLengthAutoConverter sut = new();

        // Act
        object result = sut.Convert(false, typeof(GridLength), null!, "");

        // Assert
        Assert.Equal(new GridLength(0), (GridLength)result);
    }

    [Fact(DisplayName = "Convert should invert when parameter is Invert")]
    public void Convert_ShouldInvert_WhenParameterIsInvert()
    {
        // Arrange
        BoolToGridLengthAutoConverter sut = new();

        // Act
        object trueInverted = sut.Convert(true, typeof(GridLength), "Invert", "");
        object falseInverted = sut.Convert(false, typeof(GridLength), "Invert", "");

        // Assert
        Assert.Equal(new GridLength(0), (GridLength)trueInverted);
        Assert.Equal(GridLength.Auto, (GridLength)falseInverted);
    }

    [Fact(DisplayName = "ConvertBack should map Auto and positive Pixel lengths to true")]
    public void ConvertBack_ShouldMapAutoAndPositivePixelToTrue()
    {
        // Arrange
        BoolToGridLengthAutoConverter sut = new();

        // Act
        object autoResult = sut.ConvertBack(GridLength.Auto, typeof(bool), null!, "");
        object positivePixel = sut.ConvertBack(new GridLength(10), typeof(bool), null!, "");
        object zeroPixel = sut.ConvertBack(new GridLength(0), typeof(bool), null!, "");

        // Assert
        Assert.True((bool)autoResult);
        Assert.True((bool)positivePixel);
        Assert.False((bool)zeroPixel);
    }

    [Fact(DisplayName = "ConvertBack should return false when value is not a GridLength")]
    public void ConvertBack_ShouldReturnFalse_WhenValueIsNotGridLength()
    {
        // Arrange
        BoolToGridLengthAutoConverter sut = new();

        // Act
        object result = sut.ConvertBack("not-a-gridlength", typeof(bool), null!, "");

        // Assert
        Assert.False((bool)result);
    }

    [Fact(DisplayName = "ConvertBack should invert when parameter is Invert")]
    public void ConvertBack_ShouldInvert_WhenParameterIsInvert()
    {
        // Arrange
        BoolToGridLengthAutoConverter sut = new();

        // Act
        object inverted = sut.ConvertBack(GridLength.Auto, typeof(bool), "Invert", "");

        // Assert
        Assert.False((bool)inverted);
    }
}