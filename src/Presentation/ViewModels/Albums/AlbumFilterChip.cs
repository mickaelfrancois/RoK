namespace Rok.ViewModels.Albums;

public enum AlbumFilterKind
{
    List,
    Genre,
    Tag,
}

/// <summary>An active album filter shown as a removable chip. <paramref name="Value"/> is the filter key, the genre id or the tag.</summary>
public sealed record AlbumFilterChip(AlbumFilterKind Kind, string Value, string Label);