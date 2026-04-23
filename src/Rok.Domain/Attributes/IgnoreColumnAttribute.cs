namespace Rok.Domain.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnNameAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}