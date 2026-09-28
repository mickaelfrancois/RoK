using Windows.Services.Store;
using WinRT.Interop;

namespace Rok.Services;

internal sealed class StoreReviewService(ILogger<StoreReviewService> logger) : IStoreReviewService
{
    public async Task<StoreReviewStatus> RequestRateAndReviewAsync()
    {
        if (App.MainWindowHandle == 0)
        {
            logger.LogWarning("Store rating dialog skipped: the main window handle is not available.");
            return StoreReviewStatus.Error;
        }

        try
        {
            StoreContext context = StoreContext.GetDefault();
            InitializeWithWindow.Initialize(context, App.MainWindowHandle);

            StoreRateAndReviewResult result = await context.RequestRateAndReviewAppAsync();

            if (result.ExtendedError is not null)
                logger.LogWarning("Store rating dialog returned {Status} with HResult 0x{HResult:X8}: {Message}", result.Status, result.ExtendedError.HResult, result.ExtendedError.Message);

            return result.Status switch
            {
                StoreRateAndReviewStatus.Succeeded => StoreReviewStatus.Succeeded,
                StoreRateAndReviewStatus.CanceledByUser => StoreReviewStatus.CanceledByUser,
                StoreRateAndReviewStatus.NetworkError => StoreReviewStatus.NetworkError,
                _ => StoreReviewStatus.Error
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Store rating dialog failed.");
            return StoreReviewStatus.Error;
        }
    }
}