namespace Rok.Shared.Extensions;

public static class FloatExtensions
{
    public static bool EqualsZero(this float value) => ((double)value).EqualsZero();
}