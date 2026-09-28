namespace Rok.Application.Interfaces;

/// <summary>
/// Outcome of the native Microsoft Store rating dialog.
/// </summary>
public enum StoreReviewStatus
{
    Succeeded,
    CanceledByUser,
    NetworkError,
    Error
}