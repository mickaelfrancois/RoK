namespace Rok.Domain.Entities;

[Table("Countries")]
public class CountryEntity : BaseEntity
{
    public override string ToString() => French;

    public string Code { get; set; } = string.Empty;

    public string French { get; set; } = string.Empty;

    public string English { get; set; } = string.Empty;
}