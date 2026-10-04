namespace Rok.ViewModels.Common;

public enum FilterKind
{
    List,
    Genre,
    Tag,
}

/// <summary>An active list filter shown as a removable chip. <paramref name="Value"/> is the filter key, the genre id or the tag.</summary>
public sealed record FilterChip(FilterKind Kind, string Value, string Label);