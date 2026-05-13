namespace Rok.Shared.Extensions;

public static class DoubleExtensions
{
    private const double Epsilon = 1e-6;

    public static bool EqualsZero(this double value)
    {
        return Math.Abs(value) < Epsilon;
    }
}