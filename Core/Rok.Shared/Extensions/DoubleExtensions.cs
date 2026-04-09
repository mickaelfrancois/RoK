namespace Rok.Shared.Extensions;

public static class DoubleExtensions
{
    private const float Epsilon = 1e-6f;

    public static bool EqualsZero(this double value)
    {
        return Math.Abs(value) < Epsilon;
    }
}