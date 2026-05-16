namespace Rok.Shared;


/// <summary>Represents an optional field in a patch operation, distinguishing between "not set" and "set to null".</summary>
public sealed class PatchField<T>
{
    /// <summary>Gets a value indicating whether this field has been explicitly set.</summary>
    public bool IsSet { get; private set; }

    /// <summary>Gets the value of this field, or <see langword="null"/> if not set or set to null.</summary>
    public T? Value { get; private set; }

    /// <summary>Initializes a new unset instance.</summary>
    public PatchField()
    {
        IsSet = false;
        Value = default;
    }

    /// <summary>Initializes a new instance with the specified value, marking the field as set.</summary>
    public PatchField(T? value)
    {
        IsSet = true;
        Value = value;
    }

    /// <summary>Returns the value if the field is set.</summary>
    /// <param name="value">When this method returns, contains the field value, or the default value if not set.</param>
    /// <returns><see langword="true"/> if the field is set; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out T? value)
    {
        value = Value;
        return IsSet;
    }

    /// <summary>Sets the field to the specified value.</summary>
    public void Set(T? value)
    {
        IsSet = true;
        Value = value;
    }

    /// <summary>Clears the field, marking it as not set.</summary>
    public void Clear()
    {
        IsSet = false;
        Value = default;
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        return IsSet ? Value?.ToString() : "NotSet";
    }
}