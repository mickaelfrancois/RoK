namespace Rok.ViewModels.Common;

/// <summary>
/// Tracks whether a detail view model holds a real entity yet.
/// Commands that need the entity stay disabled outside <see cref="Loaded"/>, and the
/// "not found" empty state only shows in <see cref="NotFound"/> — never while loading.
/// </summary>
public enum DetailLoadState
{
    /// <summary>The entity is being fetched. Nothing is known yet.</summary>
    Loading,

    /// <summary>The entity is available, either fetched or handed over by a list.</summary>
    Loaded,

    /// <summary>The entity does not exist. The view shows a "not found" empty state.</summary>
    NotFound
}