namespace Rok.Shared;


public class PatchField<T>
{
    public bool IsSet { get; private set; }

    public T? Value { get; private set; }

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

    public bool TryGetValue(out T? value)
    {
        value = Value;
        return IsSet;
    }

    public void Set(T? value)
    {
        IsSet = true;
        Value = value;
    }

    public void Clear()
    {
        IsSet = false;
        Value = default;
    }

    public override string? ToString()
    {
        return IsSet ? Value?.ToString() : "NotSet";
    }
}
