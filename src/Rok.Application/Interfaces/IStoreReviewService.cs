namespace Rok.Application.Interfaces;

public interface IStoreReviewService
{
    /// <summary>
    /// Opens the native Microsoft Store rating dialog inside the app. Never throws.
    /// </summary>
    Task<StoreReviewStatus> RequestRateAndReviewAsync();
}