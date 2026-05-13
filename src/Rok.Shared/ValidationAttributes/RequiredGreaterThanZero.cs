using System.ComponentModel.DataAnnotations;

namespace Rok.Shared.ValidationAttributes;

public class RequiredGreaterThanZero : ValidationAttribute
{
    public RequiredGreaterThanZero()
    {
        ErrorMessage = "The field {0} must be greater than zero.";
    }

    public override bool IsValid(object? value)
    {
        if (value is not IConvertible convertible)
            return false;

        try
        {
            long numericValue = convertible.ToInt64(null);
            return numericValue > 0;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }
}