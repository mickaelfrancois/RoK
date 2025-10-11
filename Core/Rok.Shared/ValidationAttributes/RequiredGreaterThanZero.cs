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
        return value != null && int.TryParse(value.ToString(), out int i) && i > 0;
    }
}