using Rok.Shared.Extensions;

namespace Rok.ApplicationTests.Shared.Extensions;

public class StringExtensionsTests
{
    [Theory(DisplayName = "ToFileName should strip the path-invalid characters slash question mark and colon")]
    [InlineData("a/b?c:d", "abcd")]
    [InlineData("clean", "clean")]
    public void ToFileName_ShouldStripInvalidCharacters(string input, string expected)
    {
        // Act
        string result = input.ToFileName();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "ToFileName should trim trailing dots")]
    public void ToFileName_ShouldTrimTrailingDots()
    {
        // Act
        string result = "song...".ToFileName();

        // Assert
        Assert.Equal("song", result);
    }

    [Theory(DisplayName = "Capitalize should uppercase the first letter and lowercase the rest")]
    [InlineData("HELLO", "Hello")]
    [InlineData("a", "a")]
    [InlineData("", "")]
    [InlineData("mIxED", "Mixed")]
    public void Capitalize_ShouldNormalizeFirstLetter(string input, string expected)
    {
        // Act
        string result = input.Capitalize();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "GetNameFirstLetter should return uppercase letter or hash placeholder")]
    [InlineData("apple", "A")]
    [InlineData("Étoile", "É")]
    [InlineData("", "#123")]
    [InlineData("123abc", "#123")]
    [InlineData("#hash", "#123")]
    public void GetNameFirstLetter_ShouldReturnExpectedBucket(string input, string expected)
    {
        // Act
        string result = StringExtensions.GetNameFirstLetter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "IsDifferent should return true for differing strings ignoring case")]
    [InlineData("abc", "abc", false)]
    [InlineData("abc", "ABC", false)]
    [InlineData("abc", "abd", true)]
    public void IsDifferent_ShouldCompareCaseInsensitive(string a, string b, bool expected)
    {
        // Act
        bool result = a.IsDifferent(b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "AreEquals should treat null as empty string")]
    [InlineData(null, null, true)]
    [InlineData("", null, true)]
    [InlineData(null, "", true)]
    [InlineData("a", "a", true)]
    [InlineData("a", "b", false)]
    public void AreEquals_ShouldTreatNullAsEmpty(string? a, string? b, bool expected)
    {
        // Act
        bool result = a.AreEquals(b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "AreDifferents should be the inverse of AreEquals")]
    [InlineData(null, null, false)]
    [InlineData("a", null, true)]
    [InlineData("a", "a", false)]
    [InlineData("a", "b", true)]
    public void AreDifferents_ShouldBeInverseOfAreEquals(string? a, string? b, bool expected)
    {
        // Act
        bool result = a.AreDifferents(b);

        // Assert
        Assert.Equal(expected, result);
    }
}
