namespace Rok.Shared;


public class PatchField<T>
{
    public bool IsSet { get; set; }

    public T? Value { get; set; }

    public PatchField()
    {
        IsSet = false;
        Value = default;
    }

    public PatchField(T? value)
    {
        IsSet = true;
        Value = value;
    }
}

