namespace Rok.Shared.Extensions;

public static class FloatExtensions
{
    private const float Epsilon = 1e-6f;

    public static bool EqualsZero(this float value)
    {
        return Math.Abs(value) < Epsilon;
    }
}