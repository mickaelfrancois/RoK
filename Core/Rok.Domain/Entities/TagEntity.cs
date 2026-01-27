namespace Rok.Domain.Entities;


[Table("Tags")]
public class TagEntity
{
    [Dapper.Contrib.Extensions.Key]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
