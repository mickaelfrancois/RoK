namespace Rok.Shared.Extensions;

public static class IntExtensions
{
    /// <summary>
    /// Determines whether two nullable integers are equal.
    /// </summary>
    /// <param name="a">The first nullable integer to compare.</param>
    /// <param name="b">The second nullable integer to compare.</param>
    /// <returns><see langword="true"/> if the values of <paramref name="a"/> and <paramref name="b"/> are equal; otherwise, <see
    /// langword="false"/>.</returns>
    public static bool AreEquals(this int? a, int? b) => a == b;

    /// <summary>
    /// Determines whether two nullable integers are different.
    /// </summary>
    /// <param name="a">The first nullable integer to compare.</param>
    /// <param name="b">The second nullable integer to compare.</param>
    /// <returns><see langword="true"/> if the values of <paramref name="a"/> and <paramref name="b"/> are different; otherwise,
    /// <see langword="false"/>.</returns>
    public static bool AreDifferent(this int? a, int? b) => a != b;
}
