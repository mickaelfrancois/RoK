namespace Rok.Domain.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class IdentityColumnAttribute(string name = "Id") : Attribute
{
    public string Name { get; } = name;
}