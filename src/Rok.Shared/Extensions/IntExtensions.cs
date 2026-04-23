namespace Rok.Shared.Extensions;

public static class IntExtensions
{
    /// <summary>
    /// Determines whether two nullable integers are equal, treating <see langword="null"/> as a special value.
    /// </summary>
    /// <param name="a">The first nullable integer to compare. If <see langword="null"/>, it is treated as <see cref="int.MinValue"/>.</param>
    /// <param name="b">The second nullable integer to compare. If <see langword="null"/>, it is treated as <see cref="int.MinValue"/>.</param>
    /// <returns><see langword="true"/> if the values of <paramref name="a"/> and <paramref name="b"/> are equal; otherwise, <see
    /// langword="false"/>.</returns>
    public static bool AreEquals(this int? a, int? b) => (a ?? int.MinValue) == (b ?? int.MinValue);

    /// <summary>
    /// Determines whether two nullable integers are different, treating <see langword="null"/> as a special value.
    /// </summary>
    /// <remarks>If either <paramref name="a"/> or <paramref name="b"/> is <see langword="null"/>, it is
    /// treated as <see cref="int.MinValue"/> for the comparison.</remarks>
    /// <param name="a">The first nullable integer to compare.</param>
    /// <param name="b">The second nullable integer to compare.</param>
    /// <returns><see langword="true"/> if the values of <paramref name="a"/> and <paramref name="b"/> are different; otherwise,
    /// <see langword="false"/>.</returns>
    public static bool AreDifferents(this int? a, int? b) => (a ?? int.MinValue) != (b ?? int.MinValue);
}
