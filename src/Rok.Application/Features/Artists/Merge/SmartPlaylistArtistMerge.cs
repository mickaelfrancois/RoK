using Rok.Application.Dto;
using Rok.Application.Features.Artists.Rename;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Pure rules on smart playlist criteria when two artists are merged.
/// </summary>
public static class SmartPlaylistArtistMerge
{
    /// <summary>
    /// Replaces the merged artist's name by the kept artist's name in the artist name filters.
    /// Returns true when at least one filter changed.
    /// </summary>
    public static bool Apply(IEnumerable<PlaylistGroupDto> groups, string sourceName, string targetName)
    {
        return SmartPlaylistArtistRename.Apply(groups, sourceName, targetName);
    }

    /// <summary>
    /// Returns the impact of the merge on a smart playlist, or null when no artist name filter designates
    /// the merged or the kept artist. The groups are not modified.
    /// </summary>
    public static SmartPlaylistMergeImpact? Describe(long playlistId, string playlistName, IReadOnlyCollection<PlaylistGroupDto> groups, string sourceName, string targetName)
    {
        bool designates = groups.SelectMany(g => g.Filters)
            .Where(IsArtistNameFilter)
            .Any(f => Designates(f, sourceName) || Designates(f, targetName));

        if (!designates)
            return null;

        return new SmartPlaylistMergeImpact(playlistId, playlistName, WouldRewrite(groups, sourceName, targetName));
    }

    private static bool WouldRewrite(IEnumerable<PlaylistGroupDto> groups, string sourceName, string targetName)
    {
        List<PlaylistGroupDto> copy = groups
            .Select(g => new PlaylistGroupDto
            {
                Filters = g.Filters.Where(IsArtistNameFilter).Select(Clone).ToList(),
            })
            .ToList();

        return Apply(copy, sourceName, targetName);
    }

    private static PlaylistFilterDto Clone(PlaylistFilterDto filter)
    {
        return new PlaylistFilterDto
        {
            Entity = filter.Entity,
            Field = filter.Field,
            FieldType = filter.FieldType,
            Operator = filter.Operator,
            Value = filter.Value,
            Value2 = filter.Value2,
        };
    }

    private static bool IsArtistNameFilter(PlaylistFilterDto filter)
    {
        return filter.Entity == SmartPlaylistEntity.Artists && filter.Field == SmartPlaylistField.Name && !string.IsNullOrWhiteSpace(filter.Value);
    }

    private static bool Designates(PlaylistFilterDto filter, string artistName)
    {
        string value = filter.Value!;

        switch (filter.Operator)
        {
            case SmartPlaylistOperator.Equals:
            case SmartPlaylistOperator.NotEquals:
                return string.Equals(value.Trim(), artistName, StringComparison.OrdinalIgnoreCase);

            case SmartPlaylistOperator.Contains:
            case SmartPlaylistOperator.NotContains:
                return Pattern(value) is { Length: > 0 } contained && artistName.Contains(contained, StringComparison.OrdinalIgnoreCase);

            case SmartPlaylistOperator.StartsWith:
                return Pattern(value) is { Length: > 0 } prefix && artistName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            case SmartPlaylistOperator.EndsWith:
                return Pattern(value) is { Length: > 0 } suffix && artistName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

            default:
                return false;
        }
    }

    private static string Pattern(string value)
    {
        return value.Replace("%", string.Empty, StringComparison.Ordinal);
    }
}