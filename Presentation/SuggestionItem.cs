namespace Rok;

public class SuggestionItem
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public object Data { get; set; } = null!;

    public override string ToString() => Name;
}
