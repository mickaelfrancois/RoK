using Rok.Application.Services;

namespace Rok.ApplicationTests.Services;

public class LevenshteinTests
{
    [Theory(DisplayName = "GetThreshold should return tolerance based on keyword length")]
    [InlineData("", 0)]
    [InlineData("a", 0)]
    [InlineData("abc", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 1)]
    [InlineData("abcdef", 2)]
    [InlineData("abcdefgh", 2)]
    [InlineData("abcdefghi", 3)]
    [InlineData("abcdefghijklmnop", 3)]
    public void GetThreshold_ShouldReturnExpectedTolerance(string keyword, int expected)
    {
        // Act
        int threshold = Levenshtein.GetThreshold(keyword);

        // Assert
        Assert.Equal(expected, threshold);
    }

    [Theory(DisplayName = "ComputeLevenshtein should return the edit distance between two strings")]
    [InlineData("", "", 0)]
    [InlineData("abc", "abc", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("abc", "abd", 1)]
    [InlineData("abc", "ac", 1)]
    [InlineData("abc", "abcd", 1)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("flaw", "lawn", 2)]
    [InlineData("abc", "xyz", 3)]
    public void ComputeLevenshtein_ShouldReturnEditDistance(string a, string b, int expected)
    {
        // Act
        int distance = Levenshtein.ComputeLevenshtein(a, b);

        // Assert
        Assert.Equal(expected, distance);
    }
}