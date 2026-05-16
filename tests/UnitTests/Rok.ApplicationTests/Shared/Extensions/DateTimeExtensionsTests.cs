using Rok.Shared.Extensions;

namespace Rok.ApplicationTests.Shared.Extensions;

public class DateTimeExtensionsTests
{
    [Fact(DisplayName = "TruncateToMinutes should strip seconds and sub-second precision")]
    public void TruncateToMinutes_ShouldStripSecondsAndBelow()
    {
        // Arrange
        DateTime input = new(2024, 6, 15, 10, 30, 45, 500, DateTimeKind.Utc);

        // Act
        DateTime result = input.TruncateToMinutes();

        // Assert
        Assert.Equal(new DateTime(2024, 6, 15, 10, 30, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact(DisplayName = "TruncateToMinutes should preserve the DateTimeKind")]
    public void TruncateToMinutes_ShouldPreserveDateTimeKind()
    {
        // Arrange
        DateTime input = new(2024, 1, 1, 12, 0, 30, DateTimeKind.Local);

        // Act
        DateTime result = input.TruncateToMinutes();

        // Assert
        Assert.Equal(DateTimeKind.Local, result.Kind);
    }

    [Fact(DisplayName = "TruncateToMinutes on an already-truncated value should return the same value")]
    public void TruncateToMinutes_WhenAlreadyTruncated_ShouldBeIdempotent()
    {
        // Arrange
        DateTime input = new(2024, 3, 20, 8, 15, 0, DateTimeKind.Utc);

        // Act
        DateTime result = input.TruncateToMinutes();

        // Assert
        Assert.Equal(input, result);
    }
}