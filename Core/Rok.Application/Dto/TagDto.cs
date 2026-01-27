namespace Rok.Application.Dto;

public class TagDto
{
    public override string ToString() => Name;

    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
