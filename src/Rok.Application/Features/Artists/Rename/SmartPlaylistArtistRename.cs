using Rok.Application.Dto;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Pure rewrite of smart playlist criteria when an artist is renamed.
/// </summary>
public static class SmartPlaylistArtistRename
{
    /// <summary>
    /// Replaces the old artist name by the new one in the artist name filters.
    /// Returns true when at least one filter changed.
    /// </summary>
    public static bool Apply(IEnumerable<PlaylistGroupDto> groups, string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName))
            return false;

        bool changed = false;

        foreach (PlaylistGroupDto group in groups)
        {
            foreach (PlaylistFilterDto filter in group.Filters)
            {
                if (filter.Entity != SmartPlaylistEntity.Artists || filter.Field != SmartPlaylistField.Name || filter.Value is null)
                    continue;

                string? rewritten = Rewrite(filter, oldName, newName);

                if (rewritten is null || rewritten == filter.Value)
                    continue;

                filter.Value = rewritten;
                changed = true;
            }
        }

        return changed;
    }

    private static string? Rewrite(PlaylistFilterDto filter, string oldName, string newName)
    {
        string value = filter.Value!;

        switch (filter.Operator)
        {
            case SmartPlaylistOperator.Equals:
            case SmartPlaylistOperator.NotEquals:
                return string.Equals(value.Trim(), oldName, StringComparison.OrdinalIgnoreCase) ? newName : null;

            case SmartPlaylistOperator.Contains:
            case SmartPlaylistOperator.NotContains:
            case SmartPlaylistOperator.StartsWith:
            case SmartPlaylistOperator.EndsWith:
                return value.Contains(oldName, StringComparison.OrdinalIgnoreCase)
                    ? value.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase)
                    : null;

            default:
                return null;
        }
    }
}