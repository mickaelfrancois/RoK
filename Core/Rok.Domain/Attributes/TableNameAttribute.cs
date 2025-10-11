namespace Rok.Domain.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class TableNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}